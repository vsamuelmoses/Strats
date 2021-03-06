﻿using Carvers.Charting.ViewModels;
using Carvers.IB.App;
using Carvers.IBApi;
using Carvers.IBApi.Extensions;
using Carvers.Infra.Extensions;
using Carvers.Infra.ViewModels;
using Carvers.Models;
using Carvers.Models.Events;
using Carvers.Models.Extensions;
using Carvers.Models.Indicators;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using Carvers.Utilities;

namespace FxTrendFollowing.Breakout.ViewModels
{

    public class SimpleBreakout : ViewModel
    {
        private string _status;

        public SimpleBreakout()
        {

            Ibtws = new IBTWSSimulator(Utility.SymbolFilePathGetter,
                new DateTimeOffset(2017, 01, 01, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2017, 08, 31, 0, 0, 0, TimeSpan.Zero));
            //Ibtws = new IBTWSSimulator((cxPair, dt) => Utility.FxIBDATAPathGetter(cxPair), new DateTimeOffset(2018, 04, 24, 0, 0, 0, TimeSpan.Zero));
            IbtwsViewModel = new IBTWSViewModel(Ibtws);

            var interestedPairs = new[] { CurrencyPair.GBPUSD };

            Strategy = new Strategy("Simple Breakout");
            var context = new StrategyContext(Strategy, new Lookback(4 * 60, new List<Candle>()),
                ImmutableList.Create<IContextInfo>(new[] { new EmptyContext() }));

            var nextCondition = SimpleBreakoutStrategy.Strategy;

            var candleStream = Ibtws.RealTimeBarStream.Select(msg => msg.ToCandle(TimeSpan.FromMinutes(1)));

            //TODO: for manual feed, change the candle span
            Ibtws.RealTimeBarStream.Subscribe(msg =>
            {
                var candle = msg.ToCandle(TimeSpan.FromMinutes(1));
                Status = $"Executing {candle.TimeStamp}";
                (nextCondition, context) = nextCondition().Evaluate(context, candle);
            });

            StartCommand = new RelayCommand(_ =>
            {
                Ibtws.AddRealtimeDataRequests(interestedPairs
                    .Select(pair => Tuple.Create(pair.UniqueId, ContractCreator.GetContract(pair)))
                    .ToList());
            });

            StopCommand = new RelayCommand(_ => { Strategy.Stop(); });


            var logReport = new StrategyLogReport(new[] { Strategy }, logName: "MoBo");
            var chartReport = new StrategyChartReport(new[] { Strategy }, Dispatcher.CurrentDispatcher);
            var summaryReport = new StrategySummaryReport(new[] { Strategy });
            Reporters = new Carvers.Infra.ViewModels.Reporters(logReport, chartReport, summaryReport);

            //ChartVm = new RealtimeCandleStickChartViewModel(candleStream,
            //    summaryReport.ProfitLossStream,
            //    Strategy.OpenOrders
            //            .Select(order => (IEvent)new OrderExecutedEvent(order.OrderInfo.TimeStamp, order))
            //        .Merge(Strategy.CloseddOrders
            //                .Select(order => (IEvent)new OrderExecutedEvent(order.OrderInfo.TimeStamp, order))));
        }

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public IBTWSSimulator Ibtws { get; }
        public IBTWSViewModel IbtwsViewModel { get; }
        public Strategy Strategy { get; }

        public string Status
        {
            get { return _status; }
            private set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        public Carvers.Infra.ViewModels.Reporters Reporters { get; }
        //public RealtimeCandleStickChartViewModel ChartVm { get; }
    }

    public static class SimpleBreakoutStrategy
    {
        public static BoStrategyOptions Options = new BoStrategyOptions(10d / 100000, 50d / 100000, 50d / 100000);


        private static Func<FuncCondition<StrategyContext>> contextReadyCondition = () =>
            new FuncCondition<StrategyContext>(
                onSuccess: entryCondition,
                onFailure: contextReadyCondition,
                predicate: context => context.Lb.IsComplete().ToPredicateResult());

