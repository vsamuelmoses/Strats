using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Security.RightsManagement;
using System.Windows.Forms;
using Carvers.Infra;
using Carvers.Infra.Extensions;
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


    public class ShadowStrengthFeed
        : IIndicatorFeed<TimestampedIndicator<PercentageIndicator>>
    {
        private readonly Subject<TimestampedIndicator<PercentageIndicator>> stream;
        private readonly List<Candle> lookbackCandles;
        private readonly List<ShadowCandle> shadowCandles;

        public ShadowStrengthFeed(IObservable<Timestamped<ShadowCandle>> shadowStream, 
            IObservable<Timestamped<Candle>> candleStream)
        {
            stream = new Subject<TimestampedIndicator<PercentageIndicator>>();
            lookbackCandles = new List<Candle>();
            shadowCandles = new List<ShadowCandle>();
            PrevShadowCandle = ShadowCandle.Null;
            ShadowStrengths = new List<Timestamped<double>>();

            candleStream
                .Zip(shadowStream, Tuple.Create)
                .Subscribe(feed =>
                {
                    


                    Debug.Assert(feed.Item1.Timestamp == feed.Item2.Timestamp);
                    var candle = feed.Item1.Val;
                    var shadow = feed.Item2.Val;
                    var ts = feed.Item2.Timestamp;

                    if (ts.Date == new DateTime(2017, 01, 04))
                    {
                        var breakpoint = true;
                    }
                    if (candle == null || shadow == null)
                    {
                        PrevIndicator = new PercentageIndicator(0d.AttachTimeStamp(feed.Item1.Timestamp));
                        Publish(feed.Item1.Timestamp, PrevIndicator);
                        return;
                    }


                    if (candle == prevCandle)
                    {
                        Publish(feed.Item1.Timestamp, PrevIndicator);
                        return;
                    }

                    prevCandle = candle;
                    AddToLookback(candle);

                    if (shadow != PrevShadowCandle)
                    {
                        PrevShadowCandle = shadow;
                        shadowCandles.Add(shadow);
                    }


                    var tupStrength = lookbackCandles
                        .Take(24)
                        .Select(c =>
                        {
                            var shadeCandle = (shadowCandles.LastOrDefault(sh => sh.TimeStamp.Date < c.TimeStamp.Date));

                            if (shadeCandle == null)
                                return Tuple.Create(0d, 0d);

                            return Tuple.Create((c.High - shadeCandle.Low).PercentageOf(shadeCandle.CandleLength()), (shadeCandle.High - c.Low).PercentageOf(shadeCandle.CandleLength()));
                        }).ToList();


                    var absStrength = tupStrength.Last();

                    PrevIndicator = new PercentageIndicator((absStrength.Item1 - absStrength.Item2).AttachTimeStamp(candle.TimeStamp));
                    


                    ShadowStrength = shadow.Low + shadow.CandleLength() / 2 + (shadow.CandleLength() * PrevIndicator.Value.Val * 0.01);
                    ShadowStrengths.Add(ShadowStrength.Value.AttachTimeStamp(feed.Item1.Val.TimeStamp));
                    if(ShadowStrengths.Count > 4)
                        ShadowStrengths.RemoveAt(0);

                    stream.OnNext(new TimestampedIndicator<PercentageIndicator>(feed.Item1.Timestamp, PrevIndicator));

                });
        }

        private void AddToLookback(Candle candle)
        {
            lookbackCandles.Add(candle);

            if (lookbackCandles.Count > 24)
                lookbackCandles.RemoveAt(0);

        }

        private void Publish(DateTimeOffset ts, PercentageIndicator percentage)
        {
            stream.OnNext(new TimestampedIndicator<PercentageIndicator>(ts, percentage));

        }

        public List<Timestamped<double>> ShadowStrengths { get; private set; }

        public double? ShadowStrength { get; private set; }

        public PercentageIndicator PrevIndicator { get; private set; }
        private Candle prevCandle = Candle.Null;
        public ShadowCandle PrevShadowCandle { get; private set; }
        private readonly FileWriter _writer;
        public IObservable<TimestampedIndicator<PercentageIndicator>> Stream => stream;
    }


    public class PercentageIndicator : IIndicator
    {
        public PercentageIndicator(Timestamped<double> val)
        {
            Value = val;
        }

        public Timestamped<double> Value { get; }
        public string Description { get; }
        public bool HasValidValue { get; }
    }
}