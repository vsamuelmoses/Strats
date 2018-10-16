using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Carvers.Models.Indicators
{
    public class MovingAverage
    {
        private readonly int cacheSize;
        private readonly int _length;
        private int _circIndex = -1;
        private bool _filled;
        //private double _current = double.NaN;
        private readonly double _oneOverLength;
        private readonly double[] _circularBuffer;
        private double _total;
        private double _current;

        public ConcurrentQueue<double> Averages { get; private set; }

        public MovingAverage(int length, int cacheSize = 1)
        {
            _length = length;
            _oneOverLength = 1.0 / length;
            _circularBuffer = new double[length];

            Averages = new ConcurrentQueue<double>();
            this.cacheSize = cacheSize;

            Current = double.NaN;
        }

        public MovingAverage Update(double value)
        {
            double lostValue = _circularBuffer[_circIndex];
            _circularBuffer[_circIndex] = value;

            // Maintain totals for Push function
            // skip NaN 
            _total += double.IsNaN(value) ? 0d : value;
            _total -= lostValue;

            // If not yet filled, just return. Current value should be double.NaN
            if (!_filled)
            {
                Current = double.NaN;
                return this;
            }

            // Compute the average
            double average = 0.0;
            for (int i = 0; i < _circularBuffer.Length; i++)
            {
                average += _circularBuffer[i];
            }

            Current = average * _oneOverLength;

            return this;
        }

        public MovingAverage Push(double value)
        {
            // Apply the circular buffer
            if (++_circIndex == _length)
            {
                _circIndex = 0;
            }

            double lostValue = _circularBuffer[_circIndex];
            _circularBuffer[_circIndex] = value;

            // Compute the average
            // Skip NaN 
            _total += double.IsNaN(value) ? 0d : value;
            _total -= lostValue;

            // If not yet filled, just return. Current value should be double.NaN
            if (!_filled && _circIndex != _length - 1)
            {
                Current = double.NaN;
                return this;
            }
            else
            {
                // Set a flag to indicate this is the first time the buffer has been filled
                _filled = true;
            }

            Current = _total * _oneOverLength;

            return this;
        }

        public int Length => _length;
        public double Current
        {
            get { return _current; }
            private set
            {
                _current = value;

                if (Averages.Count == cacheSize)
                {
                    double av;
                    Averages.TryDequeue(out av);
                }

                Averages.Enqueue(_current);
            }
        }
    }

    public sealed class ExponentialMovingAverage
    {
        private readonly int _cacheSize;
        private readonly double _alpha;
        private double _current = double.NaN;

        public ExponentialMovingAverage(int lookBack, int cacheSize = 1)
        {
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
    }

    
}
