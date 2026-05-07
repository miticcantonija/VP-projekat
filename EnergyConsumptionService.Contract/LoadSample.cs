using System;
using System.Runtime.Serialization;

namespace EnergyConsumptionService.Contracts
{
    [DataContract]
    public class LoadSample
    {
        [DataMember]
        public DateTime TimestampUtc { get; set; }

        [DataMember]
        public DateTime TimestampLocal { get; set; }

        [DataMember]
        public double ActualMW { get; set; }

        [DataMember]
        public double ForecastMW { get; set; }

        [DataMember]
        public double CumulativeMWh { get; set; }

        [DataMember]
        public string CountryCode { get; set; }

        [DataMember]
        public int RowIndex { get; set; }
    }
}