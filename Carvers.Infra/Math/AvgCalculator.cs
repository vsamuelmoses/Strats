using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Carvers.Infra.Math
{
    public class AvgCalculator
    {
        private readonly int _maxCount;
        private readonly List<double> values;

        public AvgCalculator(int maxCount = 0)
        {
            values = new List<double>();
            _maxCount = maxCount;
        }
        public AvgCalculator Add(double value)
        {
            values.Add(value);
            if (_maxCount == 0)
            {
                Average = values.Average();
            }
            else if (values.Count > _maxCount)
            {
                values.RemoveAt(0);
                Average = values.Average();
            }

            return this;
        }

        public void DebugDump()
        {
            Debug.WriteLine("DUMP====================");
            values.ForEach(v => Debug.WriteLine($"Avg Calc - DebugDump{v}"));
        }

        public double? Average { get; private set; }
    }
}