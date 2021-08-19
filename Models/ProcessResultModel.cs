using System;
namespace HnService.Models {
    public class ProcessResultModel {
        public int Id { get; set; }
            public int ProcessStepId { get; set; }
            public string StrResult { get; set; }
            public float? NumResult { get; set; }
            public int DurationInSeconds { get; set; }
            public bool? IsOk { get; set; }
            public DateTime CreatedDate { get; set; }
            public ProcessStepModel ProcessStep { get; set; }
    }
}