using System;
using Common;

namespace Service
{
    public class TransferEventArgs : EventArgs
    {
        public string TurbineId { get; set; }
        public string SourceFileName { get; set; }
        public int AcceptedCount { get; set; }
        public int RejectedCount { get; set; }
    }

    public class SampleReceivedEventArgs : EventArgs
    {
        public WindTurbineSample Sample { get; set; }
    }

    public class BatchReceivedEventArgs : EventArgs
    {
        public int BatchNumber { get; set; }
        public int ReceivedCount { get; set; }
        public int AcceptedCount { get; set; }
        public int RejectedCount { get; set; }
    }

    public class WarningEventArgs : EventArgs
    {
        public string WarningType { get; set; }
        public string Message { get; set; }
        public int RowIndex { get; set; }
    }
}
