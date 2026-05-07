using System;
using System.ServiceModel;

namespace EnergyConsumptionService
{
    internal class Program
    {
        static void Main(string[] args)
        {
            ServiceHost host = new ServiceHost(typeof(EnergyConsumptionServiceImpl));

            host.Open();

            Console.WriteLine("WCF servis je pokrenut.");
            Console.WriteLine("Pritisni ENTER za kraj...");
            Console.ReadLine();

            host.Close();
        }
    }
}