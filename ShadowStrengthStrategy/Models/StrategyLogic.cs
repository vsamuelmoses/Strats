using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Carvers.Models;
using Carvers.Models.Extensions;
using Carvers.Models.Indicators;

namespace ShadowStrengthStrategy.Models
{
    public static class StrategyLogic
    {
        private static Func<FuncCondition<SssContext>> contextReadyCondition = () =>
            new FuncCondition<SssContext>(
                onSuccess: didPriceHitSR,
                onFailure: contextReadyCondition,
                predicate: context => context.IsReady().ToPredicateResult());

        private static Func<FuncCondition<SssContext>> didPriceHitSR = () =>
            new FuncCondition<SssContext>(
                onSuccess: exitCondition,
                onFailure: didPriceHitSR,
                predicates: new List<Func<SssContext, PredicateResult>>()
                {
                    {
                        ctx =>
                        {
                            if(ctx.Strategy.OpenOrder != null)
                                return PredicateResult.Fail;

                            var shadowCandle = (ctx.ShadowCandles.LastOrDefault(sh => sh.TimeStamp.Date < ctx.LastCandle.TimeStamp.Date));

                            //var shadowCandle = ctx.Indicators.OfType<ShadowCandleFeed>().Single().ShadowCandle;
                            var shadowStrengthFeed = ctx.Indicators.OfType<ShadowStrengthFeed>().Single();

                            var shouldBuy = shadowStrengthFeed.ShadowStrengths.All(v => v.Val > shadowCandle.MiddlePoint() && v.Val < shadowCandle.High)
                                            && Math.Abs(shadowStrengthFeed.ShadowStrength.GetValueOrDefault() - ctx.LastCandle.Close) >= 0.0010
                                            && ctx.LastCandle.Close < shadowCandle.MiddlePoint();

                            var shouldSell = shadowStrengthFeed.ShadowStrengths.All(v => v.Val < shadowCandle.MiddlePoint() && v.Val > shadowCandle.Low)
                                             && Math.Abs(shadowStrengthFeed.ShadowStrength.GetValueOrDefault() - ctx.LastCandle.Close) >= 0.0010
                                             && ctx.LastCandle.Close > shadowCandle.MiddlePoint();

                            if (ctx.LastCandle.TimeStamp.DateTime == new DateTime(2017, 01, 10, 06, 0, 0))
                            {
                                Debug.WriteLine($"ShadowStrengths: {string.Join(",", shadowStrengthFeed.ShadowStrengths.Select(s => s.Val))}");
                                Debug.WriteLine($"ShadowCandle: {shadowCandle.ToCsv()}");
                                Debug.WriteLine($"ShadowMidPoint: {shadowCandle.MiddlePoint()}");
                                Debug.WriteLine($"Close: {ctx.LastCandle.Close}");
                            }


                            return (shouldBuy || shouldSell).ToPredicateResult();
                        }
                    }
                },
                onSuccessAction: ctx =>
                {
                    if (ctx.Strategy.OpenOrder != null)
                    {
                        ctx.LogFile.Write($"Could not place the order: Open Order exists, {ctx.Strategy.OpenOrder.ToCsv()}");
                        return ctx;
                    }
                    var shadowStrengthFeed = ctx.Indicators.OfType<ShadowStrengthFeed>().Single();
                    var shadowCandle = (ctx.ShadowCandles.LastOrDefault(sh => sh.TimeStamp.Date < ctx.LastCandle.TimeStamp.Date));
                    
                    var shouldBuy = shadowStrengthFeed.ShadowStrengths.All(v => v.Val > shadowCandle.MiddlePoint() && v.Val < shadowCandle.High)
                                    && ctx.LastCandle.Close < shadowCandle.MiddlePoint();

                    var shouldSell = shadowStrengthFeed.ShadowStrengths.All(v => v.Val < shadowCandle.MiddlePoint() && v.Val > shadowCandle.Low)
                                     && ctx.LastCandle.Close > shadowCandle.MiddlePoint();


                    var side = shouldBuy ? Side.Buy : Side.ShortSell;
                    var entryPrice = ctx.LastCandle.Close;
                    ctx = ctx.PlaceOrder(ctx.LastCandle.TimeStamp, ctx.LastCandle, entryPrice, side);
                    return ctx;
                });

        private static Func<FuncCondition<SssContext>> exitCondition = () =>
            new FuncCondition<SssContext>(
                onSuccess: didPriceHitSR,
                onFailure: exitCondition,
                predicates: new List<Func<SssContext, PredicateResult>>()
                    { { ctx => PredicateResult.Success } },
                onSuccessAction: ctx =>
                {
                    if (ctx.Strategy.OpenOrder is BuyOrder)
                    {
                        var pl = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.High, 1);
                        var candle = ctx.LastCandle;
                        //var exitPrice = pl > 0.0040
                        //    ? ctx.Strategy.OpenOrder.OrderInfo.Price + 0.0040d.USD()
                        //    : ctx.LastCandle.Close.USD();
                        var exitPrice = ctx.LastCandle.Close.USD();
                        var closedOrder = new SellOrder((BuyOrder)ctx.Strategy.OpenOrder,
                            new OrderInfo(candle.TimeStamp, ctx.Instrument, ctx.Strategy, exitPrice,
                                100000, candle));
                        ctx.Strategy.Close(closedOrder);
                        //return ctx.ReplaceContextInfo(new RecentOrderInfo(closedOrder));
                        return ctx;
                    }

                    if (ctx.Strategy.OpenOrder is ShortSellOrder)
                    {
                        var pl = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.Low, 1);
                        var candle = ctx.LastCandle;
                        //var exitPrice = pl > 0.0040
                        //    ? ctx.Strategy.OpenOrder.OrderInfo.Price - 0.0040d.USD()
                        //    : ctx.LastCandle.Close.USD();

                        var exitPrice = ctx.LastCandle.Close.USD();

                        var closedOrder = new BuyToCoverOrder((ShortSellOrder)ctx.Strategy.OpenOrder,
                            new OrderInfo(candle.TimeStamp, ctx.Instrument, ctx.Strategy, exitPrice,
                                100000, candle));
                        ctx.Strategy.Close(closedOrder);
                        //return ctx.ReplaceContextInfo(new RecentOrderInfo(closedOrder));
                        return ctx;
                    }

                    throw new Exception("Unexpected error");
                });

        public static Func<FuncCondition<SssContext>> Strategy = contextReadyCondition;
    }
}