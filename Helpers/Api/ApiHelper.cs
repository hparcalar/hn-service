using System;
using System.Net.Http;
using Newtonsoft.Json;
using HnService.Models.Application;
using System.Threading.Tasks;
using System.Text;

namespace HnService.Helpers.Api{
    public class ApiHelper {
        HttpClient _httpClient;
        string baseUrl;

        public ApiHelper(string baseUrl){
            this.baseUrl = baseUrl;
            _httpClient = new HttpClient();
        }

        public void AddHeader(string header, string value){
            if (_httpClient != null){
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header, value);
            }
        }

        public async Task<T> GetData<T>(string target){
            var httpResponse = await _httpClient.GetAsync($"{baseUrl}{target}");

            // if (!httpResponse.IsSuccessStatusCode)
            // {
            //     throw new Exception("Cannot retrieve data");
            // }

            var content = await httpResponse.Content.ReadAsStringAsync();
            var returnItem = JsonConvert.DeserializeObject<T>(content);

            return returnItem;
        }

        public async Task PostData<T>(string target, T data){
            await _httpClient.PostAsync($"{baseUrl}{target}", 
                new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json"));
        }

        public async Task PutData<T>(string target, T data){
            var putStr = JsonConvert.SerializeObject(data);
            putStr = putStr.Replace("Do","do");

            var xx = await _httpClient.PutAsync($"{baseUrl}{target}", 
                new StringContent(putStr, Encoding.UTF8, "application/json"));
            // Console.WriteLine(xx);
        }
    }
}