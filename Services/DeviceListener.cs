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
        bool _activeProcessFinished = false;
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
                try
                {
                    await _apiNodes.PutData<HnProcessModel>("Process", _processModel);
                }
                catch (System.Exception)
                {
                    Console.WriteLine("HN-API is not accessible!");
                }
            }

            ActiveStep = nextStep;
        }

        private async Task RewindToFirstStep(){
            try
            {
                _processModel.ProcStatus = 0;
                try
                {
                    await _apiNodes.PutData<HnProcessModel>("Process", _processModel);
                }
                catch (System.Exception)
                {
                    Console.WriteLine("HN-API is not accessible!");
                }
            }
            catch (System.Exception)
            {
                
            }

            ActiveStep = null;
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

        private async Task<string> PrepareConditionText(string comparison){
            try
            {
                string conditionText = comparison;
                var variables = Regex.Matches(comparison, "\\[[^\\[\\]]+\\]");
                foreach (Match vrb in variables)
                {
                    var ioPort = vrb.Value.Replace("[","").Replace("]","").ToLower().Replace("ı","i");
                    var ioResult = await GetFromDevice(ioPort);
                    conditionText = conditionText.Replace(vrb.Value, ioResult.ToString());
                }

                return conditionText;
            }
            catch (System.Exception)
            {
                
            }

            return string.Empty;
        }
    
        private async Task<bool> CheckCondition(string comparison, int satisfiedTime=0){
            try
            {
                var expresso = new Interpreter();
                bool resultSatisfied = false;
                
                string conditionText = await PrepareConditionText(comparison);
                resultSatisfied = expresso.Eval<bool>(conditionText);
                
                while (satisfiedTime > 0 && !_processModel.MustBeStopped){
                    TimeSpan loopStart = DateTime.Now.TimeOfDay;

                    conditionText = await PrepareConditionText(comparison);
                    resultSatisfied = expresso.Eval<bool>(conditionText);

                    // if (resultSatisfied == false)
                    //     break;
                    await Task.Delay(100);

                    TimeSpan loopEnd = DateTime.Now.TimeOfDay;
                    satisfiedTime -= Convert.ToInt32((loopEnd - loopStart).TotalMilliseconds) + 100;
                }

                return resultSatisfied;
            }
            catch (System.Exception)
            {
                
            }

            return false;
        }

        private async Task<int> GetFromDevice(string ioPort){
            try
            {
                int result = -1;

                var ioResults = await _apiDevice.GetData<DigitalIOResult>("slot/0/io/" + ioPort.Substring(0,2));

                if (ioPort.Substring(0,2) == "di"){
                    var portResult = ioResults.io.di.FirstOrDefault(d => d.diIndex == Convert.ToInt32(ioPort.Replace("di","")));
                    if (portResult != null){
                        result = portResult.diStatus;
                    }
                }
                else if (ioPort.Substring(0,2) == "do"){
                    var portResult = ioResults.io.Do.FirstOrDefault(d => d.doIndex == Convert.ToInt32(ioPort.Replace("do","")));
                    if (portResult != null){
                        result = portResult.doStatus;
                    }
                }

                await SetProcessConnected(true);

                return result;
            }
            catch (System.Exception)
            {
                await SetProcessConnected(false);
                Console.WriteLine("MOXA device is not accessible!");
            }

            return -1;
        }

        private async Task SendToDevice(string ioPort, int ioValue){
            try
            {
                var ioResults = await _apiDevice.GetData<DigitalIOResult>("slot/0/io/" + ioPort.Substring(0,2));

                if (ioPort.Substring(0,2) == "di"){
                    var portResult = ioResults.io.di.FirstOrDefault(d => d.diIndex == Convert.ToInt32(ioPort.Replace("di","")));
                    portResult.diStatus = ioValue;
                }
                else if (ioPort.Substring(0,2) == "do"){
                    var portResult = ioResults.io.Do.FirstOrDefault(d => d.doIndex == Convert.ToInt32(ioPort.Replace("do","")));
                    portResult.doStatus = ioValue;
                }

                await _apiDevice.PutData<DigitalIOResult>("slot/0/io/" + ioPort.Substring(0,2), ioResults);

                await SetProcessConnected(true);
            }
            catch (System.Exception)
            {
                await SetProcessConnected(false);
                Console.WriteLine("MOXA device is not accessible!");
            }
        }

        private async Task SetProcessConnected(bool deviceConnected){
            try
            {
                if (deviceConnected != _processModel.IsDeviceConnected){
                    var livePrModel = await _apiNodes.GetData<HnProcessModel>("process/" + _processModel.HnProcessId);
                    if (livePrModel != null){
                        livePrModel.IsDeviceConnected = deviceConnected;
                        _processModel.IsDeviceConnected = deviceConnected;
                        await _apiNodes.PostData<HnProcessModel>("process/", livePrModel);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private async Task MakeParallelActions(ProcessStepModel step, bool conditionSucceeded){
            try
            {
                // VARIATION EXAMPLES: [STORE_DI3], [DO5] = 0 
                string actionText = step.ParallelAction;

                if (!string.IsNullOrEmpty(actionText)){
                    var commands = Regex.Split(actionText, ",");
                    foreach (string cmd in commands)
                    {
                        var parsedCmd = cmd.Trim()
                            .Replace("[","")
                            .Replace("]", "");

                        TimeSpan diffForDuration = DateTime.Now.TimeOfDay - _processStartTime;
                        
                        if (parsedCmd.StartsWith("STORE")){
                            var ioPort = (Regex.Split(parsedCmd, "_")[1]).ToLower().Replace("ı","i");
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

                            Console.WriteLine("STORE: " + (conditionSucceeded ? "OK" : "Not OK"));

                            await _apiNodes.PostData<ProcessResultModel>("Results", new ProcessResultModel{
                                CreatedDate = DateTime.Now,
                                ProcessStepId = step.ProcessStepId,
                                StrResult = null,
                                NumResult = portStatus,
                                IsOk = conditionSucceeded,
                                DurationInSeconds = Convert.ToInt32(diffForDuration.TotalSeconds),
                            });
                        }
                        else if (parsedCmd == "STOP") {
                            HnProcessModel liveProcModel = await _apiNodes.GetData<HnProcessModel>("Process/" + _processModel.HnProcessId);
                            Console.WriteLine("STOPPED");

                            await _apiNodes.PutData<HnProcessModel>("Process", new HnProcessModel{
                                ProcStatus = liveProcModel.ProcStatus,
                                HnProcessId = _processModel.HnProcessId,
                                MustBeStopped = true,
                            });
                        }
                        else if (parsedCmd.StartsWith("TOGGLE")){ // [TOGGLE_DO5_500_4000]
                            var cmdArgs = Regex.Split(parsedCmd, "_");
                            int toggleTimeout = -999, toggleDelay = 500, lastSentSignal=0;

                            if (cmdArgs.Length >= 3)
                                toggleDelay = Convert.ToInt32(cmdArgs[2]);
                            if (cmdArgs.Length >= 4)
                                toggleTimeout = Convert.ToInt32(cmdArgs[3]);

                            // Console.WriteLine(toggleDelay + " : " + toggleTimeout);

                            while (toggleTimeout == -999 || toggleTimeout > -1){
                                if ((_activeProcessFinished == true || _processModel.MustBeStopped || ActiveStep != step))
                                    break;
                                
                                lastSentSignal = lastSentSignal == 0 ? 1 : 0;
                                await SendToDevice(cmdArgs[1].ToLower().Replace("ı","i"), lastSentSignal);

                                if (toggleTimeout != -999)
                                    toggleTimeout -= toggleDelay;

                                if (toggleTimeout > -1 || toggleTimeout == -999)
                                    await Task.Delay(toggleDelay);
                            }
                        }
                        else if (parsedCmd.StartsWith("DI") || parsedCmd.StartsWith("DO")){
                            var cmdArgs = Regex.Split(parsedCmd, "=");
                            
                            var ioPort = cmdArgs[0].Trim().ToLower().Replace("ı","i");
                            var ioValue = cmdArgs[1].Trim();

                            await SendToDevice(ioPort, Convert.ToInt32(ioValue));
                        }
                    }
                }
            }
            catch (System.Exception)
            {
                
            }
        }
        
        private async Task MakeResultActions(ProcessStepModel step, bool conditionSucceeded, bool isElseAction=false){
            try
            {
                // VARIATION EXAMPLES: [STORE_DI3], [DO5] = 0 
                string actionText = step.ResultAction;
                if (isElseAction)
                    actionText = step.ElseAction;

                // Console.WriteLine(isElseAction ? "ELSE: " : "IF: " + actionText);

                if (!string.IsNullOrEmpty(actionText)){
                    var commands = Regex.Split(actionText, ",");
                    foreach (string cmd in commands)
                    {
                        var parsedCmd = cmd.Trim()
                            .Replace("[","")
                            .Replace("]", "");

                        TimeSpan diffForDuration = DateTime.Now.TimeOfDay - _processStartTime;
                        
                        if (parsedCmd.StartsWith("STORE")){
                            string[] cmdParts = Regex.Split(parsedCmd, "_");

                            var ioPort = (Regex.Split(parsedCmd, "_")[1]).ToLower().Replace("ı","i");
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

                            if (cmdParts[0] == "STORE" && cmdParts.Length > 2 && cmdParts[2] == "NOT"){
                                portStatus = portStatus == 1 ? 0 : 1;
                                conditionSucceeded = portStatus == 1;
                            }

                            Console.WriteLine("STORE: " + (conditionSucceeded ? "OK" : "Not OK"));

                            await _apiNodes.PostData<ProcessResultModel>("Results", new ProcessResultModel{
                                CreatedDate = DateTime.Now,
                                ProcessStepId = step.ProcessStepId,
                                StrResult = null,
                                NumResult = portStatus,
                                IsOk = conditionSucceeded,
                                DurationInSeconds = Convert.ToInt32(diffForDuration.TotalSeconds),
                            });
                        }
                        else if (parsedCmd.StartsWith("BREAK")){
                            await RewindToFirstStep();
                        }
                        else if (parsedCmd.StartsWith("STOP")) {
                            HnProcessModel[] liveProcModels = await _apiNodes.GetData<HnProcessModel[]>("Apps/" + _processModel.HnAppId + "/process");
                            try
                            {
                                var procNo = (Regex.Split(parsedCmd, "_")[1]);
                                Console.WriteLine("STOPPED");

                                var liveProcModel = liveProcModels[Convert.ToInt32(procNo)];

                                await _apiNodes.PutData<HnProcessModel>("Process", new HnProcessModel{
                                    ProcStatus = liveProcModel.ProcStatus,
                                    HnProcessId = liveProcModel.HnProcessId,
                                    MustBeStopped = true,
                                });
                            }
                            catch (System.Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                            }
                        }
                        else if (parsedCmd.StartsWith("START")) {
                            HnProcessModel[] liveProcModels = await _apiNodes.GetData<HnProcessModel[]>("Apps/" + _processModel.HnAppId + "/process");
                            try
                            {
                                var procNo = (Regex.Split(parsedCmd, "_")[1]);

                                var liveProcModel = liveProcModels[Convert.ToInt32(procNo)];

                                await _apiNodes.PutData<HnProcessModel>("Process", new HnProcessModel{
                                    ProcStatus = liveProcModel.ProcStatus,
                                    HnProcessId = liveProcModel.HnProcessId,
                                    MustBeStopped = false,
                                });
                            }
                            catch (System.Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                            }
                        }
                        else if (parsedCmd.StartsWith("TOGGLE")){ // [TOGGLE_DO5_500_4000]
                            var cmdArgs = Regex.Split(parsedCmd, "_");
                            int toggleTimeout = 0, toggleDelay = 500, lastSentSignal=0;

                            if (cmdArgs.Length >= 3)
                                toggleDelay = Convert.ToInt32(cmdArgs[2]);
                            if (cmdArgs.Length >= 4)
                                toggleTimeout = Convert.ToInt32(cmdArgs[3]);

                            // Console.WriteLine(toggleDelay + " : " + toggleTimeout);

                            while (toggleTimeout > -1){
                                if (lastSentSignal == 0 && _processModel.ProcStatus == 0)
                                    break;
                                
                                lastSentSignal = lastSentSignal == 0 ? 1 : 0;
                                await SendToDevice(cmdArgs[1].ToLower().Replace("ı","i"), lastSentSignal);

                                toggleTimeout -= toggleDelay;

                                if (toggleTimeout > -1)
                                    await Task.Delay(toggleDelay);
                            }
                        }
                        else if (parsedCmd.StartsWith("DI") || parsedCmd.StartsWith("DO")){
                            var cmdArgs = Regex.Split(parsedCmd, "=");
                            
                            var ioPort = cmdArgs[0].Trim().ToLower().Replace("ı","i");
                            var ioValue = cmdArgs[1].Trim();

                            await SendToDevice(ioPort, Convert.ToInt32(ioValue));
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

                _activeProcessFinished = false;

                try
                {
                    var step = ActiveStep;

                    MakeParallelActions(step, false);

                    if (!step.ResultAction.Contains("[STOP") && !step.ResultAction.Contains("[START"))
                        Console.WriteLine(step.Explanation);
                    if (step.DelayBefore > 0)
                        await Task.Delay(step.DelayBefore.Value);

                    bool conditionSucceeded = false;
                    // DECIDE CONDITION STATUS (WAIT OR PROCEED)
                    if (step.WaitUntilConditionRealized == true){
                        TimeSpan conditionCheckStartTime = DateTime.Now.TimeOfDay;

                        bool conditionTimedOut = false;

                        while (!(await CheckCondition(step.Comparison, _activeStep.ConditionSatisfiedTime))) {
                            await Task.Delay(50);

                            TimeSpan conditionChecktime = DateTime.Now.TimeOfDay;

                            if (step.ConditionRealizeTimeout > 0){
                                TimeSpan diffCondTime = conditionChecktime - conditionCheckStartTime;

                                if (Convert.ToInt32(diffCondTime.TotalMilliseconds) >= step.ConditionRealizeTimeout){
                                    conditionTimedOut = true;
                                    break;
                                }
                            }

                            if (_processModel.MustBeStopped){
                                break;
                            }
                        }

                        if (!conditionTimedOut){
                            conditionSucceeded = true;
                        }
                    }
                    else {
                        conditionSucceeded = await CheckCondition(step.Comparison, _activeStep.ConditionSatisfiedTime);
                    }

                    _activeProcessFinished = true;

                    if (_processModel.MustBeStopped)
                    {
                        ActiveStep = null;
                        _processModel.ProcStatus = 0;
                        continue;
                    }

                    // FIRE RESULT ACTION
                    if (conditionSucceeded)
                        await MakeResultActions(step, true, false);
                    else
                        await MakeResultActions(step, false, true);

                    if (step.DelayAfter > 0)
                        await Task.Delay(step.DelayAfter.Value);

                    await ProceedToNextStep();
                }
                catch (System.Exception ex)
                {
                    Console.WriteLine(ex.Message);
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
                                _processModel.MustBeStopped = false;
                                await _apiNodes.PutData<HnProcessModel>("Process", _processModel);
                            }
                        }
                    }
                    else
                        _processModel.ProcStatus = 1;
                    

                    var liveProcModel = await _apiNodes.GetData<HnProcessModel>("Process/" + _processModel.HnProcessId);
                    if (liveProcModel != null){
                        if (liveProcModel.MustBeStopped == true){
                            _processModel.ProcStatus = 0;
                            _processModel.MustBeStopped = true;
                            //await _apiNodes.PutData<HnProcessModel>("Process", liveProcModel);

                            if (_activeStep != null)
                                _activeStep = null;
                        }
                        else{
                            _processModel.MustBeStopped = false;
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