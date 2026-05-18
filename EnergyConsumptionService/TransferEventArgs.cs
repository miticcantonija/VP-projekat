using System;

namespace EnergyConsumptionService
{
    public class TransferEventArgs : EventArgs
    {
        public string Message { get; }
        public string CountryCode { get; }
        public int SampleCount { get; }

        public TransferEventArgs(string message, string countryCode, int sampleCount)
        {
            Message = message;
            CountryCode = countryCode;
            SampleCount = sampleCount;
        }
    }
}