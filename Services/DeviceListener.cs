using System;
using System.Threading.Tasks;
using HnService.Models;
using HnService.Helpers.Api;
using HnService.Models.Application;
using HnService.Models.DeviceResults;
using System.Text.RegularExpressions;
using DynamicExpresso;
using System.Linq;

namespace HnService.Services {
    public class DeviceListener : IDisposable {
        public DeviceListener(HnProcessModel processModel) {
            _processModel = processModel;
            _processModel.ProcStatus = 0;

            _apiNodes = new ApiHelper(HnSession.GetInstance().NodesApiUrl);
            
            _apiDevice = new ApiHelper(HnSession.GetInstance().DeviceApiUrl);
            _apiDevice.AddHeader("Accept", "vdn.dac.v1");
        }

        ApiHelper _apiNodes;
        ApiHelper _apiDevice;
        HnProcessModel _processModel;
        bool _runListen = false;
        Task _taskListen;
        bool _runCheckLive = false;
        Task _taskCheckLive;
        TimeSpan _processStartTime;
        ProcessStepModel _activeStep;
        private ProcessStepModel ActiveStep {
            get{
                if (_activeStep == null)
                    _activeStep = _processModel.Steps.OrderBy(d => d.OrderNo).FirstOrDefault();

                return _activeStep;
            }
            set {
                _activeStep = value;
            }
        }
        private async Task ProceedToNextStep(){
            var nextStep = _processModel.Steps.Where(d => d.OrderNo > ActiveStep.OrderNo)
                .OrderBy(d => d.OrderNo).FirstOrDefault();
            if (nextStep == null)
            {
                _processModel.ProcStatus = 0;
                await _apiNodes.PutData<HnProcessModel>("Process", _processModel);
                Console.WriteLine("TEST FINISHED");
            }

            ActiveStep = nextStep;
        }
        public void Start(){
            _runCheckLive = true;
            _taskCheckLive = Task.Run(CheckLiveLoop);

            _runListen = true;
            _taskListen = Task.Run(ListenerLoop);
            Console.WriteLine("-- ++ " + _processModel.Name + " IS STARTED");
        }
        public void Stop() {
            _runListen=false;
            _runCheckLive = false;

            try
            {
                if (_taskListen != null)
                    _taskListen.Dispose();

                if (_taskCheckLive != null)
                    _taskCheckLive.Dispose();
            }
            catch (System.Exception)
            {
                
            }
        }

        public void Dispose(){
            Stop();
        }
    
        private async Task<bool> CheckCondition(string comparison){
            try
            {
                string conditionText = comparison;
                var variables = Regex.Matches(comparison, "\\[[^\\[\\]]+\\]");
                foreach (Match vrb in variables)
                {
                    var ioPort = vrb.Value.Replace("[","").Replace("]","").ToLower();
                    var ioResults = await _apiDevice.GetData<DigitalIOResult>("slot/0/io/" + ioPort.Substring(0,2));

                    if (ioPort.Substring(0,2) == "di"){
                        var portResult = ioResults.io.di.FirstOrDefault(d => d.diIndex == Convert.ToInt32(ioPort.Replace("di","")));
                        if (portResult != null){
                            conditionText = conditionText.Replace(vrb.Value, portResult.diStatus.ToString());
                        }
                    }
                    else if (ioPort.Substring(0,2) == "do"){
                        var portResult = ioResults.io.Do.FirstOrDefault(d => d.doIndex == Convert.ToInt32(ioPort.Replace("do","")));
                        if (portResult != null){
                            conditionText = conditionText.Replace(vrb.Value, portResult.doStatus.ToString());
                        }
                    }
                }

                var expresso = new Interpreter();
                return expresso.Eval<bool>(conditionText);
            }
            catch (System.Exception)
            {
                
            }

            return false;
        }
        
