using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Carvers.Infra;
using Carvers.Infra.Extensions;

namespace Carvers.Models
{
    public class DirectoryUniverse : Universe
    {
        private readonly DirectoryInfo directory;
        private readonly Func<string[], Candle> ctr;
        private readonly Func<string, Symbol> fileNameToSymbolConverter;

        public DirectoryUniverse(DirectoryInfo directory, Func<string[], Candle> ctr, Func<string, Symbol> fileNameToSymbolConverter = null)
        {
            this.directory = directory;
            this.ctr = ctr;
            this.fileNameToSymbolConverter = fileNameToSymbolConverter ?? (fileName => new Symbol(fileName));
            Initialise = Load();
        }

        private Task Load()
        {
            return Task.Factory.StartNew(() =>
            {
                Directory
                    .EnumerateFiles(directory.FullName)
                    .Select(file => new FileInfo(file))
                    .Foreach(file =>
                    {
                        var symbol = fileNameToSymbolConverter(file.Name);
                        stocks.AddOrUpdate(symbol, 
                            s => new StockData(s, CsvReader.ReadFile(file, ctr, skip: 1)), 
                            (s, stock) => stock.Concat(CsvReader.ReadFile(file, ctr, skip: 1)));
                    });
            });
        }
        public override Task Initialise { get; }
    }
}