using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using Carvers.Models;

namespace FxTrendFollowing.ViewModels
{
    public class AllCurrencyStrength
    {
        public DateTimeOffset TimeStamp { get; }
        public Dictionary<Currency, SingleCurrencyStrength> IndividualStrengths { get; }
        public static List<AllCurrencyStrength> PastStrengths { get; }
        private static AllCurrencyStrength CurrentStrength;

        static AllCurrencyStrength()
        {
            CurrencyStrengthSubject = new Subject<AllCurrencyStrength>();
            PastStrengths = new List<AllCurrencyStrength>();
        }

        private AllCurrencyStrength(DateTimeOffset ts)
        {
            TimeStamp = ts;
            IndividualStrengths = new Dictionary<Currency, SingleCurrencyStrength>();
        }

        public static AllCurrencyStrength Add(SingleCurrencyStrength strength)
        {
            if (CurrentStrength == null || (CurrentStrength.TimeStamp != strength.TimeStamp))
                CurrentStrength = new AllCurrencyStrength(strength.TimeStamp);

            CurrentStrength.IndividualStrengths.Add(strength.Currency, strength);

            if (CurrentStrength.IndividualStrengths.Count == 8)
            {
                PastStrengths.Add(CurrentStrength);
                var recentStrength = CurrentStrength;
                CurrencyStrengthSubject.OnNext(recentStrength);
                CurrentStrength = null;
            }

            return CurrentStrength;
        }

        private static readonly Subject<AllCurrencyStrength> CurrencyStrengthSubject;
        public static IObservable<AllCurrencyStrength> CurrencyStrengthStream => CurrencyStrengthSubject;
    }

    
}