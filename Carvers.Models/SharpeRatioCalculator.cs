using System;
using System.Collections.Generic;
using System.Linq;

namespace Carvers.Models
{

    public class SharpeRatioCalculator
    {
        private static double StandardDeviation(IEnumerable<double> valueList)
        {
            double m = 0.0;
            double s = 0.0;
            int k = 1;

            foreach (double value in valueList)
            {
                double tmpM = m;
                m += (value - tmpM) / k;
                s += (value - tmpM) * (value - m);
                k++;
            }
            return (Math.Sqrt(s / (k - 1))) / 100;
        }

        private static double AverageReturn(ICollection<double> list)
        {
            var counter = list.Sum();
            return (counter / list.Count) / 100;
        }

        private static double SharpeRatio(double average, double stdev, int numberOfTradeSessions)
        {
            return (Math.Sqrt(numberOfTradeSessions) * (average / stdev));
        }

        public static double Calculate(IEnumerable<IClosedOrder> orders)
        {
            var dailyReturnList = orders.Select(o => o.ProfitLoss.Value).ToList();
            var average = AverageReturn(dailyReturnList);
            var stdev = StandardDeviation(dailyReturnList);
            return SharpeRatio(average, stdev, dailyReturnList.Count());
        }
    }
}