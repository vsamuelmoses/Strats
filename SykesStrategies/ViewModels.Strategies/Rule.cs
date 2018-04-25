using System;
using System.Collections;
using System.Collections.Generic;
using Carvers.Models;

namespace SykesStrategies.ViewModels.Strategies
{
    public abstract class Rule
    {
        protected readonly StockData data;

        protected Rule(StockData data, 
            string description)
        {
            Description = description;
            this.data = data;
        }
        public abstract bool Execute(Candle thisCandle);
        public string Description { get; }
    }

    public class CandleRule
    {
        private readonly Func<StockData, Candle, bool> stockDataPredicate;
        private readonly Func<IEnumerable<Candle>, Candle, bool> multicandlePredicate;
        private readonly Func<Candle, Candle, bool> biCandlePredicate;
        private readonly Predicate<Candle> singleCandlePredicate;
        public string Description { get; }

        public CandleRule(Predicate<Candle> singleCandlePredicate, string description)
        {
            Description = description;
            this.singleCandlePredicate = singleCandlePredicate;
        }

        public CandleRule(Func<Candle, Candle, bool> biCandlePredicate, string description)
        {
            Description = description;
            this.biCandlePredicate = biCandlePredicate;
        }

        public CandleRule(Func<IEnumerable<Candle>, Candle, bool> multicandlePredicate, string description)
        {
            this.multicandlePredicate = multicandlePredicate;
            Description = description;
        }


        public CandleRule(Func<StockData, Candle, bool> stockDataPredicate, string description)
        {
            this.stockDataPredicate = stockDataPredicate;
            Description = description;
        }

        public bool Execute(Candle candle)
        {
            return singleCandlePredicate(candle);
        }

        public bool Execute(Candle candle1, Candle candle2)
        {
            return biCandlePredicate(candle1, candle2);
        }

        public bool Execute(StockData stock, Candle candle)
        {
            return stockDataPredicate(stock, candle);
        }
    }
}