        private async Task MakeResultActions(ProcessStepModel step, bool conditionSucceeded){
            try
            {
                // VARIATION EXAMPLES: [STORE_DI3], [DO5] = 0 
                if (!string.IsNullOrEmpty(step.ResultAction)){
                    var commands = Regex.Split(step.ResultAction, ",");
                    foreach (string cmd in commands)
                    {
                        var parsedCmd = cmd.Trim()
                            .Replace("[","")
                            .Replace("]", "");

                        TimeSpan diffForDuration = DateTime.Now.TimeOfDay - _processStartTime;
                        
                        if (cmd.StartsWith("STORE")){
                            var ioPort = (Regex.Split(parsedCmd, "_")[1]).ToLower();
                            var ioResults = await _apiDevice.GetData<DigitalIOResult>("slot/0/io/" + ioPort.Substring(0,2));

                            int portStatus = 0;
                            if (ioPort.Substring(0,2) == "di"){
                                var portResult = ioResults.io.di.FirstOrDefault(d => d.diIndex == Convert.ToInt32(ioPort.Replace("di","")));
                                portStatus = portResult.diStatus;
                            }
                            else if (ioPort.Substring(0,2) == "do"){
                                var portResult = ioResults.io.Do.FirstOrDefault(d => d.doIndex == Convert.ToInt32(ioPort.Replace("do","")));
                                portStatus = portResult.doStatus;
                            }

                            Console.WriteLine("STORE: " + conditionSucceeded);

                            await _apiNodes.PostData<ProcessResultModel>("Results", new ProcessResultModel{
                                CreatedDate = DateTime.Now,
                                ProcessStepId = step.ProcessStepId,
                                StrResult = null,
                                NumResult = portStatus,
                                IsOk = conditionSucceeded,
                                DurationInSeconds = Convert.ToInt32(diffForDuration.TotalSeconds),
                            });
                        }
                        else if (cmd == "STOP") {
                            HnProcessModel liveProcModel = await _apiNodes.GetData<HnProcessModel>("Process/" + _processModel.HnProcessId);
                            Console.WriteLine("STOPPED");

                            await _apiNodes.PutData<HnProcessModel>("Process", new HnProcessModel{
                                ProcStatus = liveProcModel.ProcStatus,
                                HnProcessId = _processModel.HnProcessId,
                                MustBeStopped = true,
                            });
                        }
                        else if (parsedCmd.StartsWith("DI") || parsedCmd.StartsWith("DO")){
                            var cmdArgs = Regex.Split(parsedCmd, "=");
                            
                            var ioPort = cmdArgs[0].Trim().ToLower();
                            var ioValue = cmdArgs[1].Trim();

                            var ioResults = await _apiDevice.GetData<DigitalIOResult>("slot/0/io/" + ioPort.Substring(0,2));

                            if (ioPort.Substring(0,2) == "di"){
                                var portResult = ioResults.io.di.FirstOrDefault(d => d.diIndex == Convert.ToInt32(ioPort.Replace("di","")));
                                portResult.diStatus = Convert.ToInt32(ioValue);
                            }
                            else if (ioPort.Substring(0,2) == "do"){
                                var portResult = ioResults.io.Do.FirstOrDefault(d => d.doIndex == Convert.ToInt32(ioPort.Replace("do","")));
                                portResult.doStatus = Convert.ToInt32(ioValue);
                            }

                            await _apiDevice.PutData<DigitalIOResult>("slot/0/io/" + ioPort.Substring(0,2), ioResults);
                        }
                    }
                }
            }
            catch (System.Exception)
            {
                
            }
        }
        private async Task ListenerLoop(){
            while (_runListen){

                // WAIT FOR PROCESS IS STARTED
                if (_processModel.ProcStatus != 1)
                {
                    await Task.Delay(30);
                    continue;
                }

                if (_activeStep == null && _processModel.DelayBefore > 0)
                    await Task.Delay(_processModel.DelayBefore);

                try
                {
                    var step = ActiveStep;
                    Console.WriteLine(step.Explanation);
                    if (step.DelayBefore > 0)
                        await Task.Delay(step.DelayBefore.Value);

                    bool conditionSucceeded = false;
                    // DECIDE CONDITION STATUS (WAIT OR PROCEED)
                    if (step.WaitUntilConditionRealized == true){
                        TimeSpan conditionCheckStartTime = DateTime.Now.TimeOfDay;

                        bool conditionTimedOut = false;

                        while (!(await CheckCondition(step.Comparison))) {
                            await Task.Delay(50);

                            TimeSpan conditionChecktime = DateTime.Now.TimeOfDay;

                            if (step.ConditionRealizeTimeout > 0){
                                TimeSpan diffCondTime = conditionChecktime - conditionCheckStartTime;

                                Console.WriteLine(diffCondTime.TotalMilliseconds);

                                if (Convert.ToInt32(diffCondTime.TotalMilliseconds) >= step.ConditionRealizeTimeout){
                                    conditionTimedOut = true;
                                    break;
                                }
                            }
                        }

                        if (!conditionTimedOut)
                            conditionSucceeded = true;
                    }
                    else {
                        conditionSucceeded = await CheckCondition(step.Comparison);
                    }

                    // FIRE RESULT ACTION
                    if (conditionSucceeded)
                        await MakeResultActions(step, conditionSucceeded);

                    if (step.DelayAfter > 0)
                        await Task.Delay(step.DelayAfter.Value);

                    await ProceedToNextStep();
                }
                catch (System.Exception)
                {
                    
                }

                if (_activeStep == null && _processModel.DelayAfter > 0)
                    await Task.Delay(_processModel.DelayAfter);
            }
        }
    
        private async Task CheckLiveLoop(){
            while (_runCheckLive){
                try
                {
                    if (!string.IsNullOrEmpty(_processModel.LiveCondition)){
                        var procStatus = await CheckCondition(_processModel.LiveCondition) ? 1 : 0;

                        if (procStatus != _processModel.ProcStatus) {

                            // IF THE PROCESS HAS GOT ALIVE THEN SET ITS START TIME
                            if (procStatus == 1)
                                _processStartTime = DateTime.Now.TimeOfDay;
                            // else if (procStatus == 0) {
                            //     // IF THE PROCESS IS NOT ALIVE THEN RESET ACTIVE STEP
                            //     if (_activeStep != null)
                            //         _activeStep = null;
                                
                            // }

                            if (procStatus == 1){
                                _processModel.ProcStatus = procStatus;
                                await _apiNodes.PutData<HnProcessModel>("Process", _processModel);
                                Console.WriteLine("TEST STARTED");
                            }
                        }
                    }
                    else
                        _processModel.ProcStatus = 1;
                    

                    var liveProcModel = await _apiNodes.GetData<HnProcessModel>("Process/" + _processModel.HnProcessId);
                    if (liveProcModel != null){
                        if (liveProcModel.MustBeStopped == true){
                            _processModel.ProcStatus = 0;

                            liveProcModel.ProcStatus = 0;
                            liveProcModel.MustBeStopped = false;
                            await _apiNodes.PutData<HnProcessModel>("Process", liveProcModel);

                            if (_activeStep != null)
                                _activeStep = null;
                        }
                    }
                }
                catch (System.Exception)
                {
                    
                }

                await Task.Delay(100);
            }
        }
    }
}