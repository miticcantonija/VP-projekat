using System.Collections.Generic;
using System.ServiceModel;

namespace EnergyConsumptionService.Contracts
{
    [ServiceContract]
    public interface IEnergyConsumptionService
    {
        [OperationContract]
        [FaultContract(typeof(ValidationFault))]
        [FaultContract(typeof(DataFormatFault))]
        void StartSession(SessionMeta meta);

        [OperationContract]
        [FaultContract(typeof(ValidationFault))]
        [FaultContract(typeof(DataFormatFault))]
        void PushBatch(List<LoadSample> samples);

        [OperationContract]
        [FaultContract(typeof(ValidationFault))]
        [FaultContract(typeof(DataFormatFault))]
        void EndSession();
    }
}