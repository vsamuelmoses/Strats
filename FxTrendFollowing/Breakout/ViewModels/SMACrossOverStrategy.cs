using Carvers.Models;
using Carvers.Models.Indicators;
using System;
using System.Collections.Generic;

namespace FxTrendFollowing.Breakout.ViewModels
{
    public static class SMACrossOverStrategy
    {
        private const string InterestedSMA = Indicators.CloseSma14;

        private static Func<FuncCondition<SMAContext>> contextReadyCondition = () =>
            new FuncCondition<SMAContext>(
                onSuccess: entryCondition,
                onFailure: contextReadyCondition,
                predicate: context => context.IsReady());

        private static Func<FuncCondition<SMAContext>> entryCondition = () =>
            new FuncCondition<SMAContext>(
                onSuccess: exitCondition,
                onFailure: entryCondition,
                predicates: new List<Func<SMAContext, bool>>()
                {
                    {
                        ctx =>
                        {
                            //var sma3600LineMax = ctx.Sma3600.Averages.TakeLast(3).Select(p => p + 0.00120).GetLine(3);
                            //var exma50 = ctx.ExMa50.Averages.GetLine(3);
                            //var sma3600LineMin = ctx.Sma3600.Averages.TakeLast(3).Select(p => p - 0.00120).GetLine(3);

                            //var isCrossed = !sma3600LineMax.HasSameStartPoint(exma50)
                            //                && !sma3600LineMax.HasSameEndPoint(exma50)
                            //                && sma3600LineMax.IntersectionPoint(exma50).Item2
                            //                && ctx.ExMa50.Current > ctx.Sma3600.CurrentValue
                            //                && ctx.LastCandle.Close > ctx.Sma3600.CurrentValue + 0.00120;
                            //                //&& ctx.LastCandle.Close < ctx.Sma3600.Current + 0.00120;// + 0.00050;

                            //if (!isCrossed)
                            //    isCrossed = !sma3600LineMin.HasSameStartPoint(exma50)
                            //                && !sma3600LineMin.HasSameEndPoint(exma50)
                            //                && sma3600LineMin.IntersectionPoint(exma50).Item2
                            //                && ctx.ExMa50.Current < ctx.Sma3600.CurrentValue
                            //                && ctx.LastCandle.Close < ctx.Sma3600.CurrentValue - 0.00120;
                            //                //&& ctx.LastCandle.Close > ctx.Sma3600.Current - 0.00120;// - 0.00050;





                            ////if(isCrossed 
                            ////   && (ctx.Sma3600.Averages.Last() < ctx.ExMa50.Averages.Last())
                            ////   && ctx.LastCandle.Close > ctx.Sma3600.Current
                            ////   && ctx.Sma3600.Current == ctx.Sma3600.Averages.Max())
                            ////return true;


                            ////if(isCrossed
                            ////   && (ctx.Sma3600.Averages.Last() > ctx.ExMa50.Averages.Last())
                            ////   && ctx.LastCandle.Close < ctx.Sma3600.Current
                            ////   && ctx.Sma3600.Current == ctx.Sma3600.Averages.Min())
                            ////    return true;


                            ////var smas = new double[]
                            ////{

                            ////    ctx.Sma50.Current,
                            ////    ctx.Sma100.Current,
                            ////    ctx.Sma250.Current,
                            ////    ctx.Sma500.Current,
                            ////    ctx.Sma1000.Current,
                            ////    ctx.Sma3600.Current,
                            ////};

                            ////if (isCrossed
                            ////   && (ctx.Sma3600.Averages.Last() < ctx.ExMa3600.Averages.Last())
                            ////   && ctx.Sma100.Current > ctx.Sma3600.Current)
                            ////    return true;

                            ////if (isCrossed
                            ////    && (ctx.Sma3600.Averages.Last() > ctx.ExMa3600.Averages.Last())
                            ////    && ctx.Sma100.Current < ctx.Sma3600.Current)
                            ////    return true;


                            if ((ctx.LastCandle.Low < ctx.Indicators[InterestedSMA].OfType<MovingAverage>().Value 
                                 && ctx.LastCandle.Close > ctx.Indicators[InterestedSMA].OfType<MovingAverage>().Value
                                 &&  CandleSentiment.Of(ctx.LastCandle) == CandleSentiment.Red)
                                || (ctx.LastCandle.High > ctx.Indicators[InterestedSMA].OfType<MovingAverage>().Value 
                                    && ctx.LastCandle.Close < ctx.Indicators[InterestedSMA].OfType<MovingAverage>().Value 
                                    &&  CandleSentiment.Of(ctx.LastCandle) == CandleSentiment.Green))
                                return true;


                            return false;

                        }
                    }
                },
                onSuccessAction: ctx =>
                {
                    if (ctx.Indicators[InterestedSMA].OfType<MovingAverage>().Value < ctx.LastCandle.Close)
                        return ctx.PlaceOrder(ctx.LastCandle, Side.ShortSell);

                    if (ctx.Indicators[InterestedSMA].OfType<MovingAverage>().Value > ctx.LastCandle.Close)
                        return ctx.PlaceOrder(ctx.LastCandle, Side.Buy);

                    throw new Exception("Unexpected");
                });

