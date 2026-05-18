using System;

namespace EnergyConsumptionService
{
    public class TransferEventArgs : EventArgs
    {
        public string Message { get; private set; }
        public string CountryCode { get; private set; }
        public int SampleCount { get; private set; }

        public int Hour { get; private set; }
        public double LoadFactor { get; private set; }

        public TransferEventArgs(string message, string countryCode, int sampleCount)
        {
            Message = message;
            CountryCode = countryCode;
            SampleCount = sampleCount;

            Hour = -1;
            LoadFactor = double.NaN;
        }

        public TransferEventArgs(string message, string countryCode, int sampleCount, int hour, double loadFactor)
        {
            Message = message;
            CountryCode = countryCode;
            SampleCount = sampleCount;
            Hour = hour;
            LoadFactor = loadFactor;
        }
    }
}