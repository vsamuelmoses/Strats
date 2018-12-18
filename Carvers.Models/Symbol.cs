using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Carvers.Models
{
    public class Symbol
    {
        public Symbol(string val)
        {
            Val = val;
        }

        public Symbol(string val, int id)
        {
            Val = val;
            UniqueId = id;
        }

        public string Val { get; }

        public override string ToString()
        {
            return Val;
        }

        public override bool Equals(object obj)
        {
            var val = (obj as string);
            if (val == null)
                return false;

            return Equals(val);
        }

        protected bool Equals(Symbol other)
        {
            return string.Equals(Val, other.Val);
        }

        public override int GetHashCode()
        {
            return Val?.GetHashCode() ?? 0;
        }

        public int UniqueId { get; }
    }


    public class Index : Symbol
    {
        private static readonly List<Index> Indices = new List<Index>();
        public static Index DAX = new Index("DAX", 2001);
        private Index(string symbol, int uniqueId) : base(symbol, uniqueId)
        {
            Indices.Add(this);
        }

        public static Index Get(string symbol)
            => Indices.Single(i => i.Val == symbol);
    }



    public class CurrencyPair : Symbol
    {
        private static readonly Dictionary<Currency, List<CurrencyPair>> Pairs = new Dictionary<Currency, List<CurrencyPair>>();

        public static CurrencyPair AUDCAD = new CurrencyPair(Currency.AUD, Currency.CAD);
        public static CurrencyPair AUDCHF = new CurrencyPair(Currency.AUD, Currency.CHF);
        public static CurrencyPair AUDJPY = new CurrencyPair(Currency.AUD, Currency.JPY);
        public static CurrencyPair AUDNZD = new CurrencyPair(Currency.AUD, Currency.NZD);
        public static CurrencyPair AUDUSD = new CurrencyPair(Currency.AUD, Currency.USD);
        public static CurrencyPair CADCHF = new CurrencyPair(Currency.CAD, Currency.CHF);
        public static CurrencyPair CADJPY = new CurrencyPair(Currency.CAD, Currency.JPY);
        public static CurrencyPair CHFJPY = new CurrencyPair(Currency.CHF, Currency.JPY);
        public static CurrencyPair EURAUD = new CurrencyPair(Currency.EUR, Currency.AUD);
        public static CurrencyPair EURCAD = new CurrencyPair(Currency.EUR, Currency.CAD);
        public static CurrencyPair EURCHF = new CurrencyPair(Currency.EUR, Currency.CHF);
        public static CurrencyPair EURGBP = new CurrencyPair(Currency.EUR, Currency.GBP);
        public static CurrencyPair EURJPY = new CurrencyPair(Currency.EUR, Currency.JPY);
        public static CurrencyPair EURNZD = new CurrencyPair(Currency.EUR, Currency.NZD);
        public static CurrencyPair EURUSD = new CurrencyPair(Currency.EUR, Currency.USD);
        public static CurrencyPair GBPAUD = new CurrencyPair(Currency.GBP, Currency.AUD);
        public static CurrencyPair GBPCAD = new CurrencyPair(Currency.GBP, Currency.CAD);
        public static CurrencyPair GBPCHF = new CurrencyPair(Currency.GBP, Currency.CHF);
        public static CurrencyPair GBPJPY = new CurrencyPair(Currency.GBP, Currency.JPY);
        public static CurrencyPair GBPNZD = new CurrencyPair(Currency.GBP, Currency.NZD);
        public static CurrencyPair GBPUSD = new CurrencyPair(Currency.GBP, Currency.USD);
        public static CurrencyPair NZDCAD = new CurrencyPair(Currency.NZD, Currency.CAD);
        public static CurrencyPair NZDCHF = new CurrencyPair(Currency.NZD, Currency.CHF);
        public static CurrencyPair NZDJPY = new CurrencyPair(Currency.NZD, Currency.JPY);
        public static CurrencyPair NZDUSD = new CurrencyPair(Currency.NZD, Currency.USD);
        public static CurrencyPair USDCAD = new CurrencyPair(Currency.USD, Currency.CAD);
        public static CurrencyPair USDCHF = new CurrencyPair(Currency.USD, Currency.CHF);
        public static CurrencyPair USDJPY = new CurrencyPair(Currency.USD, Currency.JPY);


        private CurrencyPair(Currency target, Currency @base)
        : base(target.ToString() + @base.ToString(), target.UniqueId * 100 + @base.UniqueId)
        {
            BaseCurrency = @base;
            TargetCurrency = target;

            if(!Pairs.ContainsKey(TargetCurrency))
                Pairs.Add(TargetCurrency, new List<CurrencyPair>());

            Pairs[TargetCurrency].Add(this);

        }

        public Currency BaseCurrency { get; }
        public Currency TargetCurrency { get; }

        public override int GetHashCode()
        {
            return BaseCurrency.GetHashCode() * 13 + TargetCurrency.GetHashCode()  * 37;
        }

        public override bool Equals(object other)
        {
            return other is CurrencyPair otherKeys &&
                   this.TargetCurrency == otherKeys.TargetCurrency &&
                   this.BaseCurrency == otherKeys.BaseCurrency;
        }

        public static CurrencyPair Get(Currency primaryCurrency, Currency baseCurrency)
        {
            return Pairs[primaryCurrency].Single(pair => pair.BaseCurrency == baseCurrency);
        }

        public static bool Contains(Currency primary, Currency baseCurrency)
        {
            if (Pairs.ContainsKey(primary))
                return Pairs[primary].Any(pair => pair.BaseCurrency == baseCurrency);

            return false;
        }

        public static List<CurrencyPair> All()
        {
            return Pairs
                    .Select(kvp => kvp.Value)
                    .SelectMany(items => items)
                    .ToList();
        }
    }

    public class Currency
    {
        public static readonly List<Currency> Currencies = new List<Currency>();

        public static readonly Currency AUD = new Currency("AUD", 1);
        public static readonly Currency CAD = new Currency("CAD", 2);
        public static readonly Currency CHF = new Currency("CHF", 3);
        public static readonly Currency EUR = new Currency("EUR", 4);
        public static readonly Currency GBP = new Currency("GBP", 5);
        public static readonly Currency JPY = new Currency("JPY", 6);
        public static readonly Currency NZD = new Currency("NZD", 7);
        public static readonly Currency USD = new Currency("USD", 8);

        private Currency(string symbol, int uniqueId)
        {
            Symbol = symbol;
            UniqueId = uniqueId;
            Currencies.Add(this);
        }

        public override string ToString() => Symbol;

        public string Symbol { get;  }
        public int UniqueId { get; }

        public static Currency Get(string cur) => Currencies.Single(currency => currency.Symbol == cur);
    }


    public static class CurrencyPairExtensions
    {
        public static double ProfitLoss(this CurrencyPair pair, int positionSize, double differenceInPips, Func<CurrencyPair, double> fxRateGetter)
        {
            if (pair.BaseCurrency == Currency.GBP)
                return (differenceInPips * positionSize);

            return (differenceInPips * positionSize) / fxRateGetter(CurrencyPair.Get(Currency.GBP, pair.BaseCurrency));
        }
    }
}