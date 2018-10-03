using System;
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
                        lastCandle = null;
                    }
                    else if (candle.TimeStamp - lastCandle.TimeStamp > span)
                    {
                        stream.OnNext(lastCandle);
                        lastCandle = candle;
                    }

                });
        }

        public IObservable<Candle> Stream => stream;

    }
}
