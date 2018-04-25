using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using Carvers.IBApi.Extensions;
using Carvers.Models;
using Carvers.Models.Extensions;
using IBSampleApp.messages;

namespace FxTrendFollowing.ViewModels
{
    public class HourlyCurrencyPairData
    {
        private readonly bool shouldCache;
        private readonly int cacheSize;
        public CurrencyPair Pair { get; }

        private readonly Subject<Tuple<DateTimeOffset, double, double>> hourlyCsiSubject;
        private readonly FileWriter fileWriter;
        public IObservable<Tuple<DateTimeOffset, double, double>> HourlyCsiStream => hourlyCsiSubject;

        public HourlyCurrencyPairData(CurrencyPair pair,
            IEnumerable<Candle> seedCandles,
            string directoryPath, 
            bool shouldCache, 
            int cacheSize)
        {
            this.shouldCache = shouldCache;
            this.cacheSize = cacheSize;
            Pair = pair;
            hourlyCsiSubject = new Subject<Tuple<DateTimeOffset, double, double>>();

            fileWriter = new FileWriter(Path.Combine(directoryPath, $"{Pair}.csv"), 12);
            Candles = new Queue<Candle>(seedCandles);
            LatestCandle = seedCandles.LastOrDefault();
        }

        public void Add(RealTimeBarMessage rtbMsg, TimeSpan span)
        {
            if (rtbMsg.IsForCurrencyPair(Pair))
            {
                var candle = rtbMsg.ToCandle(span);
                Add(candle);

                if (shouldCache)
                    fileWriter.Write(candle.ToCsv());
            }
        }

        public Queue<Candle> Candles { get; private set; }

        public void Add(Candle candle)
        {
            while (Candles.Count >= cacheSize)
                Candles.Dequeue();

            Candles.Enqueue(candle);

            LatestCandle = candle;

            if (candle.EndsAtXMinsPastHour(5))
            {
                var lastHourCandles =
                    Candles.Where(c => c.TimeStamp.ToString("yyMMddHH") == candle.TimeStamp.Subtract(TimeSpan.FromHours(1)).ToString("yyMMddHH"));

                if (lastHourCandles.Any())
                {
                    var hourlyCandle = lastHourCandles.ToSingleCandle(TimeSpan.FromHours(1));
                    hourlyCsiSubject.OnNext(hourlyCandle.ToCurrencyStrength(TimeSpan.FromHours(1)));
                }
            }
        }


        //public void Add(Candle candle)
        //{
        //    while (Candles.Count >= cacheSize)
        //        Candles.Dequeue();

        //    Candles.Enqueue(candle);

        //    if (!candle.StartsAtXMinsPastHour(0))
        //    {
        //        LatestCandle = LatestCandle != null ? LatestCandle.Add(candle) : candle;
        //    }
        //    else
        //    {
        //        if (LatestCandle != null)
        //        {
        //            var lastCandle = LatestCandle;
        //            LatestCandle = candle;
        //            hourlyCsiSubject.OnNext(lastCandle.ToCurrencyStrength(TimeSpan.FromHours(1)));
        //        }
        //    }
        //}

        public Candle LatestCandle { get; private set; }
    }
}