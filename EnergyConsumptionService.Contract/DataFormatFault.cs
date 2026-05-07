using System.Runtime.Serialization;

namespace EnergyConsumptionService.Contracts
{
    [DataContract]
    public class DataFormatFault
    {
        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public int RowIndex { get; set; }
    }
}