using System;
using Carvers.Models;

namespace SykesStrategies.ViewModels.Strategies
{
    public class SellOnMaxOrderInfo : IOrderInfo
    {
        public SellOnMaxOrderInfo(DateTimeOffset timestamp, Symbol symbol, IStrategy strategy, Candle candle, Price price)
        {
            Strategy = strategy;
            Candle = candle;
            TimeStamp = timestamp;
            Price = price;
            Symbol = symbol;
        }

        public SellOnMaxOrderInfo(StrategyDecisionInfo info, DateTimeOffset timestamp, Symbol symbol, IStrategy strategy, Candle candle, Price price)
            : this(timestamp,symbol,strategy,candle,price)
        {
            Info = info;
        }

        public IStrategy Strategy { get; }
        public int Size { get; }
        public Candle Candle { get; }
        public DateTimeOffset TimeStamp { get; }
        public Price Price { get; }
        public Symbol Symbol { get; }
        public StrategyDecisionInfo Info { get; }

        public string ToCsv()
        {
            return $"{Symbol},{Info?.ToCsv()},{Candle.ToCsv()}";
        }
    }

    public class StrategyDecisionInfo
    {
        public StrategyDecisionInfo(Symbol symbol, Candle lastMax, Candle previousCandle, Candle decisionCandle)
        {
            Symbol = symbol;
            LastMax = lastMax;
            PrevCandle = previousCandle;
            DecisionCandle = decisionCandle;
        }

        public Symbol Symbol { get; }
        public Candle LastMax { get; }
        public Candle PrevCandle { get; }
        public Candle DecisionCandle { get; }

        public string ToCsv()
        {
            return $"DecisionInfo,{Symbol},{LastMax.ToCsv()},{PrevCandle.ToCsv()},{DecisionCandle.ToCsv()}";
        }
    }
}