        private static Func<FuncCondition<SMAContext>> exitCondition = () =>
            new FuncCondition<SMAContext>(
                onSuccess: entryCondition,
                onFailure: exitCondition,
                predicates: new List<Func<SMAContext, bool>>()
                {
                    {
                        /* When Moving averages cross */
                        ctx =>
                        {
                            //if (Math.Abs(ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle, 100000)) >= 250)
                            //    return true;

                            //var stopLoss = ((SmaContextInfo) ctx.ContextInfo).StopLossLimit.Item2 + 0.00050;
                            var openPrice = ctx.Strategy.OpenOrder.OrderInfo.Price.Value;
                            var stopLossPips = 8 * ctx.Indicators[Indicators.CandleBodySma5].OfType<MovingAverage>().Value;
                            var takeProfitPips = 8 * ctx.Indicators[Indicators.CandleBodySma5].OfType<MovingAverage>().Value;

                            if (ctx.Strategy.OpenOrder is BuyOrder)
                            {
                                var pl = ctx.LastCandle.Close - openPrice;

                                if (pl < -1* stopLossPips || pl > takeProfitPips)
                                    return true;
                            }

                            if (ctx.Strategy.OpenOrder is ShortSellOrder)
                            {
                                var pl = openPrice - ctx.LastCandle.Close;

                                if (pl < -1* stopLossPips || pl > takeProfitPips)
                                    return true;
                            }

                            return false;
                        }


                        /* Target of Pips */
                        //ctx =>
                        //{
                        //    if (ctx.Strategy.OpenOrder is BuyOrder)
                        //    {
                        //        var target = (ctx.LastCandle.Close - ctx.Strategy.OpenOrder.OrderInfo.Price.Value) * 100000;
                        //        return (target >= 40) || target <= -20;
                        //    }

                        //    if (ctx.Strategy.OpenOrder is ShortSellOrder)
                        //    {
                        //        var target = (ctx.Strategy.OpenOrder.OrderInfo.Price.Value - ctx.LastCandle.Close) * 100000;
                        //        return (target >= 40) || target <= -20;
                        //    }

                        //    return false;
                        //}


                        //ctx =>
                        //{
                        //    if (ctx.Strategy.OpenOrder is BuyOrder)
                        //        return (ctx.LastCandle.Close < ctx.Sma3600.Current);


                        //    if (ctx.Strategy.OpenOrder is ShortSellOrder)
                        //        return (ctx.LastCandle.Close > ctx.Sma3600.Current);

                        //    throw new Exception("Unexpected Errr");

                        //}
                    }
                },
                onSuccessAction: ctx =>
                {
                    if (ctx.Strategy.OpenOrder is BuyOrder)
                    {
                        var candle = ctx.LastCandle;
                        ctx.Strategy.Close(
                            new SellOrder((BuyOrder)ctx.Strategy.OpenOrder,
                                new OrderInfo(candle.TimeStamp, CurrencyPair.EURGBP, ctx.Strategy, candle.Ohlc.Close, 100000, candle)));
                        return ctx;
                    }

                    if (ctx.Strategy.OpenOrder is ShortSellOrder)
                    {
                        var candle = ctx.LastCandle;
                        ctx.Strategy.Close(
                            new BuyToCoverOrder((ShortSellOrder)ctx.Strategy.OpenOrder,
                                new OrderInfo(candle.TimeStamp, CurrencyPair.EURGBP, ctx.Strategy, candle.Ohlc.Close, 100000, candle)));
                        return ctx;
                    }

                    throw new Exception("Unexpected error");
                });


        public static Func<FuncCondition<SMAContext>> Strategy = contextReadyCondition;
    }


    //public static class SMACrossOverStrategy
    //{


    //    private static Func<FuncCondition<SMAContext>> contextReadyCondition = () =>
    //        new FuncCondition<SMAContext>(
    //            onSuccess: entryCondition,
    //            onFailure: contextReadyCondition,
    //            predicate: context => context.IsReady());

