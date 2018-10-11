using Carvers.Charting.ViewModels;
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
using System.Threading;
using System.Windows.Input;
using System.Windows.Threading;
using Carvers.Infra.Math.Geometry;

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
            var context = new SMAContext(Strategy, new MovingAverage(50), new MovingAverage(100), new MovingAverage(250), new MovingAverage(3600));

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
                predicates: new List<Func<SMAContext, bool>>()
                {
                    {
                        ctx =>
                        {
                            var sma3600Line = ctx.Sma3600.Buffer.GetLine(3);
                            var sma100Line = ctx.Sma100.Buffer.GetLine(3);

                            return !sma3600Line.HasSameStartPoint(sma100Line)
                                   && !sma3600Line.HasSameEndPoint(sma100Line) 
                                   && sma3600Line.IntersectionPoint(sma100Line).IsSuccess;
                        }
                    }
                },
                onSuccessAction: ctx =>
                {
                    if (ctx.Sma3600.Buffer.Last() < ctx.Sma100.Buffer.Last())
                        return ctx.PlaceOrder(ctx.LastCandle, Side.Buy);

                    if (ctx.Sma3600.Buffer.Last() > ctx.Sma100.Buffer.Last())
                        return ctx.PlaceOrder(ctx.LastCandle, Side.ShortSell);

                    throw new Exception("Unexpected");
                });

        private static Func<FuncCondition<SMAContext>> exitCondition = () =>
            new FuncCondition<SMAContext>(
                onSuccess: entryCondition,
                onFailure: exitCondition,
                predicates: new List<Func<SMAContext, bool>>()
                {
                    {
                        ctx =>
                        {
                            var sma250Line = ctx.Sma250.Buffer.GetLine(3);
                            var sma50Line = ctx.Sma50.Buffer.GetLine(3);

                            if (ctx.Strategy.OpenOrder is BuyOrder)
                            {
                                return sma50Line.IntersectionPoint(sma250Line).IsSuccess
                                       && ctx.Sma50.Buffer.Last() < ctx.Sma250.Buffer.Last();
                            }

                            if (ctx.Strategy.OpenOrder is ShortSellOrder)
                            {
                                return sma50Line.IntersectionPoint(sma250Line).IsSuccess
                                       && ctx.Sma50.Buffer.Last() > ctx.Sma250.Buffer.Last();
                            }

                            throw new Exception("Unexpected error");
                        }
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

    public class SMAContext : IContext
    {
        private readonly List<MovingAverage> smas;
        public SMAContext(Strategy strategy, 
            MovingAverage sma50, 
            MovingAverage sma100,
            MovingAverage sma250,
            MovingAverage sma3600)
        {
            Strategy = strategy;
            Sma50 = sma50;
            Sma100 = sma100;
            Sma250 = sma250;
            Sma3600 = sma3600;

            smas = new List<MovingAverage> { Sma50, Sma100, Sma250, Sma3600 };
        }

        public IContext Add(Candle candle)
        {
            LastCandle = candle;
            smas.Foreach(sma => sma.Push(candle.Close));
            return this;
        }

        public Candle LastCandle { get; private set; }

        public bool IsReady()
            => smas.All(sma => !double.IsNaN(sma.Current));

        public Strategy Strategy { get; }
        public MovingAverage Sma50 { get; private set; }
        public MovingAverage Sma100 { get; private set; }
        public MovingAverage Sma250 { get; private set; }
        public MovingAverage Sma3600 { get; private set; }
    }

    public static class ContextExtensions
    {
        public static SMAContext PlaceOrder(this SMAContext context, Candle lastCandle, Side side)
        {
            if (side == Side.ShortSell)
            {
                context.Strategy.Open(new ShortSellOrder(
                    new OrderInfo(lastCandle.TimeStamp, CurrencyPair.EURGBP, context.Strategy, lastCandle.Ohlc.Close, 100000)));
                return context;
            }

            if (side == Side.Buy)
            {
                context.Strategy.Open(new BuyOrder(
                    new OrderInfo(lastCandle.TimeStamp, CurrencyPair.EURGBP, context.Strategy, lastCandle.Ohlc.Close, 100000)));
                return context;
            }

            throw new Exception("unexpected error");
        }
    }
}
