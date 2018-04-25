using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FxTrendFollowing.ViewModels;

namespace FxTrendFollowing.Reporters
{
    public class CurrencyStrengthLogger
    {
        public CurrencyStrengthLogger(FileInfo file, IObservable<AllCurrencyStrength> csiStream)
        {
            csiStream.Subscribe(cs =>
            {
                File.AppendAllLines(file.FullName, new List<string> {
                    string.Join(",",
                        new[]
                        {
                            cs.TimeStamp.ToString(),
                            string.Join(",",
                                cs.IndividualStrengths
                                    .OrderByDescending(strength => strength.Key)
                                    .Select(strength => $"{strength.Key}:{strength.Value.AverageValue.ToString()}"))
                        })});
            });
        }
    }
}
