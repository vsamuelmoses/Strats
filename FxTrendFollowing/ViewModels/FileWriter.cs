using System.Collections.Generic;
using System.IO;

namespace FxTrendFollowing.ViewModels
{
    public class FileWriter
    {
        private readonly List<string> cache;
        private readonly string filePath;
        private readonly int cacheSize;

        public FileWriter(string filePath, int cacheSize)
        {
            cache = new List<string>();
            this.filePath = filePath;
            this.cacheSize = cacheSize;
        }

        public void Write(string line)
        {
            cache.Add(line);

            if (cache.Count >= cacheSize)
            {
                File.AppendAllLines(filePath, cache);
                cache.Clear();
            }
        }
    }
}