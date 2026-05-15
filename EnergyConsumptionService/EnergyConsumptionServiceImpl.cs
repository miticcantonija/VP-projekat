using EnergyConsumptionService.Contracts;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.ServiceModel;

namespace EnergyConsumptionService
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class EnergyConsumptionServiceImpl : IEnergyConsumptionService
    {
        private SessionMeta currentSession;
        private int receivedSamples = 0;
        private double lastCumulativeMWh = -1;

        private string sessionFilePath;
        private string rejectsFilePath;

        public void StartSession(SessionMeta meta)
        {
            if (meta == null)
            {
                throw new FaultException<DataFormatFault>(
                    new DataFormatFault
                    {
                        Message = "Meta podaci nisu poslati."
                    });
            }

            if (string.IsNullOrWhiteSpace(meta.CountryCode))
            {
                throw new FaultException<ValidationFault>(
                    new ValidationFault
                    {
                        Message = "CountryCode ne sme biti prazan.",
                        Field = "CountryCode"
                    });
            }

            if (string.IsNullOrWhiteSpace(meta.Date))
            {
                throw new FaultException<ValidationFault>(
                    new ValidationFault
                    {
                        Message = "Date ne sme biti prazan.",
                        Field = "Date"
                    });
            }

            if (string.IsNullOrWhiteSpace(meta.SourceFileName))
            {
                throw new FaultException<ValidationFault>(
                    new ValidationFault
                    {
                        Message = "SourceFileName ne sme biti prazan.",
                        Field = "SourceFileName"
                    });
            }

            if (meta.TotalSamples <= 0)
            {
                throw new FaultException<ValidationFault>(
                    new ValidationFault
                    {
                        Message = "TotalSamples mora biti veći od 0.",
                        Field = "TotalSamples"
                    });
            }

            if (meta.BatchSize <= 0)
            {
                throw new FaultException<ValidationFault>(
                    new ValidationFault
                    {
                        Message = "BatchSize mora biti veći od 0.",
                        Field = "BatchSize"
                    });
            }

            DateTime parsedDate;

            if (!DateTime.TryParse(meta.Date, out parsedDate))
            {
                throw new FaultException<ValidationFault>(
                    new ValidationFault
                    {
                        Message = "Date nije validan datum.",
                        Field = "Date"
                    });
            }

            currentSession = meta;
            receivedSamples = 0;
            lastCumulativeMWh = -1;

            string folderPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Data",
                meta.CountryCode,
                meta.Date
            );

            Directory.CreateDirectory(folderPath);

            sessionFilePath = Path.Combine(folderPath, "session.csv");
            rejectsFilePath = Path.Combine(folderPath, "rejects.csv");

            using (StreamWriter writer = new StreamWriter(sessionFilePath, false))
            {
                writer.WriteLine("RowIndex;TimestampUtc;TimestampLocal;ActualMW;ForecastMW;CumulativeMWh;CountryCode");
            }

            using (StreamWriter writer = new StreamWriter(rejectsFilePath, false))
            {
                writer.WriteLine("RowIndex;Reason");
            }

            Console.WriteLine("=== START SESSION ===");
            Console.WriteLine("Country: " + meta.CountryCode);
            Console.WriteLine("Date: " + meta.Date);
            Console.WriteLine("Source file: " + meta.SourceFileName);
            Console.WriteLine("Total samples: " + meta.TotalSamples);
            Console.WriteLine("Batch size: " + meta.BatchSize);
        }

        public void PushBatch(List<LoadSample> samples)
        {
            if (currentSession == null)
            {
                throw new FaultException<DataFormatFault>(
                    new DataFormatFault
                    {
                        Message = "Sesija nije pokrenuta.",
                        RowIndex = -1
                    });
            }

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
                    WriteServerReject(sample.RowIndex, "TimestampUtc nije validan");

                    throw new FaultException<ValidationFault>(
                        new ValidationFault
                        {
                            Message = "TimestampUtc nije validan.",
                            Field = "TimestampUtc"
                        });
                }

                if (sample.TimestampLocal == DateTime.MinValue)
                {
                    WriteServerReject(sample.RowIndex, "TimestampLocal nije validan");

                    throw new FaultException<ValidationFault>(
                        new ValidationFault
                        {
                            Message = "TimestampLocal nije validan.",
                            Field = "TimestampLocal"
                        });
                }

                if (sample.ActualMW < 0)
                {
                    WriteServerReject(sample.RowIndex, "ActualMW negativan");

                    throw new FaultException<ValidationFault>(
                        new ValidationFault
                        {
                            Message = "ActualMW ne sme biti negativan.",
                            Field = "ActualMW"
                        });
                }

                if (sample.ForecastMW < 0)
                {
                    WriteServerReject(sample.RowIndex, "ForecastMW negativan");

                    throw new FaultException<ValidationFault>(
                        new ValidationFault
                        {
                            Message = "ForecastMW ne sme biti negativan.",
                            Field = "ForecastMW"
                        });
                }

                if (lastCumulativeMWh != -1 &&
                    sample.CumulativeMWh < lastCumulativeMWh)
                {
                    WriteServerReject(sample.RowIndex, "CumulativeMWh nije monoton");

                    throw new FaultException<ValidationFault>(
                        new ValidationFault
                        {
                            Message = "CumulativeMWh mora monotono da raste.",
                            Field = "CumulativeMWh"
                        });
                }

                if (sample.CountryCode != currentSession.CountryCode)
                {
                    WriteServerReject(sample.RowIndex, "Pogrešna država");

                    throw new FaultException<ValidationFault>(
                        new ValidationFault
                        {
                            Message = "Uzorak ne pripada izabranoj zemlji.",
                            Field = "CountryCode"
                        });
                }

                if (sample.TimestampLocal.Date !=
                    DateTime.Parse(currentSession.Date).Date)
                {
                    WriteServerReject(sample.RowIndex, "Pogrešan datum");

                    throw new FaultException<ValidationFault>(
                        new ValidationFault
                        {
                            Message = "Uzorak ne pripada izabranom danu.",
                            Field = "TimestampLocal"
                        });
                }

                using (StreamWriter writer = new StreamWriter(sessionFilePath, true))
                {
                    writer.WriteLine(
                        sample.RowIndex + ";" +
                        sample.TimestampUtc.ToString("o") + ";" +
                        sample.TimestampLocal.ToString("o") + ";" +
                        sample.ActualMW.ToString(CultureInfo.InvariantCulture) + ";" +
                        sample.ForecastMW.ToString(CultureInfo.InvariantCulture) + ";" +
                        sample.CumulativeMWh.ToString(CultureInfo.InvariantCulture) + ";" +
                        sample.CountryCode
                    );
                }

                lastCumulativeMWh = sample.CumulativeMWh;
            }

            receivedSamples += samples.Count;

            Console.WriteLine("Primljen blok uzoraka: " + samples.Count);
            Console.WriteLine("Ukupno primljeno: " + receivedSamples);
        }

        public void EndSession()
        {
            Console.WriteLine("=== END SESSION ===");

            if (currentSession != null)
            {
                Console.WriteLine("Završen prenos za: " + currentSession.CountryCode);
                Console.WriteLine("Očekivano uzoraka: " + currentSession.TotalSamples);
                Console.WriteLine("Primljeno uzoraka: " + receivedSamples);

                if (receivedSamples != currentSession.TotalSamples)
                {
                    Console.WriteLine("UPOZORENJE: broj uzoraka nije isti.");
                }
            }
        }

        private void WriteServerReject(int rowIndex, string reason)
        {
            if (string.IsNullOrWhiteSpace(rejectsFilePath))
                return;

            using (StreamWriter writer = new StreamWriter(rejectsFilePath, true))
            {
                writer.WriteLine(rowIndex + ";" + reason);
            }
        }
    }
}