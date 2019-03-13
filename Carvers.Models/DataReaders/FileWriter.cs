using System;
using System.Collections.Generic;
using System.IO;

namespace Carvers.Models.DataReaders
{
    public class FileWriter : IFileWriter
    {
        private readonly List<string> cache;
        private readonly string filePath;
        private readonly int cacheSize;

        public FileWriter(string filePath, int cacheSize = 1)
        {
            cache = new List<string>();
            this.filePath = filePath;
            this.cacheSize = cacheSize;

            if (!File.Exists(filePath))
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.Create(filePath);
            }

        }

        public void WriteWithTs(string line)
        {
            cache.Add($"{DateTime.Now.ToString()},{line}");

            if (cache.Count >= cacheSize)
            {
                File.AppendAllLines(filePath, cache);
                cache.Clear();
            }
        }
    }

    public interface IFileWriter
    {
        void WriteWithTs(string line);
    }

    public class DummyFileWriter : IFileWriter
    {
        public void WriteWithTs(string line) { }
    }
}