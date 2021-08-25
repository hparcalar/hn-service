using System;

namespace HnService.Models.DeviceResults {
    public class DigitalIOResult {
        public int slot { get; set; }
        public DigitalIOArray io { get; set; }
    }
}