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
        public IEnumerable<AllCurrencyStrength> PastStrengths { get; }

        static AllCurrencyStrength()
        {
            CurrencyStrengthSubject = new Subject<AllCurrencyStrength>();
        }

        private AllCurrencyStrength(IEnumerable<AllCurrencyStrength> pastStrengths, DateTimeOffset ts)
        {
            TimeStamp = ts;
            IndividualStrengths = new Dictionary<Currency, SingleCurrencyStrength>();
            PastStrengths = pastStrengths;
        }

        public AllCurrencyStrength Add(SingleCurrencyStrength strength)
        {
            if (TimeStamp != strength.TimeStamp)
            {
                var csi = new AllCurrencyStrength(PastStrengths, strength.TimeStamp);
                csi.IndividualStrengths.Add(strength.Currency, strength);
                return csi;
            }

            IndividualStrengths.Add(strength.Currency, strength);

            if (IndividualStrengths.Count == 8)
            {
                var past = PastStrengths.ToList();
                past.Add(this);
                var newStrength = new AllCurrencyStrength(past, strength.TimeStamp);
                CurrencyStrengthSubject.OnNext(newStrength);
                return newStrength;
            }

            return this;
        }

        public static AllCurrencyStrength Unknown()
            => new AllCurrencyStrength(Enumerable.Empty<AllCurrencyStrength>(), DateTimeOffset.MinValue);
        

        private static readonly Subject<AllCurrencyStrength> CurrencyStrengthSubject;
        public static IObservable<AllCurrencyStrength> CurrencyStrengthStream => CurrencyStrengthSubject;
    }

    
}