using Carvers.Models;
using Carvers.Models.Extensions;
using Carvers.Models.Indicators;
using System;

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

    public class TrendContinuation : IRule
    {
        private readonly Candle prevCandle;
        private readonly CandleSentiment prevSentiment;

        public TrendContinuation(Lookback lb)
        {
            this.prevSentiment = CandleSentiment.Of(lb.LastCandle);
            this.prevCandle = lb.LastCandle;
            Lb = lb;
        }

        public Lookback Lb { get; private set; }

        public IRule Evaluate(Candle candle)
        {
            var lbEvaluator = new LookbackEvaluator(Lb);
            var nexEvaluator = lbEvaluator.Evaluate(candle);
            var lb = lbEvaluator.Lb;

            var thisSentiment = CandleSentiment.Of(candle);
            if (thisSentiment != prevSentiment)
                return nexEvaluator;

            if (prevSentiment == CandleSentiment.Green 
                && prevCandle.Ohlc.High < candle.Ohlc.Close
                && CandleSentiment.Of(lb.Candles.ToSingleCandle(TimeSpan.FromMinutes(Lb.Period))) == CandleSentiment.Red)
                return new TrendReversalRule(candle, lb);

            if (prevSentiment == CandleSentiment.Red 
                && prevCandle.Ohlc.Low > candle.Ohlc.Close
                && CandleSentiment.Of(lb.Candles.ToSingleCandle(TimeSpan.FromMinutes(Lb.Period))) == CandleSentiment.Green)
                return new TrendReversalRule(candle, lb);

            return nexEvaluator;

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
            var lbEvaluator = new LookbackEvaluator(this.Lb);
            var nextEvaluator = lbEvaluator.Evaluate(candle);
            Lb = lbEvaluator.Lb;


            var thisSentiment = CandleSentiment.Of(candle);
            if (thisSentiment == prevSentiment)
                return nextEvaluator;
            
            if (prevSentiment == CandleSentiment.Green 
                && prevCandle.Ohlc.Low > candle.Ohlc.Close
                && prevCandle.Ohlc.Open > candle.Ohlc.Close
                && !candle.IsLowerThan(Lb.LowCandle)
                && (candle.Ohlc.Close.Value - Lb.LowCandle.Ohlc.Low.Value) * 100000 > 100)
            {
                //Console.WriteLine($"SOLD: {candle.TimeStamp}");
                return new ProfitLossEvaluator(buy:false, candle, Lb, Lb.HighCandle, Lb.LowCandle);
                //return new CandleSentimentEvaluator().Evaluate(candle);
            }

            if (prevSentiment == CandleSentiment.Red 
                && prevCandle.Ohlc.High < candle.Ohlc.High
                && prevCandle.Ohlc.Open < candle.Ohlc.Close
                && !candle.IsHigherThan(Lb.HighCandle)
                && (Lb.HighCandle.Ohlc.High.Value - candle.Ohlc.Close.Value) * 100000 > 100)
            {
                //Console.WriteLine($"Bought: {candle.TimeStamp}");
                //return new CandleSentimentEvaluator().Evaluate(candle);
                return new ProfitLossEvaluator(buy: true, candle, Lb, Lb.HighCandle, Lb.LowCandle);
            }

            return nextEvaluator;

        }


        public class ProfitLossEvaluator : IRule
        {
            private readonly Candle lCandle;
            private readonly Candle hCandle;
            private readonly Candle entryCandle;
            private readonly bool buy;
            private double target;

            public ProfitLossEvaluator(bool buy, Candle candle, Lookback lb, Candle hCandle, Candle lCandle)
            {
                this.buy = buy;
                this.entryCandle = candle;
                Lb = lb;
                this.hCandle = hCandle;
                this.lCandle = lCandle;

                if (buy)
                {
                    var idealTarget = hCandle.Ohlc.High - entryCandle.Ohlc.Close;
                    target = idealTarget.Value / 2;
                    
                }
                else
                {
                    var idealTarget = entryCandle.Ohlc.Close - lCandle.Ohlc.Low;
                    target = idealTarget.Value / 2;
                }

                //if (Math.Abs(target) > 0.00050)
                //    target = 0.00050;

            }

            public Lookback Lb { get; private set; }

            public IRule Evaluate(Candle candle)
            {
                var lbEvaluator = new LookbackEvaluator(Lb);
                var nextEvaluator = lbEvaluator.Evaluate(candle);
                Lb = lbEvaluator.Lb;

               
                if (buy)
                {
                    if (candle.IsLowerThan(entryCandle))
                    {
                        Close(entryCandle.Ohlc.Low.Value, candle);
                        return nextEvaluator;
                    }

                    //if (candle.IsHigherThan(hCandle))
                    if (candle.Ohlc.High - entryCandle.Ohlc.Close > (hCandle.Ohlc.High - entryCandle.Ohlc.Close)* (2/3))
                    {
                        Close(candle.Ohlc.High.Value, candle);
                        return nextEvaluator;
                    }
                }
                if (!buy)
                {
                    if (candle.IsHigherThan(entryCandle))
                    {
                        Close(entryCandle.Ohlc.High.Value, candle);
                        return nextEvaluator;
                    }

                    var tar = (entryCandle.Ohlc.Close - lCandle.Ohlc.Low) * (2 / 3);
                    //if (candle.IsLowerThan(lCandle))
                    if (entryCandle.Ohlc.Close - candle.Ohlc.High > tar)
                    {
                        Close(candle.Ohlc.Low.Value, candle);
                        return nextEvaluator;
                    }
                    
                }


                

                //if (closeTrade || s(Math.Abmovement.Value) >= target)
                //{
                //    var pl = candle.Ohlc.Close - entryCandle.Ohlc.Close;
                //    var side = buy ? "BOUGHT" : "SOLD";
                //    var sideSymbol = buy ? +1 : -1;
                //    Console.WriteLine($"{side},{entryCandle.TimeStamp},{candle.TimeStamp},{sideSymbol * pl.Value * 100000}");
                //    return nextEvaluator;
                //}


                return new ProfitLossEvaluator(buy, entryCandle, Lb, hCandle, lCandle);
            }

            private void Close(double closingPrice, Candle candle)
            {
                var pl = closingPrice - entryCandle.Ohlc.Close.Value;
                var side = buy ? "BOUGHT" : "SOLD";
                var sideSymbol = buy ? +1 : -1;
                Console.WriteLine($"{side},{entryCandle.TimeStamp},{candle.TimeStamp},{sideSymbol * pl * 100000}");
            }

        }
    }
    
}

