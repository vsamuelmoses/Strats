using System;
using Carvers.Models;

namespace SykesStrategies.ViewModels.Strategies
{
    public class ValueInRangeRule<T> : Rule
        where T : IComparable
    {
        private readonly Func<Candle, T> valueGetter;
        private readonly T @from;
        private readonly T to;

        public ValueInRangeRule(StockData data, 
            Func<Candle, T> valueGetter,
            T from, T to,
            string description) : base(data, description)
        {
            this.valueGetter = valueGetter;
            this.@from = @from;
            this.to = to;
        }

        public override bool Execute(Candle thisCandle)
        {
            var value = valueGetter(thisCandle);
            return (value.CompareTo(from) >= 0 && value.CompareTo(to) <= 0);
        }
    }
}