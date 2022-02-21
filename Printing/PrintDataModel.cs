using System;

namespace HnService.Printing{
    public class PrintDataModel{
        public int Id { get; set; }
        public string Barcode { get; set; }
        public DateTime ProductionDate { get; set; }
        public string ModelCode { get; set; }
        public string ModelName { get; set; }
    }
}