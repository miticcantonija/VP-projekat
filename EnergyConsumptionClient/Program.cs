using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.ServiceModel;
using EnergyConsumptionService.Contracts;


namespace EnergyConsumptionClient
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string countryCode = ConfigurationManager.AppSettings["CountryCode"];
            string selectedDate = ConfigurationManager.AppSettings["SelectedDate"];
            string csvPath = ConfigurationManager.AppSettings["CsvPath"];
            int batchSize = int.Parse(ConfigurationManager.AppSettings["BatchSize"]);
            bool simulateTransferBreak = bool.Parse(ConfigurationManager.AppSettings["SimulateTransferBreak"]);
            int breakAfterBatches = int.Parse(ConfigurationManager.AppSettings["BreakAfterBatches"]);

            List<LoadSample> samples = ReadCsv(csvPath, countryCode, selectedDate);

            SessionMeta meta = new SessionMeta
            {
                CountryCode = countryCode,
                Date = selectedDate,
                SourceFileName = Path.GetFileName(csvPath),
                TotalSamples = samples.Count,
                BatchSize = batchSize
            };

            ChannelFactory<IEnergyConsumptionService> factory =
                new ChannelFactory<IEnergyConsumptionService>("EnergyConsumptionEndpoint");

            IEnergyConsumptionService proxy = factory.CreateChannel();
            IClientChannel clientChannel = (IClientChannel)proxy;

            try
            {
                proxy.StartSession(meta);

                int sentBatches = 0;

                for (int i = 0; i < samples.Count; i += batchSize)
                {
                    List<LoadSample> batch = samples
                        .Skip(i)
                        .Take(batchSize)
                        .ToList();

                    proxy.PushBatch(batch);
                    sentBatches++;

                    if (simulateTransferBreak && sentBatches >= breakAfterBatches)
                    {
                        throw new Exception("Simuliran prekid prenosa nakon batch-a broj: " + sentBatches);
                    }
                }

                proxy.EndSession();

                Console.WriteLine("Klijent je završio slanje.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Greška tokom slanja: " + ex.Message);
            }
            finally
            {
                if (clientChannel.State == CommunicationState.Faulted)
                {
                    clientChannel.Abort();
                    Console.WriteLine("WCF proxy je abortovan.");
                }
                else
                {
                    clientChannel.Close();
                    Console.WriteLine("WCF proxy je zatvoren.");
                }

                if (factory.State == CommunicationState.Faulted)
                {
                    factory.Abort();
                    Console.WriteLine("ChannelFactory je abortovan.");
                }
                else
                {
                    factory.Close();
                    Console.WriteLine("ChannelFactory je zatvoren.");
                }
            }
            Console.ReadLine();
        }

        static List<LoadSample> ReadCsv(string path, string countryCode, string selectedDate)
        {
            List<LoadSample> result = new List<LoadSample>();
            string rejectedPath = Path.Combine(
     AppDomain.CurrentDomain.BaseDirectory,
     "rejected_client.csv"
 );

            string actualColumn = countryCode + "_load_actual_entsoe_transparency";
            string forecastColumn = countryCode + "_load_forecast_entsoe_transparency";

            using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (StreamReader reader = new StreamReader(fileStream))
            using (FileStream rejectedFileStream = new FileStream(rejectedPath, FileMode.Create, FileAccess.Write))
            using (StreamWriter rejectedWriter = new StreamWriter(rejectedFileStream))
            {
                rejectedWriter.WriteLine("RowIndex;Reason;RawLine");

                string headerLine = reader.ReadLine();
                string[] headers = headerLine.Split(',');

                int utcIndex = Array.IndexOf(headers, "utc_timestamp");
                int localIndex = Array.IndexOf(headers, "cet_cest_timestamp");
                int actualIndex = Array.IndexOf(headers, actualColumn);
                int forecastIndex = Array.IndexOf(headers, forecastColumn);

                if (utcIndex == -1 || localIndex == -1 || actualIndex == -1 || forecastIndex == -1)
                {
                    throw new FaultException<DataFormatFault>(
                        new DataFormatFault
                        {
                            Message = "CSV fajl ne sadrži sve potrebne kolone za zemlju: " + countryCode,
                            RowIndex = 0
                        });
                }

                string line;
                int rowIndex = 1;
                double cumulativeMWh = 0;

                while ((line = reader.ReadLine()) != null)
                {
                    rowIndex++;

                    string[] parts = line.Split(',');

                    int maxRequiredIndex = Math.Max(Math.Max(utcIndex, localIndex), Math.Max(actualIndex, forecastIndex));

                    if (parts.Length <= maxRequiredIndex)
                    {
                        rejectedWriter.WriteLine(rowIndex + ";Nedovoljan broj kolona;" + line);
                        continue;
                    }

                    DateTime timestampUtc;
                    DateTime timestampLocal;

                    bool validUtc = DateTime.TryParse(
                        parts[utcIndex],
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out timestampUtc);

                    bool validLocal = DateTime.TryParse(
                        parts[localIndex],
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out timestampLocal);

                    if (!validUtc || !validLocal)
                    {
                        rejectedWriter.WriteLine(rowIndex + ";Nevalidan datum;" + line);
                        continue;
                    }

                    if (timestampLocal.ToString("yyyy-MM-dd") != selectedDate)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(parts[actualIndex]) ||
                        string.IsNullOrWhiteSpace(parts[forecastIndex]))
                    {
                        rejectedWriter.WriteLine(rowIndex + ";Prazno actual ili forecast polje;" + line);
                        continue;
                    }

                    double actualMW;
                    double forecastMW;

                    bool validActual = double.TryParse(
                        parts[actualIndex],
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out actualMW);

                    bool validForecast = double.TryParse(
                        parts[forecastIndex],
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out forecastMW);

                    if (!validActual || !validForecast)
                    {
                        rejectedWriter.WriteLine(rowIndex + ";Nevalidan broj;" + line);
                        continue;
                    }

                    if (double.IsNaN(actualMW) || double.IsNaN(forecastMW))
                    {
                        rejectedWriter.WriteLine(rowIndex + ";NaN vrednost;" + line);
                        continue;
                    }

                    if (actualMW < 0 || forecastMW < 0)
                    {
                        rejectedWriter.WriteLine(rowIndex + ";Negativna actual ili forecast vrednost;" + line);
                        continue;
                    }

                    double energyMWh = actualMW * 0.25;
                    cumulativeMWh += energyMWh;

                    LoadSample sample = new LoadSample
                    {
                        TimestampUtc = timestampUtc,
                        TimestampLocal = timestampLocal,
                        ActualMW = actualMW,
                        ForecastMW = forecastMW,
                        CumulativeMWh = cumulativeMWh,
                        CountryCode = countryCode,
                        RowIndex = rowIndex
                    };

                    result.Add(sample);
                }
            }

            return result;
        }
    }
}