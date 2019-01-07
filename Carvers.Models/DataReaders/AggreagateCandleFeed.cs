using System;
using System.Diagnostics;
using System.Reactive.Subjects;
using System.Threading;
using Carvers.Models.Extensions;

namespace Carvers.Models.DataReaders
{
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
                        aggregateCandle = aggregateCandle.Add(candle);
                        //Thread.Sleep(20);
                        Publish(timestamp, aggregateCandle);
                        Debug.Assert(candle.TimeStamp.Minute % span.TotalMinutes == 0 && candle.TimeStamp.Second == 0);
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
