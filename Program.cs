using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using HnService.Models.Application;
using HnService.Models;
using HnService.Helpers.Api;
using System.Threading.Tasks;
using HnService.Services;
using HnService.Models.DeviceResults;

namespace HnService
{
    class Program
    {
        static async Task Main(string[] args)
        {
            BuildConfiguration();

            Console.WriteLine("-- HEKA Nodes Service App --");

            bool isApiAlive = false;

            while (!isApiAlive){
                try
                {
                    // GET ALL APPS
                    ApiHelper api = new ApiHelper(HnSession.GetInstance().NodesApiUrl);
                    var result = await api.GetData<HnAppModel[]>("Apps");
                    
                    // START LISTENERS FOR APPS
                    foreach (var app in result)
                    {
                        var procList = await api.GetData<HnProcessModel[]>("Apps/" + app.HnAppId + "/process");
                        foreach (var proc in procList)
                        {
                            DeviceListener dvcManager = new DeviceListener(proc);
                            dvcManager.Start();
                        }
                    }

                    isApiAlive = true;
                }
                catch (System.Exception)
                {
                    Console.WriteLine("Trying to access HN-API service...");
                    await Task.Delay(2000);
                }
            }

            // WAIT FOR FORCED FINISH
            Console.WriteLine("-- ALL PROCESSES ARE LAUNCHED SUCCESSFULLY --");
            while (true)
                await Task.Delay(100);
        }

        static void BuildConfiguration(){
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var builder = new ConfigurationBuilder()
                .AddJsonFile($"appSettings.json", true, true)
                .AddEnvironmentVariables();

            var config = builder.Build();

            HnSession.GetInstance().NodesApiUrl = config.GetSection("NodesApiUrl").Value;
            HnSession.GetInstance().DeviceApiUrl = config.GetSection("DeviceApiUrl").Value;
        }
    }
}
