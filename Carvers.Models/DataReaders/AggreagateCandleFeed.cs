using System;
using System.Diagnostics;
using System.Reactive.Subjects;
using Carvers.Models.Extensions;

namespace Carvers.Models.DataReaders
{
    public class AggreagateCandleFeed
    {
        private Subject<Candle> stream;
        private Candle lastCandle;

        public AggreagateCandleFeed(IObservable<Candle> candleStream, TimeSpan span)
        {
            stream = new Subject<Candle>();

            candleStream
                .Subscribe(candle =>
                {
                    if (lastCandle == null)
                    {
                        if(candle.TimeStamp.Second == 0 && candle.TimeStamp.Minute == 0)
                            lastCandle = candle;

                        return;
                    }

                    if (candle.TimeStamp < (lastCandle.TimeStamp + span))
                    {
                        lastCandle = lastCandle.Add(candle);

                    }

                    if (candle.TimeStamp - lastCandle.TimeStamp == span)
                    {
                        lastCandle = lastCandle.Add(candle);
                        stream.OnNext(lastCandle);
                        Debug.Assert(candle.TimeStamp.Minute == 0 && candle.TimeStamp.Second == 0);
                        lastCandle = candle;
                    }
                    else if (candle.TimeStamp - lastCandle.TimeStamp > span)
                    {
                        stream.OnNext(lastCandle);
                        if (candle.TimeStamp.Second == 0 && candle.TimeStamp.Minute == 0)
                            lastCandle = candle;
                        else
                        {
                            lastCandle = null;
                        }
                    }

                });
        }

        public IObservable<Candle> Stream => stream;

    }
}
