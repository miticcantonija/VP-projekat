using System;
using System.Collections.Generic;
using EnergyConsumptionService.Contracts;
using System.ServiceModel;

namespace EnergyConsumptionService
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class EnergyConsumptionServiceImpl : IEnergyConsumptionService
    {
        private SessionMeta currentSession;
        private int receivedSamples = 0;
        private double lastCumulativeMWh = -1;

        public void StartSession(SessionMeta meta)
        {
            currentSession = meta;
            receivedSamples = 0;
            lastCumulativeMWh = -1;

            Console.WriteLine("=== START SESSION ===");
            Console.WriteLine("Country: " + meta.CountryCode);
            Console.WriteLine("Date: " + meta.Date);
            Console.WriteLine("Source file: " + meta.SourceFileName);
            Console.WriteLine("Total samples: " + meta.TotalSamples);
            Console.WriteLine("Batch size: " + meta.BatchSize);
        }

        public void PushBatch(List<LoadSample> samples)
        {
            if (samples == null || samples.Count == 0)
            {
                throw new FaultException<DataFormatFault>(
                    new DataFormatFault
                    {
                        Message = "Batch je prazan.",
                        RowIndex = -1
                    });
            }

            foreach (LoadSample sample in samples)
            {
                if (sample.TimestampUtc == DateTime.MinValue)
                {
                    throw new FaultException<ValidationFault>(
                        new ValidationFault
                        {
                            Message = "TimestampUtc nije validan.",
                            Field = "TimestampUtc"
                        });
                }

                if (sample.TimestampLocal == DateTime.MinValue)
                {
                    throw new FaultException<ValidationFault>(
                        new ValidationFault
                        {
                            Message = "TimestampLocal nije validan.",
                            Field = "TimestampLocal"
                        });
                }

                if (sample.ActualMW < 0)
                {
                    throw new FaultException<ValidationFault>(
                        new ValidationFault
                        {
                            Message = "ActualMW ne sme biti negativan.",
                            Field = "ActualMW"
                        });
                }

                if (sample.ForecastMW < 0)
                {
                    throw new FaultException<ValidationFault>(
                        new ValidationFault
                        {
                            Message = "ForecastMW ne sme biti negativan.",
                            Field = "ForecastMW"
                        });
                }

                if (lastCumulativeMWh != -1 && sample.CumulativeMWh < lastCumulativeMWh)
                {
                    throw new FaultException<ValidationFault>(
                        new ValidationFault
                        {
                            Message = "CumulativeMWh mora monotono da raste.",
                            Field = "CumulativeMWh"
                        });
                }

                lastCumulativeMWh = sample.CumulativeMWh;
            }

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