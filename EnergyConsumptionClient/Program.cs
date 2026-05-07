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

            proxy.StartSession(meta);

            for (int i = 0; i < samples.Count; i += batchSize)
            {
                List<LoadSample> batch = samples
                    .Skip(i)
                    .Take(batchSize)
                    .ToList();

                proxy.PushBatch(batch);
            }

            proxy.EndSession();

            ((IClientChannel)proxy).Close();
            factory.Close();

            Console.WriteLine("Klijent je završio slanje.");
            Console.ReadLine();
        }

        static List<LoadSample> ReadCsv(string path, string countryCode, string selectedDate)
        {
            List<LoadSample> result = new List<LoadSample>();

            string actualColumn = countryCode + "_load_actual_entsoe_transparency";
            string forecastColumn = countryCode + "_load_forecast_entsoe_transparency";

            using (StreamReader reader = new StreamReader(path))
            {
                string headerLine = reader.ReadLine();
                string[] headers = headerLine.Split(',');

                int utcIndex = Array.IndexOf(headers, "utc_timestamp");
                int localIndex = Array.IndexOf(headers, "cet_cest_timestamp");
                int actualIndex = Array.IndexOf(headers, actualColumn);
                int forecastIndex = Array.IndexOf(headers, forecastColumn);

                if (actualIndex == -1 || forecastIndex == -1)
                {
                    throw new Exception("Ne postoje kolone za državu: " + countryCode);
                }

                string line;
                int rowIndex = 1;
                double cumulativeMWh = 0;

                while ((line = reader.ReadLine()) != null)
                {
                    rowIndex++;

                    string[] parts = line.Split(',');

                    DateTime timestampUtc = DateTime.Parse(parts[utcIndex], CultureInfo.InvariantCulture);
                    DateTime timestampLocal = DateTime.Parse(parts[localIndex], CultureInfo.InvariantCulture);

                    if (timestampLocal.ToString("yyyy-MM-dd") != selectedDate)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(parts[actualIndex]) ||
                        string.IsNullOrWhiteSpace(parts[forecastIndex]))
                    {
                        continue;
                    }

                    double actualMW = double.Parse(parts[actualIndex], CultureInfo.InvariantCulture);
                    double forecastMW = double.Parse(parts[forecastIndex], CultureInfo.InvariantCulture);

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