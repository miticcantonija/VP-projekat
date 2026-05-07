using System.Runtime.Serialization;

namespace EnergyConsumptionService.Contracts
{
    [DataContract]
    public class SessionMeta
    {
        [DataMember]
        public string CountryCode { get; set; }

        [DataMember]
        public string Date { get; set; }

        [DataMember]
        public string SourceFileName { get; set; }

        [DataMember]
        public int TotalSamples { get; set; }

        [DataMember]
        public int BatchSize { get; set; }
    }
}