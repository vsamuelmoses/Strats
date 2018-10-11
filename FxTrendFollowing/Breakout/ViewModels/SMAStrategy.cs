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

namespace FxTrendFollowing.Breakout.ViewModels
{
    public class SMAStrategyViewModel : ViewModel
    {
        private string _status;

        public SMAStrategyViewModel()
        {

            Ibtws = new IBTWSSimulator(Utility.FxFilePathGetter,
                new DateTimeOffset(2017, 01, 01, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2017, 08, 31, 0, 0, 0, TimeSpan.Zero));
            //Ibtws = new IBTWSSimulator((cxPair, dt) => Utility.FxIBDATAPathGetter(cxPair), new DateTimeOffset(2018, 04, 24, 0, 0, 0, TimeSpan.Zero));
            IbtwsViewModel = new IBTWSViewModel(Ibtws);

            var interestedPairs = new[] { CurrencyPair.GBPUSD };

            Strategy = new Strategy("Simple Breakout");
            var context = new SMAContext(new MovingAverage(50), new MovingAverage(100), new MovingAverage(250), new MovingAverage(3600));

            var nextCondition = SMAStrategy.Strategy;

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
                    .Select(pair => Tuple.Create(pair.UniqueId, ContractCreator.GetCurrencyPairContract(pair)))
                    .ToList());
            });

            StopCommand = new RelayCommand(_ => { Strategy.Stop(); });


            var logReport = new StrategyLogReport(new[] { Strategy }, logName: "MoBo");
            var chartReport = new StrategyChartReport(new[] { Strategy }, Dispatcher.CurrentDispatcher);
            var summaryReport = new StrategySummaryReport(new[] { Strategy });
            Reporters = new Carvers.Infra.ViewModels.Reporters(logReport, chartReport, summaryReport);

            ChartVm = new RealtimeCandleStickChartViewModel(candleStream,
                Strategy.OpenOrders
                        .Select(order => (IEvent)new OrderExecutedEvent(order.OrderInfo.TimeStamp, order))
                    .Merge(Strategy.CloseddOrders
                            .Select(order => (IEvent)new OrderExecutedEvent(order.OrderInfo.TimeStamp, order))));
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
        public RealtimeCandleStickChartViewModel ChartVm { get; }
    }

    public static class SMAStrategy
    {
        private static Func<FuncCondition<SMAContext>> contextReadyCondition = () =>
            new FuncCondition<SMAContext>(
                onSuccess: entryCondition,
                onFailure: contextReadyCondition,
                predicate: context => context.IsReady());

        private static Func<FuncCondition<SMAContext>> entryCondition = () =>
            new FuncCondition<SMAContext>(
                onSuccess: exitCondition,
                onFailure: entryCondition,
                predicates: new List<Func<StrategyContext, bool>>()
                {
                    {
                        ctx =>
                        {
                            if()
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
                        return context.CloseOrderOnTargetReached(context.Infos.OfType<SimpleBreakoutEntryContext>().Single(), target);
                    }

                    if (context.Strategy.OpenOrder is ShortSellOrder)
                    {
                        var target = entryContext.HCandle.High - entryContext.EntryCandle.Close;
                        return context.CloseOrderOnTargetReached(context.Infos.OfType<SimpleBreakoutEntryContext>().Single(), target);
                    }

                    throw new Exception("Unexpected error");
                });


        public static Func<FuncCondition<StrategyContext>> Strategy = contextReadyCondition;
    }

    public class SMAContext : IContext
    {
        private List<MovingAverage> smas;
        public SMAContext(MovingAverage sma50, 
            MovingAverage sma100,
            MovingAverage sma250,
            MovingAverage sma3600)
        {
            Sma50 = sma50;
            Sma100 = sma100;
            Sma250 = sma250;
            Sma3600 = sma3600;

            smas = new List<MovingAverage> { Sma50, Sma100, Sma250, Sma3600 };

        }

        public SMAContext Update(Candle candle)
        {
            smas.Foreach(sma => sma.Push(candle.Close));
            return this;
        }

        public bool IsReady()
            => smas.All(sma => sma.Current != double.NaN);

        public MovingAverage Sma50 { get; private set; }
        public MovingAverage Sma100 { get; private set; }
        public MovingAverage Sma250 { get; private set; }
        public MovingAverage Sma3600 { get; private set; }
    }
}
