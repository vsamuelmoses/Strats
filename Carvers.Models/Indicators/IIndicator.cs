using System;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace Carvers.Models.Indicators
{
    public interface IIndicator
    {
        string Description { get; }
        bool HasValidValue { get; }
    }

    public class CustomIndicator : IIndicator
    {
        private CustomIndicator(string description)
        {
            Description = description;
        }
        public string Description { get; }
        public bool HasValidValue => true;

        private static ConcurrentDictionary<string, CustomIndicator> _indicators = new ConcurrentDictionary<string, CustomIndicator>();

        public static IIndicator Get(string description)
            => _indicators.GetOrAdd(description, new CustomIndicator(description));

    }

    public static class CustomIndicators
    {
        public static string EquityCurveIndicator = nameof(EquityCurveIndicator);
    }

    public static class IndicatorExtensions
    {
        public static T OfType<T>(this IIndicator indicator)
            => (T) indicator;
    }

    public abstract class BaseIndicator<TOut> : IIndicator
    {
        protected BaseIndicator(string description,
            TOut value,
            DateTimeOffset timeStamp)
        {
            Description = description;
            Value = value;
            TimeStamp = timeStamp;
        }

        public string Description { get; }
        public abstract bool HasValidValue { get; }
        public TOut Value { get; protected set; }
        public DateTimeOffset TimeStamp { get; }
    }

    public abstract class CandleBasedIndicator<TOut> : BaseIndicator<TOut>
    {
        public Candle Candle { get; }

        protected CandleBasedIndicator(string description,
            TOut value,
            Candle candle)
            : base(description, value, candle.TimeStamp)
        {
            Candle = candle;
        }
    }

    public interface IIndicatorStreamingService<T>
        where T : IIndicator
    {
        IObservable<T> Stream { get; }
    }



    public class MovingAverageStreamingService
        : IIndicatorStreamingService<MovingAverage>
    {
        private readonly Subject<MovingAverage> stream;

        public MovingAverageStreamingService(
            string description,
            IObservable<Candle> candleStream,
            Func<Candle, double> candleToValue,
            int length)
        {
            stream = new Subject<MovingAverage>();
            MovingAverage = MovingAverage.Construct(description, length);

            candleStream.Subscribe(candle =>
            {
                MovingAverage = MovingAverage.Push(MovingAverage, candleToValue(candle), candle.TimeStamp);
                stream.OnNext(MovingAverage);
            });
        }

        public MovingAverage MovingAverage { get; private set; }
        public IObservable<MovingAverage> Stream => stream;
    }


    public class HeikinAshiCandle : Candle, IIndicator
    {
        protected HeikinAshiCandle(Ohlc ohlc, DateTimeOffset timeStamp)
            : base(ohlc, timeStamp)
        {
        }

        public HeikinAshiCandle(Ohlc ohlc, DateTimeOffset timeStamp, TimeSpan span)
            : base(ohlc, timeStamp, span)
        {
        }

        public bool IsGreen { get; set; }

        public string Description => Indicators.HeikinAshi;
        public bool HasValidValue => TimeStamp != DateTimeOffset.MinValue && TimeStamp != DateTimeOffset.MaxValue;

        public static readonly HeikinAshiCandle Null = new HeikinAshiCandle(
            new Ohlc(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN), DateTimeOffset.MinValue,
            TimeSpan.MinValue);
    }


    public class HeikinAshiCandleFeed
        : IIndicatorStreamingService<HeikinAshiCandle>
    {
        private readonly Subject<HeikinAshiCandle> stream;

        public HeikinAshiCandleFeed(IObservable<Candle> candleStream)
        {
            stream = new Subject<HeikinAshiCandle>();

            HACandle = HeikinAshiCandle.Null;

            candleStream.Subscribe(candle =>
            {
                if (prevCandle == Candle.Null)
                    prevCandle = candle;


                var isgreen = (candle.High > prevCandle.High && candle.Low > prevCandle.Low);

                if (candle.Close > prevCandle.Low && candle.Close < prevCandle.High)
                {
                    stream.OnNext(HACandle);
                }
                else
                {
                    prevCandle = candle;

                    HACandle = new HeikinAshiCandle(new Ohlc(prevCandle.Open, prevCandle.High, prevCandle.Low, prevCandle.Close, prevCandle.Ohlc.Volume),
                        candle.TimeStamp, candle.Span);
                    HACandle.IsGreen = isgreen;

                    stream.OnNext(HACandle);
                }

                
            });
        }

        private Candle prevCandle = Candle.Null;
        public HeikinAshiCandle HACandle { get; private set; }

        public IObservable<HeikinAshiCandle> Stream => stream;
        //}
        //public class HeikinAshiCandleFeed
        //    : IIndicatorStreamingService<HeikinAshiCandle>
        //{
        //    private readonly Subject<HeikinAshiCandle> stream;
        //    public HeikinAshiCandleFeed(IObservable<Candle> candleStream)
        //    {
        //        stream = new Subject<HeikinAshiCandle>();

        //        HACandle = HeikinAshiCandle.Null;

        //        candleStream.Subscribe(candle =>
        //        {
        //            var open = candle.Open;
        //            var high = candle.High;
        //            var low = candle.Low;
        //            var close = candle.Close;

        //            var haClose = (open + high + low + close) / 4;
        //            double haOpen;
        //            if (HACandle != HeikinAshiCandle.Null)
        //                haOpen = (HACandle.Open + HACandle.Close) / 2;
        //            else
        //                haOpen = (open + close) / 2;

        //            var haHigh = Math.Max(Math.Max(high, haOpen), haClose);
        //            var haLow = Math.Min(Math.Min(low, haOpen), haClose);

        //            HACandle = new HeikinAshiCandle(new Ohlc(haOpen, haHigh, haLow, haClose, candle.Ohlc.Volume), candle.TimeStamp, candle.Span);
        //            stream.OnNext(HACandle);
        //        });
        //    }

        //    public HeikinAshiCandle HACandle { get; private set; }
        //    public IObservable<HeikinAshiCandle> Stream => stream;
        //}
    }
}
