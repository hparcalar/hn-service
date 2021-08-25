using System;

namespace HnService.Models.Application {
    public class HnSession {
        private static HnSession _instance;

        private HnSession(){

        }

        public static HnSession GetInstance(){
            if (_instance == null)
                _instance = new HnSession();

            return _instance;
        }

        #region SESSION PROPERTIES
        public string NodesApiUrl { get; set; }
        public string DeviceApiUrl { get; set; }
        #endregion
    }
}