    //    private static Func<FuncCondition<SMAContext>> entryCondition = () =>
    //        new FuncCondition<SMAContext>(
    //            onSuccess: exitCondition,
    //            onFailure: entryCondition,
    //            predicates: new List<Func<SMAContext, bool>>()
    //            {
    //                {
    //                    ctx =>
    //                    {
    //                        var sma3600Line = ctx.Sma3600.Averages.GetLine(3);
    //                        var sma100Line = ctx.Sma100.Averages.GetLine(3);

    //                        var isCrossed = !sma3600Line.HasSameStartPoint(sma100Line)
    //                               && !sma3600Line.HasSameEndPoint(sma100Line)
    //                               && sma3600Line.IntersectionPoint(sma100Line).Item2;

    //                        if (!isCrossed)
    //                            return false;

    //                        var crossingIsbuySignal = (ctx.Sma3600.Averages.Last() < ctx.Sma100.Averages.Last());
    //                        var crossingIsSellSignal = (ctx.Sma3600.Averages.Last() > ctx.Sma100.Averages.Last());

    //                        var is3600Greater =
    //                            ctx.Sma3600.Averages.Zip(ctx.Sma100.Averages, (v3600, v100) => v3600 > v100);

    //                        if(crossingIsbuySignal)
    //                            return is3600Greater.Count(_ => !_) > (0.8 * is3600Greater.Count());

    //                        if(crossingIsSellSignal)
    //                            return is3600Greater.Count(_ => _) > (0.8 * is3600Greater.Count());

    //                        throw new Exception("Unexpected error");

    //                    }
    //                }
    //            },
    //            onSuccessAction: ctx =>
    //            {
    //                if (ctx.Sma3600.Averages.Last() < ctx.Sma100.Averages.Last())
    //                    return ctx.PlaceOrder(ctx.LastCandle, Side.Buy);

    //                if (ctx.Sma3600.Averages.Last() > ctx.Sma100.Averages.Last())
    //                    return ctx.PlaceOrder(ctx.LastCandle, Side.ShortSell);

    //                throw new Exception("Unexpected");
    //            });

    //    private static Func<FuncCondition<SMAContext>> exitCondition = () =>
    //        new FuncCondition<SMAContext>(
    //            onSuccess: entryCondition,
    //            onFailure: exitCondition,
    //            predicates: new List<Func<SMAContext, bool>>()
    //            {
    //                {
    //                    ctx =>
    //                    {
    //                        var sma250Line = ctx.Sma250.Averages.GetLine(3);
    //                        var sma50Line = ctx.Sma50.Averages.GetLine(3);

    //                        if (ctx.Strategy.OpenOrder is BuyOrder)
    //                        {
    //                            return sma50Line.IntersectionPoint(sma250Line).Item2
    //                                   && ctx.Sma50.Averages.Last() < ctx.Sma250.Averages.Last();
    //                        }

    //                        if (ctx.Strategy.OpenOrder is ShortSellOrder)
    //                        {
    //                            return sma50Line.IntersectionPoint(sma250Line).Item2
    //                                   && ctx.Sma50.Averages.Last() > ctx.Sma250.Averages.Last();
    //                        }

    //                        throw new Exception("Unexpected error");
    //                    }
    //                }
    //            },
    //            onSuccessAction: ctx =>
    //            {
    //                if (ctx.Strategy.OpenOrder is BuyOrder)
    //                {
    //                    var candle = ctx.LastCandle;
    //                    ctx.Strategy.Close(
    //                        new SellOrder((BuyOrder)ctx.Strategy.OpenOrder,
    //                            new OrderInfo(candle.TimeStamp, CurrencyPair.EURGBP, ctx.Strategy, candle.Ohlc.Close, 100000, candle)));
    //                    return ctx;
    //                }

    //                if (ctx.Strategy.OpenOrder is ShortSellOrder)
    //                {
    //                    var candle = ctx.LastCandle;
    //                    ctx.Strategy.Close(
    //                        new BuyToCoverOrder((ShortSellOrder)ctx.Strategy.OpenOrder,
    //                            new OrderInfo(candle.TimeStamp, CurrencyPair.EURGBP, ctx.Strategy, candle.Ohlc.Close, 100000, candle)));
    //                    return ctx;
    //                }

    //                throw new Exception("Unexpected error");
    //            });


    //    public static Func<FuncCondition<SMAContext>> Strategy = contextReadyCondition;
    //}
}
