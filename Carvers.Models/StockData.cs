using System;
using System.Collections.Generic;
using System.Linq;

namespace Carvers.Models
{
    public class StockData
    {
        public StockData(string symbol, IEnumerable<Candle> candles)
            :this(new Symbol(symbol), candles)
        {
        }

        public StockData(Symbol symbol, IEnumerable<Candle> cands)
        {
            Symbol = symbol;
            Candles = cands.ToDictionary(cand => cand.TimeStamp);
        }

        
        public Symbol Symbol { get; }
        public Dictionary<DateTimeOffset, Candle> Candles { get; }
    }


    public static class StockDataExtensions
    {

        public static StockData Concat(this StockData stockData, IEnumerable<Candle> cands)
        {
            var totalCands = stockData.Candles.Values.Concat(cands);
            return new StockData(stockData.Symbol, totalCands);
        }
    }
}