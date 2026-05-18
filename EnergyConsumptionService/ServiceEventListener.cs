using System;

namespace EnergyConsumptionService
{
    public class ServiceEventListener
    {
        public void OnTransferStartedHandler(object sender, TransferEventArgs e)
        {
            Console.WriteLine("[EVENT] Transfer started");
            Console.WriteLine("[EVENT] " + e.Message);
        }

        public void OnBatchReceivedHandler(object sender, TransferEventArgs e)
        {
            Console.WriteLine("[EVENT] Batch received");
            Console.WriteLine("[EVENT] " + e.Message);
        }

        public void OnTransferCompletedHandler(object sender, TransferEventArgs e)
        {
            Console.WriteLine("[EVENT] Transfer completed");
            Console.WriteLine("[EVENT] " + e.Message);
        }

        public void OnWarningRaisedHandler(object sender, TransferEventArgs e)
        {
            Console.WriteLine("[WARNING EVENT] " + e.Message);

            if (e.Hour >= 0 && !double.IsNaN(e.LoadFactor))
            {
                Console.WriteLine("[WARNING EVENT] CountryCode: " + e.CountryCode);
                Console.WriteLine("[WARNING EVENT] Hour: " + e.Hour);
                Console.WriteLine("[WARNING EVENT] LoadFactor: " + e.LoadFactor);
            }
        }
    }
}