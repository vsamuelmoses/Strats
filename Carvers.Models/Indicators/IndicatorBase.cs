using System;

namespace Carvers.Models.Indicators
{
    public abstract class IndicatorBase<TIn, TOut> : IIndicator
    {
        protected IndicatorBase(string description, Func<TIn, TOut> calculate)
        {
            Description = description;
            Calculate = calculate;
        }

        public TOut Push(TIn input)
            => Calculate(input);

        public string Description { get; }
        public bool HasValidValue { get; }
        public Func<TIn, TOut> Calculate { get; }
    }
}
