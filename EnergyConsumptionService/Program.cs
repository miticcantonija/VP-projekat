using System;
using System.ServiceModel;

namespace EnergyConsumptionService
{
    internal class Program
    {
        static void Main(string[] args)
        {
            ServiceHost host = null;

            try
            {
                host = new ServiceHost(typeof(EnergyConsumptionServiceImpl));

                host.Open();

                Console.WriteLine("WCF servis je pokrenut.");
                Console.WriteLine("Pritisni ENTER za kraj...");
                Console.ReadLine();

                host.Close();
                Console.WriteLine("WCF servis je pravilno zatvoren.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Greška u radu servisa: " + ex.Message);

                if (host != null)
                {
                    host.Abort();
                    Console.WriteLine("WCF servis je prekinut preko Abort().");
                }
            }
            finally
            {
                if (host != null && host.State != CommunicationState.Closed)
                {
                    host.Abort();
                    Console.WriteLine("Resursi ServiceHost-a su oslobođeni.");
                }
            }
        }
    }
}