        private static Func<FuncCondition<StrategyContext>> entryCondition = () =>
            new FuncCondition<StrategyContext>(
                onSuccess: exitCondition,
                onFailure: entryCondition,
                predicates: new List<Func<StrategyContext, PredicateResult>>()
                {
                    {
                        ctx =>
                        {

                            Candle lCandle = ctx.Lb.Candles.First();
                            Candle hCandle = ctx.Lb.Candles.First();
                            var lowIndex = 0;
                            var highIndex = 0;
                            var candleIndex = 0;
                            foreach (var c in ctx.Lb.Candles)
                            {
                                if (c == ctx.Lb.LastCandle)
                                    continue;

                                if (c.Low < lCandle.Low)
                                {
                                    lowIndex = candleIndex;
                                    lCandle = c;
                                }

                                if (c.High > hCandle.High)
                                {
                                    highIndex = candleIndex;
                                    hCandle = c;
                                }

                                candleIndex++;
                            }

                            var closedAboveHigh = ctx.Lb.LastCandle.ClosedAboveHigh(hCandle);
                            var closedBelowLow = ctx.Lb.LastCandle.ClosedBelowLow(lCandle);

                            if (!closedAboveHigh && !closedBelowLow)
                                return PredicateResult.Fail;

                            var lbSingleCandle = ctx.Lb.Candles.TakeWhile(c => c != ctx.Lb.LastCandle).ToSingleCandle(TimeSpan.FromHours(4));

                            if(closedAboveHigh && highIndex < ctx.Lb.Count / 3)
                            {
                                var target = ctx.Lb.LastCandle.Close - lbSingleCandle.Low;

                                if(CandleSentiment.Of(lbSingleCandle)  == CandleSentiment.Green
                                    /*&& lbSingleCandle.AbsBodyLength() >= target*/)
                                    return PredicateResult.Success;

                            }

                            if(closedBelowLow && lowIndex < ctx.Lb.Count / 3
                                && CandleSentiment.Of(lbSingleCandle)  == CandleSentiment.Red)
                                return PredicateResult.Success;

                            return PredicateResult.Fail;
                        }
                    }
                },
                onSuccessAction: ctx =>
                {
                    var closedAboveHigh = ctx.Lb.LastCandle == ctx.Lb.HighCandle;
                    var closedBelowLow = ctx.Lb.LastCandle == ctx.Lb.LowCandle;

                    if (closedAboveHigh)
                        return ctx.PlaceOrder(ctx.Lb.LastCandle, Side.Buy)
                            .AddContextInfo(new SimpleBreakoutEntryContext(ctx.Lb.LastCandle,
                                ctx.Lb.Candles.OrderBy(candle => candle.Ohlc.High).TakeLast(2).First(),
                                ctx.Lb.Candles.OrderBy(candle => candle.Ohlc.Low).Take(2).Last()));

                    if (closedBelowLow)
                        return ctx.PlaceOrder(ctx.Lb.LastCandle, Side.ShortSell)
                            .AddContextInfo(new SimpleBreakoutEntryContext(ctx.Lb.LastCandle,
                                ctx.Lb.Candles.OrderBy(candle => candle.Ohlc.High).TakeLast(2).First(),
                                ctx.Lb.Candles.OrderBy(candle => candle.Ohlc.Low).Take(2).Last()));

                    throw new Exception("Unexpected");

                });

        private static Func<FuncCondition<StrategyContext>> exitCondition = () =>
            new FuncCondition<StrategyContext>(
                onSuccess: entryCondition,
                onFailure: exitCondition,
                predicate: context =>
                {
                    var candle = context.Lb.LastCandle;
                    var entryContext = context.Infos.OfType<SimpleBreakoutEntryContext>().Single();
                    if (context.Strategy.OpenOrder is BuyOrder)
                    {
                        var target = entryContext.EntryCandle.Close - entryContext.LCandle.Low;
                        return context.CloseOrderOnTargetReached(context.Infos.OfType<SimpleBreakoutEntryContext>().Single(), target).ToPredicateResult();
                    }

                    if (context.Strategy.OpenOrder is ShortSellOrder)
                    {
                        var target = entryContext.HCandle.High - entryContext.EntryCandle.Close;
                        return context.CloseOrderOnTargetReached(context.Infos.OfType<SimpleBreakoutEntryContext>().Single(), target).ToPredicateResult();
                    }

                    throw new Exception("Unexpected error");
                });


        public static Func<FuncCondition<StrategyContext>> Strategy = contextReadyCondition;
    }

    public class SimpleBreakoutEntryContext : EntryContextInfo
    {
        public SimpleBreakoutEntryContext(Candle entryCandle, Candle hCandle, Candle lCandle)
            : base(entryCandle, hCandle, lCandle)
        {
        }
    }
}