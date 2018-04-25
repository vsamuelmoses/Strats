using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Carvers.Infra;
using Carvers.Infra.Extensions;
using Carvers.Models;
using System.Collections.Generic;

namespace CSIDataProvider
{
    public class SykesUniverse : Universe
    {
        private IEnumerable<Symbol> symbols;
        private Predicate<Candle> include;

        public SykesUniverse(IEnumerable<Symbol> symbols, Predicate<Candle> includePredicate)
        {
            this.symbols = symbols;
            this.include = includePredicate;
            Initialise = Load();
        }

        private Task Load()
        {
            return Task.Factory.StartNew(() =>
            {
                symbols
                .Select(symbol => Tuple.Create(symbol, new FileInfo(Path.Combine(Paths.CsiData, $"{symbol}.csv"))))
                .Where(tup => tup.Item2.Exists)
                .Foreach(tup =>
                {
                    stocks[tup.Item1] = new StockData(tup.Item1, CsvReader.ReadFile(tup.Item2, Utility.CSIFormat, skip: 1).Where(can => include(can)));
                });

                
            });
        }
        public override Task Initialise { get; }
    }
}