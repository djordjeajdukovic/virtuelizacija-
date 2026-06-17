using System.Runtime.Serialization;

namespace Common
{
    [DataContract]
    public class BatchResult
    {
        [DataMember]
        public int BatchNumber { get; set; }

        [DataMember]
        public int ReceivedCount { get; set; }

        [DataMember]
        public int AcceptedCount { get; set; }

        [DataMember]
        public int RejectedCount { get; set; }

        [DataMember]
        public string Status { get; set; }

        [DataMember]
        public string Message { get; set; }
    }
}
