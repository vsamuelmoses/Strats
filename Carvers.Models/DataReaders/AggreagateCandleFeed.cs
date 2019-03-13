using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using Carvers.Models.Extensions;
using Carvers.Infra.Extensions;

namespace Carvers.Models.DataReaders
{
    public static class CandleFeed
    {

        //public static IObservable<RealTimeBarMessage> ToHourlyStream(this IObservable<RealTimeBarMessage> candleStream)
        //    => Aggregate(candleStream,
        //        (c1, c2) => c1.TimeStamp.Hour != c2.TimeStamp.Hour,
        //        TimeSpan.FromHours(1),
        //        dateTime => dateTime.Date.Add(TimeSpan.FromHours(dateTime.Hour)));

        //public static IObservable<RealTimeBarMessage> ToDailyStream(this IObservable<RealTimeBarMessage> candleStream)
        //    => Aggregate(candleStream,
        //        (c1, c2) => c1.TimeStamp.Date != c2.TimeStamp.Date,
        //        TimeSpan.FromDays(1),
        //        dateTime => dateTime.Date);


        public static IObservable<Candle> ToHourlyStream(this IObservable<Candle> candleStream)
            => Aggregate(candleStream, 
                (c1, c2) => c1.TimeStamp.Hour != c2.TimeStamp.Hour, 
                TimeSpan.FromHours(1), 
                dateTime => dateTime.Date.Add(TimeSpan.FromHours(dateTime.Hour)));

        public static IObservable<Candle> ToDailyStream(this IObservable<Candle> candleStream)
            => Aggregate(candleStream,
                //(c1, c2) => c1.TimeStamp.Date != c2.TimeStamp.Date,
                (c1, c2) => c1.TimeStamp.Hour == 16 && c2.TimeStamp.Hour == 17,
                TimeSpan.FromDays(1),
                dateTime => dateTime.Date);

        private static IObservable<Candle> Aggregate(IObservable<Candle> candleStream, 
            Func<Candle, Candle, bool> boundry, 
            TimeSpan span, 
            Func<DateTime, DateTime> startTimeAdjustment = null)
        {
            if (startTimeAdjustment == null)
                startTimeAdjustment = dt => dt;

            var boundrySelector = candleStream
                    .Zip(candleStream.Skip(1), Tuple.Create)
                    .Where(tup =>
                    {
                        //Debug.WriteLine($"filtering sequence- {tup.Item1.TimeStamp}, {tup.Item2.TimeStamp}");
                        return boundry(tup.Item1, tup.Item2);
                    })
                    .Publish().
                RefCount();

            return candleStream
                .Buffer(boundrySelector, s => boundrySelector)
                .Where(candles => candles.Any())
                .Select(candles => {
                    return candles.ToSingleCandle(span).AdjustTime(startTimeAdjustment);
                })
                .Publish()
                .RefCount();
        }
    }


    public class AggreagateCandleFeed
    {
        private Subject<Timestamped<Candle>> stream;
        private Candle aggregateCandle;
        private Candle lastPublishedCandle = Candle.Null;
        private Candle lastSourceCandle;

        public AggreagateCandleFeed(IObservable<Timestamped<Candle>> candleStream, TimeSpan span)
        {
            Predicate<DateTimeOffset> startTime = null;
            stream = new Subject<Timestamped<Candle>>();

            if (span == TimeSpan.FromDays(1))
            {
                startTime = time => time.Hour == 0 && time.Minute == 0 && time.Second == 0;
            }
            else if (span == TimeSpan.FromHours(1))
            {
                startTime = time => time.Minute == 0 && time.Second == 0;
            }
            else if (span == TimeSpan.FromMinutes(1))
            {
                startTime = time => time.Second == 0;
            }
            else
            {
                throw new Exception("Aggregate span not supported");
            }

            candleStream
                .Subscribe(tsdCandle =>
                {
                    var timestamp = tsdCandle.Timestamp;
                    var candle = tsdCandle.Val;

                    if (lastSourceCandle != null && lastSourceCandle == candle
                        || candle == null)
                    {
                        Publish(timestamp, lastPublishedCandle);
                        return;
                    }

                    lastSourceCandle = candle;

                    if (aggregateCandle == null)
                    {
                        //if(candle.TimeStamp.Second == 0 && candle.TimeStamp.Minute == 0)
                        if(startTime(candle.TimeStamp))
                            aggregateCandle = candle;

                        Publish(timestamp, lastPublishedCandle);

                        return;
                    }

                    if (candle.TimeStamp < (aggregateCandle.TimeStamp + span))
                    {
                        aggregateCandle = aggregateCandle.Add(candle);
                        Publish(timestamp, lastPublishedCandle);
                    }

                    if (candle.TimeStamp - aggregateCandle.TimeStamp == span)
                    {
                        //aggregateCandle = aggregateCandle.Add(candle);
                        //Thread.Sleep(20);
                        Publish(timestamp, aggregateCandle);
                        //Debug.Assert(candle.TimeStamp.Minute % span.TotalMinutes == 0 && candle.TimeStamp.Second == 0);
                        aggregateCandle = candle;
                    }
                    else if (candle.TimeStamp - aggregateCandle.TimeStamp > span)
                    {
                        //Thread.Sleep(20);

                        Publish(timestamp, aggregateCandle);
                        aggregateCandle = startTime(candle.TimeStamp) ? candle : null;
                    }

                });
        }

        private void Publish(DateTimeOffset ts, Candle candle)
        {
            lastPublishedCandle = candle;
            stream.OnNext(new Timestamped<Candle>(ts, lastPublishedCandle));
        }

        public IObservable<Timestamped<Candle>> Stream => stream;
    }
}
