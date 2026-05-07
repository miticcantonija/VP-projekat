using System;
using System.Collections.Generic;
using EnergyConsumptionService.Contracts;

namespace EnergyConsumptionService
{
    public class EnergyConsumptionServiceImpl : IEnergyConsumptionService
    {
        private SessionMeta currentSession;
        private int receivedSamples = 0;

        public void StartSession(SessionMeta meta)
        {
            currentSession = meta;
            receivedSamples = 0;

            Console.WriteLine("=== START SESSION ===");
            Console.WriteLine("Country: " + meta.CountryCode);
            Console.WriteLine("Date: " + meta.Date);
            Console.WriteLine("Source file: " + meta.SourceFileName);
            Console.WriteLine("Total samples: " + meta.TotalSamples);
            Console.WriteLine("Batch size: " + meta.BatchSize);
        }

        public void PushBatch(List<LoadSample> samples)
        {
            receivedSamples += samples.Count;

            Console.WriteLine("Primljen blok uzoraka: " + samples.Count);
            Console.WriteLine("Ukupno primljeno: " + receivedSamples);

            foreach (LoadSample sample in samples)
            {
                Console.WriteLine(
                    sample.RowIndex + " | " +
                    sample.TimestampUtc + " | " +
                    sample.CountryCode + " | Actual: " +
                    sample.ActualMW + " | Forecast: " +
                    sample.ForecastMW + " | Cumulative: " +
                    sample.CumulativeMWh
                );
            }
        }

        public void EndSession()
        {
            Console.WriteLine("=== END SESSION ===");

            if (currentSession != null)
            {
                Console.WriteLine("Završen prenos za: " + currentSession.CountryCode);
                Console.WriteLine("Očekivano uzoraka: " + currentSession.TotalSamples);
                Console.WriteLine("Primljeno uzoraka: " + receivedSamples);
            }
        }
    }
}