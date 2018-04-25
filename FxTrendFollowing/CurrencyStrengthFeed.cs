using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using Carvers.Infra.Extensions;
using Carvers.Models;
using Carvers.Models.Extensions;

namespace FxTrendFollowing
{
    public class CurrencyStrengthFeed
    {
        private Subject<CurrencyStrengthIndicator> csiStreamSubject;
        public IObservable<CurrencyStrengthIndicator> CsiStream => csiStreamSubject;


        public Dictionary<Currency, Dictionary<DateTimeOffset, Tuple<int,double>>> Feed { get; private set; }

        private CurrencyStrengthFeed(IEnumerable<StockData> stocks, TimeSpan feedSpan)
        {
            csiStreamSubject = new Subject<CurrencyStrengthIndicator>();

            Feed = new Dictionary<Currency, Dictionary<DateTimeOffset, Tuple<int, double>>>();

            foreach (var stock in stocks)
            {
                IEnumerable<Tuple<DateTimeOffset, double, double>> strength;
                if(feedSpan == TimeSpan.FromMinutes(1))
                strength = stock.Candles
                        .Select(candle => candle.Value.ToCurrencyStrength());
                else
                    strength = stock.Candles
                            .Values
                            .Cast<MinuteCandle>()
                            .ToHourCandles()
                            .Select(candle => candle.ToCurrencyStrength());

                strength.Foreach(val =>
                {
                    Add(((CurrencyPair)stock.Symbol).TargetCurrency, val.Item1, val.Item2);
                    Add(((CurrencyPair)stock.Symbol).BaseCurrency, val.Item1, val.Item3);
                });
            }
        }

        public void Add(Currency currency, DateTimeOffset timestamp, double value)
        {
            if (!Feed.ContainsKey(currency))
                Feed.Add(currency, new Dictionary<DateTimeOffset, Tuple<int, double>>());

            if (!Feed[currency].ContainsKey(timestamp))
            {
                Feed[currency].Add(timestamp, Tuple.Create(1, value));
                return;
            }

            var val = Feed[currency][timestamp];
            var count = val.Item1 + 1;
            var csiValue = (value + (val.Item2 * val.Item1)) / count;

            //Feed[currency][timestamp] = Tuple.Create(val.Item1 + 1, (value + (val.Item2 * val.Item1)) / (val.Item1 + 1));
            Feed[currency][timestamp] = Tuple.Create(count, csiValue);
        }


        public static CurrencyStrengthFeed CurrencyStrengthFeedForMinute(IEnumerable<StockData> stocks)
        {
            return new CurrencyStrengthFeed(stocks, TimeSpan.FromMinutes(1));
        }

        public static CurrencyStrengthFeed CurrencyStrengthFeedForHour(IEnumerable<StockData> stocks)
        {
            return new CurrencyStrengthFeed(stocks, TimeSpan.FromHours(1));
        }
    }
}