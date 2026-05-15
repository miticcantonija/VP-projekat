using System;
using System.IO;

namespace EnergyConsumptionClient
{
    public class CsvFileManager : IDisposable
    {
        private StreamReader reader;
        private FileStream fileStream;
        private bool disposed = false;

        public CsvFileManager(string path)
        {
            fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            reader = new StreamReader(fileStream);
        }

        public string ReadLine()
        {
            return reader.ReadLine();
        }

        ~CsvFileManager()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    reader?.Dispose();
                    fileStream?.Dispose();
                }

                disposed = true;
            }
        }
    }
}