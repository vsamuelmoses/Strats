using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using Carvers.Models;

namespace FxTrendFollowing.ViewModels
{
    public class SingleCurrencyStrength
    {
        public Dictionary<Currency, double> StrengthAgainstCurrency { get; }
        static SingleCurrencyStrength()
        {
            CurrencyStrengthSubject = new Subject<SingleCurrencyStrength>();
            PastStrength = new Dictionary<Currency, List<SingleCurrencyStrength>>();

            Currency.Currencies.ForEach(c => PastStrength.Add(c, new List<SingleCurrencyStrength>()));
        }

        private SingleCurrencyStrength(Currency c, DateTimeOffset ts)
        {
            Currency = c;
            StrengthAgainstCurrency = new Dictionary<Currency, double>();
            TimeStamp = ts;
        }

        private static Dictionary<Currency, SingleCurrencyStrength> CurrentStrength = new Dictionary<Currency, SingleCurrencyStrength>();

        public static void Add(Currency baseCurrency, Currency againstCurrency, double value, DateTimeOffset ts)
        {
            if (!CurrentStrength.ContainsKey(baseCurrency) || CurrentStrength[baseCurrency].TimeStamp != ts)
                CurrentStrength[baseCurrency] = new SingleCurrencyStrength(baseCurrency, ts);

            CurrentStrength[baseCurrency].StrengthAgainstCurrency.Add(againstCurrency, value);
            if (CurrentStrength[baseCurrency].StrengthAgainstCurrency.Count == 7)
            {
                var recentStrength = CurrentStrength;
                PastStrength[baseCurrency].Add(CurrentStrength[baseCurrency]);
                CurrencyStrengthSubject.OnNext(recentStrength[baseCurrency]);

                CurrentStrength.Remove(baseCurrency);
            }
        }

        public Currency Currency { get; }
        public double AverageValue => StrengthAgainstCurrency.Sum(kvp => kvp.Value)/ StrengthAgainstCurrency.Count;
        public DateTimeOffset TimeStamp { get; }
        public static Dictionary<Currency, List<SingleCurrencyStrength>> PastStrength { get; }

        public static SingleCurrencyStrength Unknown(Currency c) => new SingleCurrencyStrength(c, DateTimeOffset.MinValue);

        private static readonly Subject<SingleCurrencyStrength> CurrencyStrengthSubject;
        public static IObservable<SingleCurrencyStrength> CurrencyStrengthStream => CurrencyStrengthSubject;
    }
}