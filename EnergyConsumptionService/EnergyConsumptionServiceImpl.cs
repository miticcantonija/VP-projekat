using EnergyConsumptionService.Contracts;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.ServiceModel;
using System.Configuration;

namespace EnergyConsumptionService
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class EnergyConsumptionServiceImpl : IEnergyConsumptionService
    {
        public delegate void TransferEventHandler(object sender, TransferEventArgs e);

        public event TransferEventHandler OnTransferStarted;
        public event TransferEventHandler OnBatchReceived;
        public event TransferEventHandler OnTransferCompleted;
        public event TransferEventHandler OnWarningRaised;

        private double loadFactorMin;
        private double flatlineEpsilon;
        private int flatlineWindowSamples;
        private double spikeDeltaMW;

        private SessionMeta currentSession;
        private int receivedSamples = 0;
        private double lastCumulativeMWh = -1;

        private string sessionFilePath;
        private string rejectsFilePath;

        public EnergyConsumptionServiceImpl()
        {
            LoadConfigurationValues();
        }

        private void LoadConfigurationValues()
        {
            double.TryParse(ConfigurationManager.AppSettings["LoadFactorMin"], NumberStyles.Any, CultureInfo.InvariantCulture, out loadFactorMin);
            double.TryParse(ConfigurationManager.AppSettings["FlatlineEpsilon"], NumberStyles.Any, CultureInfo.InvariantCulture, out flatlineEpsilon);
            int.TryParse(ConfigurationManager.AppSettings["FlatlineWindowSamples"], out flatlineWindowSamples);
            double.TryParse(ConfigurationManager.AppSettings["SpikeDeltaMW"], NumberStyles.Any, CultureInfo.InvariantCulture, out spikeDeltaMW);

            Console.WriteLine("=== CONFIG THRESHOLDS ===");
            Console.WriteLine("LoadFactorMin: " + loadFactorMin);
            Console.WriteLine("FlatlineEpsilon: " + flatlineEpsilon);
            Console.WriteLine("FlatlineWindowSamples: " + flatlineWindowSamples);
            Console.WriteLine("SpikeDeltaMW: " + spikeDeltaMW);
        }

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

            if (OnTransferStarted != null)
            {
                OnTransferStarted(
                    this,
                    new TransferEventArgs(
                        "Transfer started for country " + meta.CountryCode + ", date " + meta.Date,
                        meta.CountryCode,
                        0
                    )
                );
            }
        }

        public string PushBatch(List<LoadSample> samples)
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

                CheckLoadFactor(sample);

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

            if (OnBatchReceived != null)
            {
                OnBatchReceived(
                    this,
                    new TransferEventArgs(
                        "Batch received. Samples in batch: " + samples.Count + ". Total received: " + receivedSamples,
                        currentSession.CountryCode,
                        samples.Count
                    )
                );
            }

            return "Blok primljen. Broj uzoraka u bloku: " + samples.Count +
       ". Ukupno primljeno: " + receivedSamples;
        }

        public string EndSession()
        {
            Console.WriteLine("=== END SESSION ===");

            if (currentSession != null)
            {
                Console.WriteLine("Zavrsen prenos za: " + currentSession.CountryCode);
                Console.WriteLine("Ocekivano uzoraka: " + currentSession.TotalSamples);
                Console.WriteLine("Primljeno uzoraka: " + receivedSamples);

                if (receivedSamples != currentSession.TotalSamples)
                {
                    Console.WriteLine("UPOZORENJE: broj uzoraka nije isti.");

                    if (OnWarningRaised != null)
                    {
                        OnWarningRaised(
                            this,
                            new TransferEventArgs(
                                "Expected samples: " + currentSession.TotalSamples + ", received samples: " + receivedSamples,
                                currentSession.CountryCode,
                                receivedSamples
                            )
                        );
                    }
                }
            }

            if (currentSession != null && OnTransferCompleted != null)
            {
                OnTransferCompleted(
                    this,
                    new TransferEventArgs(
                        "Transfer completed. Total received samples: " + receivedSamples,
                        currentSession.CountryCode,
                        receivedSamples
                    )
                );
            }

            return "Prenos zavrsen. Ukupno primljeno uzoraka: " + receivedSamples;
        }

        private void CheckLoadFactor(LoadSample sample)
        {
            if (sample == null)
            {
                return;
            }

            if (double.IsNaN(sample.ActualMW) || double.IsNaN(sample.ForecastMW))
            {
                return;
            }

            if (sample.ForecastMW == 0)
            {
                return;
            }

            double loadFactor = sample.ActualMW / sample.ForecastMW;

            if (loadFactor < loadFactorMin)
            {
                if (OnWarningRaised != null)
                {
                    OnWarningRaised(
                        this,
                        new TransferEventArgs(
                            "LowLoadFactorWarning: LoadFactor is below configured minimum. LoadFactor=" +
                            loadFactor.ToString("F3", CultureInfo.InvariantCulture) +
                            ", LoadFactorMin=" +
                            loadFactorMin.ToString("F3", CultureInfo.InvariantCulture),
                            sample.CountryCode,
                            1,
                            sample.TimestampLocal.Hour,
                            loadFactor
                        )
                    );
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