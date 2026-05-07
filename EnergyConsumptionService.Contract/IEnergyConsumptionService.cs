using System.Collections.Generic;
using System.ServiceModel;

namespace EnergyConsumptionService.Contracts
{
    [ServiceContract]
    public interface IEnergyConsumptionService
    {
        [OperationContract]
        void StartSession(SessionMeta meta);

        [OperationContract]
        void PushBatch(List<LoadSample> samples);

        [OperationContract]
        void EndSession();
    }
}