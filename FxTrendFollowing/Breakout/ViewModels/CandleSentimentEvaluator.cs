using Carvers.Models;
using Carvers.Models.Extensions;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace FxTrendFollowing.Breakout.ViewModels
{
    public class LookbackEvaluator : IRule
    {
        public LookbackEvaluator(Lookback lb)
        {
            Lb = lb;
        }

        public IRule Evaluate(Candle candle)
        {
            Lb = Lb.Add(candle);
            if (Lb.IsComplete())
                return new TrendContinuation(Lb);

            return new LookbackEvaluator(Lb);
        }

        public Lookback Lb { get; private set; }
    }

    public class Lookback
    {
        public Lookback(int period, ConcurrentQueue<Candle> candles)
        {
            Candles = candles;
            HighCandle = candles.OrderBy(candle =>candle.Ohlc.High).LastOrDefault();
            LowCandle = candles.OrderBy(candle => candle.Ohlc.Low).FirstOrDefault();
            Period = period;
        }

        public int Count => Candles.Count;
        public ConcurrentQueue<Candle> Candles { get; }
        public Candle HighCandle { get; }
        public Candle LowCandle { get; }
        public Candle CurrentCandle => Candles.Last();
        public int Period { get; }
    }

    public static class Extensions
    {
        public static Lookback Add(this Lookback lb, Candle candle)
        {
            if (lb.Period == lb.Count)
            {
                Candle none;
                lb.Candles.TryDequeue(out none);
            }

            lb.Candles.Enqueue(candle);
            return new Lookback(lb.Period, lb.Candles);
        }

        public static bool IsComplete(this Lookback lb)
            => lb.Period == lb.Count;
    }


    public class TrendContinuation : IRule
    {
        private readonly Candle prevCandle;
        private readonly CandleSentiment prevSentiment;

        public TrendContinuation(Lookback lb)
        {
            this.prevSentiment = CandleSentiment.Of(lb.CurrentCandle);
            this.prevCandle = lb.CurrentCandle;
            Lb = lb;
        }

        public Lookback Lb { get; private set; }

        public IRule Evaluate(Candle candle)
        {
            var lb = Lb.Add(candle);

            var thisSentiment = CandleSentiment.Of(candle);
            if (thisSentiment != prevSentiment)
                return new LookbackEvaluator(Lb).Evaluate(candle);

            if (prevSentiment == CandleSentiment.Green && prevCandle.Ohlc.High < candle.Ohlc.Close)
                return new TrendReversalRule(candle, lb);

            if (prevSentiment == CandleSentiment.Red && prevCandle.Ohlc.Low > candle.Ohlc.Close)
                return new TrendReversalRule(candle, lb);

            return new LookbackEvaluator(Lb).Evaluate(candle);

        }
    }

    public class TrendReversalRule : IRule
    {
        private readonly CandleSentiment prevSentiment;
        private readonly Candle prevCandle;

        public TrendReversalRule(Candle candle, Lookback lb)
        {
            this.prevCandle = candle;
            prevSentiment = CandleSentiment.Of(candle);
            Lb = lb;
        }

        public Lookback Lb { get; private set; }

        public IRule Evaluate(Candle candle)
        {
            var thisSentiment = CandleSentiment.Of(candle);
            if (thisSentiment == prevSentiment)
                return new LookbackEvaluator(Lb).Evaluate(candle);

            if (prevSentiment == CandleSentiment.Green && prevCandle.Ohlc.Low > candle.Ohlc.Close
                && !candle.IsLowerThan(Lb.LowCandle)
                && (candle.Ohlc.Close.Value - Lb.LowCandle.Ohlc.Low.Value) * 100000 > 10)
            {
                //Console.WriteLine($"SOLD: {candle.TimeStamp}");
                return new ProfitLossEvaluator(buy:false, candle, Lb.Add(candle), Lb.HighCandle, Lb.LowCandle);
                //return new CandleSentimentEvaluator().Evaluate(candle);
            }

            if (prevSentiment == CandleSentiment.Red && prevCandle.Ohlc.High < candle.Ohlc.High
                && !candle.IsHigherThan(Lb.HighCandle)
                && (Lb.HighCandle.Ohlc.High.Value - candle.Ohlc.Close.Value) * 100000 > 10)
            {
                //Console.WriteLine($"Bought: {candle.TimeStamp}");
                //return new CandleSentimentEvaluator().Evaluate(candle);
                return new ProfitLossEvaluator(buy: true, candle, Lb.Add(candle), Lb.HighCandle, Lb.LowCandle);
            }

            return new LookbackEvaluator(Lb).Evaluate(candle);

        }


        public class ProfitLossEvaluator : IRule
        {
            private readonly Candle lCandle;
            private readonly Candle hCandle;
            private readonly Candle entryCandle;
            private readonly bool buy;

            public ProfitLossEvaluator(bool buy, Candle candle, Lookback lb, Candle hCandle, Candle lCandle)
            {
                this.buy = buy;
                this.entryCandle = candle;
                Lb = lb;
                this.hCandle = hCandle;
                this.lCandle = lCandle;
            }

            public Lookback Lb { get; }

            public IRule Evaluate(Candle candle)
            {
                if (candle.IsHigherThan(Lb.HighCandle) || candle.IsLowerThan(Lb.LowCandle))
                {
                    var pl = candle.Ohlc.Close - entryCandle.Ohlc.Close;
                    var side = buy ? "BOUGHT" : "SOLD";
                    var sideSymbol = buy ? +1 : -1;
                    Console.WriteLine($"{side},{entryCandle.TimeStamp},{candle.TimeStamp},{sideSymbol * pl.Value * 100000}");
                    return new LookbackEvaluator(Lb).Evaluate(candle);
                }


                return new ProfitLossEvaluator(buy, entryCandle, Lb.Add(candle), hCandle, lCandle);
            }

        }
    }
    
}

