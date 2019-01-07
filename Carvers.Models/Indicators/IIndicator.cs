using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Windows.Forms;
using Carvers.Infra;
using Carvers.Models.DataReaders;
using Carvers.Models.Extensions;

namespace Carvers.Models.Indicators
{
    public interface IIndicator
    {
        string Description { get; }
        bool HasValidValue { get; }
    }

    public class TimestampedIndicator<T> : Timestamped<T>, IIndicator
        where T : IIndicator
    {
        public DateTimeOffset Timestamp { get; }

        public TimestampedIndicator(DateTimeOffset timestamp, T val) : base(timestamp, val)
        {
            Timestamp = timestamp;
        }

        public string Description => Val.Description;
        public bool HasValidValue => Val.HasValidValue;
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

    public interface IIndicatorFeed
    {
        
    }

    public interface IIndicatorFeed<out T> : IIndicatorFeed
        where T : IIndicator
    {
        IObservable<T> Stream { get; }
    }



    public class MovingAverageFeed
        : IIndicatorFeed<MovingAverage>
    {
        private readonly Subject<MovingAverage> stream;

        public MovingAverageFeed(
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


    public class ShadowCandle: Candle, IIndicator
    {
        public int SupportStrength { get; }
        public int ResStrength { get; }

        protected ShadowCandle(Ohlc ohlc, DateTimeOffset timeStamp)
            : base(ohlc, timeStamp)
        {
        }

        public ShadowCandle(Ohlc ohlc, DateTimeOffset timeStamp, TimeSpan span, int supportStrength, int resStrength)
            : base(ohlc, timeStamp, span)
        {
            SupportStrength = supportStrength;
            ResStrength = resStrength;
        }

        public bool IsGreen { get; set; }

        public string Description => Indicators.ShadowCandle;
        public bool HasValidValue => TimeStamp != DateTimeOffset.MinValue && TimeStamp != DateTimeOffset.MaxValue;

        public static readonly ShadowCandle Null = new ShadowCandle(
            new Ohlc(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN), DateTimeOffset.MinValue,
            TimeSpan.MinValue, 1, 1);
    }


    public class ShadowCandleFeed
        : IIndicatorFeed<TimestampedIndicator<ShadowCandle>>
    {
        public FileInfo File { get; }
        private readonly int _lookbackCount;
        private readonly Subject<TimestampedIndicator<ShadowCandle>> stream;
        private readonly List<ShadowCandle> lookback;
        public ShadowCandleFeed(FileInfo file, IObservable<Timestamped<Candle>> candleStream, int lookbackCount)
        {
            File = file;
            _writer = new FileWriter(File.FullName, 1);
            _lookbackCount = lookbackCount;
            stream = new Subject<TimestampedIndicator<ShadowCandle>>();
            lookback = new List<ShadowCandle>();

            ShadowCandle = ShadowCandle.Null;

            candleStream.Subscribe(feed =>
            {
                var candle = feed.Val;

                if (candle == null)
                {
                    stream.OnNext(new TimestampedIndicator<ShadowCandle>(feed.Timestamp, ShadowCandle));
                    return;
                }

                if (prevShadowCandle == Candle.Null)
                    prevShadowCandle = candle;

                if ((prevCandle != null && prevCandle == candle)
                    || candle == null)
                {
                    stream.OnNext(new TimestampedIndicator<ShadowCandle>(feed.Timestamp, ShadowCandle));
                    return;
                }

                prevCandle = candle;

                var isgreen = (candle.High > prevShadowCandle.High && candle.Low > prevShadowCandle.Low);

                //if (candle.Close > prevCandle.Low && candle.Close < prevCandle.High)
                if (candle.Low > prevShadowCandle.Low && candle.High < prevShadowCandle.High)
                {
                    lookback.Add(ShadowCandle);
                    if (lookback.Count > lookbackCount)
                        lookback.RemoveAt(0);

                    stream.OnNext(new TimestampedIndicator<ShadowCandle>(feed.Timestamp, ShadowCandle));
                }
                else
                {
                    prevShadowCandle = candle;

                    int supportStrength = 0;
                    int resistanceStrength = 0;

                    var high = prevShadowCandle.High;
                    var low = prevShadowCandle.Low;
                    
                    ShadowCandle = new ShadowCandle(new Ohlc(prevShadowCandle.Open, high, low, prevShadowCandle.Close, prevShadowCandle.Ohlc.Volume),
                        candle.TimeStamp, candle.Span, supportStrength, resistanceStrength);
                    ShadowCandle.IsGreen = isgreen;

                    lookback.Add(ShadowCandle);
                    if (lookback.Count > lookbackCount)
                        lookback.RemoveAt(0);

                    stream.OnNext(new TimestampedIndicator<ShadowCandle>(feed.Timestamp, ShadowCandle));
                }

            });
        }

        private Candle prevCandle = Candle.Null;
        private Candle prevShadowCandle = Candle.Null;
        private readonly FileWriter _writer;

        public Timestamped<ShadowCandle> TsShadowCandle { get; private set; }
        public ShadowCandle ShadowCandle { get; private set; }
        public IEnumerable<ShadowCandle> Lookback => lookback;
        
        public IObservable<TimestampedIndicator<ShadowCandle>> Stream => stream;

    }
    public class HeikinAshiCandleFeed
        : IIndicatorFeed<HeikinAshiCandle>
    {
        private readonly Subject<HeikinAshiCandle> stream;
        public HeikinAshiCandleFeed(IObservable<Candle> candleStream)
        {
            stream = new Subject<HeikinAshiCandle>();

            HACandle = HeikinAshiCandle.Null;

            candleStream.Subscribe(candle =>
            {
                var open = candle.Open;
                var high = candle.High;
                var low = candle.Low;
                var close = candle.Close;

                var haClose = (open + high + low + close) / 4;
                double haOpen;
                if (HACandle != HeikinAshiCandle.Null)
                    haOpen = (HACandle.Open + HACandle.Close) / 2;
                else
                    haOpen = (open + close) / 2;

                var haHigh = Math.Max(Math.Max(high, haOpen), haClose);
                var haLow = Math.Min(Math.Min(low, haOpen), haClose);

                HACandle = new HeikinAshiCandle(new Ohlc(haOpen, haHigh, haLow, haClose, candle.Ohlc.Volume), candle.TimeStamp, candle.Span);
                stream.OnNext(HACandle);
            });
        }

        public HeikinAshiCandle HACandle { get; private set; }
        public IObservable<HeikinAshiCandle> Stream => stream;
    }

}