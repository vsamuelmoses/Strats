using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Windows.Media.Converters;
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

        public static void Add(Currency baseCurrency, Currency againstCurrency, double value, DateTimeOffset ts)
        {

            var strengthAtTime = PastStrength[baseCurrency].SingleOrDefault(strength => strength.TimeStamp == ts);

            if (strengthAtTime == null)
            {
                strengthAtTime = new SingleCurrencyStrength(baseCurrency, ts);
                strengthAtTime.StrengthAgainstCurrency.Add(againstCurrency, value);
                PastStrength[baseCurrency].Add(strengthAtTime);
                //return strengthAtTime;
            }
            else
            {
                strengthAtTime.StrengthAgainstCurrency.Add(againstCurrency, value);
                if (strengthAtTime.StrengthAgainstCurrency.Count == 7)
                {
                    //PastStrength[baseCurrency].Add(strengthAtTime);
                    CurrencyStrengthSubject.OnNext(strengthAtTime);
                }
                //return this;
            }



            ///var strengthAtTime = PastStrength[Currency].SingleOrDefault(strength => strength.TimeStamp == ts);

            //if (strengthAtTime == null)
            //{
            //    strengthAtTime = new SingleCurrencyStrength(Currency, ts);
            //    strengthAtTime.StrengthAgainstCurrency.Add(againstCurrency, value);
            //    PastStrength[Currency].Add(strengthAtTime);
            //    return strengthAtTime;
            //}
            //else
            //{
            //    var strengthAtTime = this;
            //    if (StrengthAgainstCurrency.Count == 7)
            //    {

            //    }

            //    StrengthAgainstCurrency.Add(againstCurrency, value);
            //    if (StrengthAgainstCurrency.Count == 7)
            //    {
            //        PastStrength[Currency].Add(this);
            //        CurrencyStrengthSubject.OnNext(this);


            //    }
            //    return this;
            //}







            //if (StrengthAgainstCurrency.Count != 0 && ts != TimeStamp)
            //    return new SingleCurrencyStrength(Currency, new Dictionary<Currency, double> {{againstCurrency, value}}, ts);

            //this.StrengthAgainstCurrency.Add(againstCurrency, value);

            ////StrengthAgainstCurrency[againstCurrency] = value;


            //var currenctStrength = new SingleCurrencyStrength(Currency, StrengthAgainstCurrency, ts);

            //if (currenctStrength.StrengthAgainstCurrency.Count == 7)
            //{
            //    CurrencyStrengthSubject.OnNext(currenctStrength);
            //    return new SingleCurrencyStrength(currenctStrength.Currency, new Dictionary<Currency, double>(), DateTimeOffset.MinValue, currenctStrength.PastStrength);
            //}

            //return currenctStrength;
        }



        public Currency Currency { get; }
        //public int Count { get; }
        //public double Value { get; }
        //public double AverageValue => Value / Count;
        public double AverageValue => StrengthAgainstCurrency.Sum(kvp => kvp.Value)/ StrengthAgainstCurrency.Count;
        public DateTimeOffset TimeStamp { get; }
        public static Dictionary<Currency, List<SingleCurrencyStrength>> PastStrength { get; }

        public static SingleCurrencyStrength Unknown(Currency c) => new SingleCurrencyStrength(c, DateTimeOffset.MinValue);

        private static readonly Subject<SingleCurrencyStrength> CurrencyStrengthSubject;
        public static IObservable<SingleCurrencyStrength> CurrencyStrengthStream => CurrencyStrengthSubject;
    }
}