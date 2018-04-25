using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Carvers.Infra;
using Carvers.Infra.Extensions;
using Carvers.Models;

namespace HVLF
{
    public class DailyScannerUniverse : Universe
    {
        public DailyScannerUniverse(string scannerFilePath,
            Func<string, DateTimeOffset> scannerFilePathToDateTimeConverter)
        {
            Date = scannerFilePathToDateTimeConverter(scannerFilePath).Date;
            TimeStampsForASymbol = Utility.ScannerFileParser(scannerFilePath);
            SymbolsAtTimeStamp = GetSymbolsAtATimeStamp(TimeStampsForASymbol);
            Initialise = Load();
        }


        private Dictionary<DateTimeOffset, List<Symbol>> GetSymbolsAtATimeStamp(Dictionary<Symbol, List<DateTimeOffset>> timestampsForASymbol)
        {
            var dictionary = new Dictionary<DateTimeOffset, List<Symbol>>();
            foreach (var kvp in TimeStampsForASymbol)
            {
                kvp.Value.Distinct()
                    .Foreach(value =>
                    {
                        if (!dictionary.ContainsKey(value))
                            dictionary.Add(value, new List<Symbol>());
                        dictionary[value].Add(kvp.Key);
                    });
            }

            return dictionary;
        }

        private Task Load()
        {
            return Task.Factory.StartNew(() =>
            {
                TimeStampsForASymbol
                    .Select(kvp =>
                        Tuple.Create<Symbol, FileInfo>(kvp.Key, new FileInfo(Path.Combine(Paths.HVLFMinuteBars, $"{kvp.Key}_M.csv"))))
                    .Where(tup => tup.Item2.Exists)
                    .Foreach(tup =>
                    {
                        stocks[tup.Item1] = new StockData(tup.Item1.ToString(),
                            CsvReader.ReadFile(tup.Item2, Utility.MinBarCandles, skip: 1));
                    });


                
            });
        }


        public DateTimeOffset Date { get; set; }
        public Dictionary<Symbol, List<DateTimeOffset>> TimeStampsForASymbol { get; }
        public Dictionary<DateTimeOffset, List<Symbol>> SymbolsAtTimeStamp { get; }


        public override Task Initialise { get; }
    }
}