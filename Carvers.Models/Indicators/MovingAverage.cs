using System;
using System.Collections.Concurrent;

namespace Carvers.Models.Indicators
{
    public class MovingAverage : BaseIndicator<double>
    {
        private readonly double totalValue = double.NaN;
        private readonly ConcurrentQueue<double> lookbackValues;
        private MovingAverage(string description,
            ConcurrentQueue<double> lookbackValues,
            int length, 
            double value,
            double total,
            DateTimeOffset timestamp)
            : base(description, value, timestamp)
        {
            Length = length;
            totalValue = total;
            this.lookbackValues = lookbackValues;
            Value = value;
        }
        public int Length { get; }

        public static MovingAverage Push(MovingAverage previousAvg, double value, DateTimeOffset timestamp)
        {
            double newTotalValue = double.NaN;

            if (double.IsNaN(previousAvg.totalValue))
                newTotalValue = value;
            else
                newTotalValue = previousAvg.totalValue + value;

            double lostValue = 0d;
            previousAvg.lookbackValues.Enqueue(value);

            if (previousAvg.lookbackValues.Count > previousAvg.Length)
                previousAvg.lookbackValues.TryDequeue(out lostValue);

            newTotalValue = newTotalValue - lostValue;

            if (previousAvg.lookbackValues.Count == previousAvg.Length)
            {
                var newAvgValue = newTotalValue / previousAvg.Length;
                return new MovingAverage(previousAvg.Description, previousAvg.lookbackValues, previousAvg.Length, newAvgValue, newTotalValue, timestamp);
            }

            if (previousAvg.lookbackValues.Count < previousAvg.Length)
                return new MovingAverage(previousAvg.Description, previousAvg.lookbackValues, previousAvg.Length, double.NaN, newTotalValue, timestamp);

            throw new Exception("Unexpected error");
        }

        public static MovingAverage Construct(string description, int length)
        {
            return new MovingAverage(description, new ConcurrentQueue<double>(), length, double.NaN, double.NaN, DateTime.MinValue);
        }

        public override bool HasValidValue => !double.IsNaN(Value);
    }

    public sealed class ExponentialMovingAverage : IIndicator
    {
        private readonly int _cacheSize;
        private readonly double _alpha;
        private double _current = double.NaN;

        public ExponentialMovingAverage(string description, int lookBack, int cacheSize = 1)
        {
            Description = description;
            _cacheSize = cacheSize;
            _alpha = 2f / (lookBack + 1);
            Averages = new ConcurrentQueue<double>();
        }

        public double Push(double value) => 
            Current = double.IsNaN(Current) 
                ? value
                : (value - Current) * _alpha + Current;

        public ConcurrentQueue<double> Averages { get; private set; }

        public double Current
        {
            get { return _current; }
            private set
            {
                _current = value;

                if (Averages.Count == _cacheSize)
                {
                    double av;
                    Averages.TryDequeue(out av);
                }

                Averages.Enqueue(_current);
            }
        }

        public string Description { get; }
        public bool HasValidValue => !double.IsNaN(Current);
    }

    public class AverageCandleIndicator
    {
        public AverageCandleIndicator(MovingAverage fullCandleLength, 
            MovingAverage candleBodyLength)
        {
            FullCandleLength = fullCandleLength;
            CandleBodyLength = candleBodyLength;
        }

        private MovingAverage FullCandleLength { get; }
        private MovingAverage CandleBodyLength { get; }
    }
}
