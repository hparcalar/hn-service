namespace HnService.Models {
    public class HnProcessModel {
        public int HnProcessId { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
        public int HnAppId { get; set; }
        public int DelayBefore { get; set; }
        public int DelayAfter { get; set; }
        public bool CanRepeat { get; set; } = false;
        public int ProcStatus { get; set; } = 0;
        public string LiveCondition { get; set; }
        public ProcessStepModel[] Steps { get; set; }
    }
}