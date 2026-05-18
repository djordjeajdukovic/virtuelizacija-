using System;
using System.Runtime.Serialization;

namespace Common
{
    [DataContract]
    public class SessionMeta
    {
        [DataMember]
        public string TurbineId { get; set; }

        [DataMember]
        public string SourceFileName { get; set; }

        [DataMember]
        public DateTime StartedAt { get; set; }
    }
}
