using Carvers.Charting.ViewModels;
using Carvers.IB.App;
using Carvers.IBApi;
using Carvers.IBApi.Extensions;
using Carvers.Infra.Extensions;
using Carvers.Infra.ViewModels;
using Carvers.Models;
using Carvers.Models.Events;
using Carvers.Models.Indicators;
using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using System.Windows.Threading;

namespace FxTrendFollowing.Breakout.ViewModels
{
    public class BreakoutViewModel : ViewModel
    {
        public BreakoutViewModel()
        {
            Ibtws = new IBTWSSimulator(Utility.FxFilePathGetter,
                new DateTimeOffset(2017, 01, 01, 0, 0, 0, TimeSpan.Zero));
            //Ibtws = new IBTWSSimulator((cxPair, dt) => Utility.FxIBDATAPathGetter(cxPair), new DateTimeOffset(2018, 04, 24, 0, 0, 0, TimeSpan.Zero));
            IbtwsViewModel = new IBTWSViewModel(Ibtws);

            var interestedPairs = new[] {CurrencyPair.EURAUD};


            IRule ruleChain = new LookbackEvaluator(new Lookback(30, new ConcurrentQueue<Candle>()));
            //TODO: for manual feed, change the candle span
            Ibtws.RealTimeBarStream.Subscribe(msg =>
            {
                ruleChain = ruleChain.Evaluate(msg.ToCandle(TimeSpan.FromMinutes(1)));
                //CurrencyPairDataVMs.Foreach(pairVm => pairVm.Add(msg, options.CandleFeedInterval));

            });

            StartCommand = new RelayCommand(_ =>
            {
                Ibtws.AddRealtimeDataRequests(interestedPairs
                    .Select(pair => Tuple.Create(pair.UniqueId, ContractCreator.GetCurrencyPairContract(pair)))
                    .ToList());
            });
        }

        public ICommand StartCommand { get; }
        public IBTWSSimulator Ibtws { get; }
        public IBTWSViewModel IbtwsViewModel { get; }
        public Strategy Strategy { get; }
    }





    public interface IRule
    {
        IRule Evaluate(Candle candle);
    }

    public class BOVm : ViewModel
    {
        private string _status;

        public BOVm()
        {

            Ibtws = new IBTWSSimulator(Utility.FxFilePathGetter,
                new DateTimeOffset(2017, 01, 01, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2017, 01, 31, 0, 0, 0, TimeSpan.Zero));
            //Ibtws = new IBTWSSimulator((cxPair, dt) => Utility.FxIBDATAPathGetter(cxPair), new DateTimeOffset(2018, 04, 24, 0, 0, 0, TimeSpan.Zero));
            IbtwsViewModel = new IBTWSViewModel(Ibtws);

            var interestedPairs = new[] {CurrencyPair.GBPUSD};

            Strategy = new Strategy("MomentumBO");
            var context = new StrategyContext(Strategy, new Lookback(5 * 480, new ConcurrentQueue<Candle>()),
                ImmutableList.Create<IContextInfo>(new[] {new EmptyContext()}));
            var nextCondition = BOStrategyWithFractals.Strategy;

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


            var logReport = new StrategyLogReport(new[] {Strategy}, logName: "MoBo");
            var chartReport = new StrategyChartReport(new[] {Strategy}, Dispatcher.CurrentDispatcher);
            var summaryReport = new StrategySummaryReport(new[] {Strategy});
            Reporters = new Carvers.Infra.ViewModels.Reporters(logReport, chartReport, summaryReport);

          
            //Strategy
            //    .OpenOrders
            //    .Select(order => (IEvent) new OrderExecutedEvent(order.OrderInfo.TimeStamp, order))
            //    .Merge(
            //        Strategy
            //            .CloseddOrders
            //            .Select(order => (IEvent) new OrderExecutedEvent(order.OrderInfo.TimeStamp, order))));

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
    }


    public static class BOStrategy
    {
        private static Func<FuncCondition<StrategyContext>> contextReadyCondition = () =>
            new FuncContextReadyCondition(
                onSuccess: trendContinuationRule,
                onFailure: contextReadyCondition);

        private static Func<FuncCondition<StrategyContext>> trendContinuationRule = () =>
            new FuncTrendContinuationCondition(
                onSuccess: trendReversalRule,
                onFailure: trendContinuationRule);

        private static Func<FuncCondition<StrategyContext>> trendReversalRule = () =>
            new FuncTrendReversalCondition(
                onSuccess: exitRule,
                onFailure: trendContinuationRule);

        private static Func<FuncCondition<StrategyContext>> exitRule = () =>
            new FuncExitCondition(
                onSuccess: trendContinuationRule,
                onFailure: exitRule);



        public static Func<FuncCondition<StrategyContext>> Strategy = contextReadyCondition;
    }


    public class BoStrategyOptions
    {
        public BoStrategyOptions(double fractolStrength, double target, double lbMovementInPips)
        {
            Target = target;
            FractolStrength = fractolStrength;
            LbMovementInPips = lbMovementInPips;
        }

        public double Target { get; }
        public double FractolStrength { get; }
        public double LbMovementInPips { get; }
    }


    public static class BOStrategyWithFractals
    {
        public static BoStrategyOptions Options = new BoStrategyOptions(10d / 100000, 50d / 100000, 50d / 100000);


        private static Func<FuncCondition<StrategyContext>> contextReadyCondition = () =>
            new FuncContextReadyCondition(
                onSuccess: fractalCondition,
                onFailure: contextReadyCondition);

        private static Func<FuncCondition<StrategyContext>> fractalCondition = () =>
            new FuncFractalCondition(
                fractolStrength: Options.FractolStrength,
                lbMovement: Options.LbMovementInPips,
                onSuccess: exitRule,
                onFailure: fractalCondition,
                onSuccessAction: ctx =>
                {

                    if (BooleanIndicators.GetFractolIndicator(ctx.Lb, 5, Options.FractolStrength) == Fractol.Bearish)
                    {
                        var fractolContextInfo = new FractolContextInfo(
                            BooleanIndicators.GetFractolIndicator(ctx.Lb, 5, Options.FractolStrength),
                            ctx.Lb.Candles.TakeLast(5).ToList());

                        return ctx.PlaceOrder(ctx.Lb.LastCandle, Side.ShortSell)
                            .AddContextInfo(fractolContextInfo);
                    }

                    if (BooleanIndicators.GetFractolIndicator(ctx.Lb, 5, Options.FractolStrength) == Fractol.Bullish)
                    {
                        var fractolContextInfo = new FractolContextInfo(
                            BooleanIndicators.GetFractolIndicator(ctx.Lb, 5, Options.FractolStrength),
                            ctx.Lb.Candles.TakeLast(5).ToList());

                        return ctx.PlaceOrder(ctx.Lb.LastCandle, Side.Buy)
                            .AddContextInfo(fractolContextInfo);
                    }

                    return ctx;
                });

        //private static Func<FuncCondition<StrategyContext>> exitOnStaticTarget = () =>
        //   new FuncExitOnTargetCondition(Options.Target,
        //       onSuccess: fractalCondition,
        //       onFailure: exitOnStaticTarget);

        private static Func<FuncCondition<StrategyContext>> exitRule = () =>
            new FuncExitCondition(
                onSuccess: fractalCondition,
                onFailure: exitRule);


        public static Func<FuncCondition<StrategyContext>> Strategy = contextReadyCondition;
    }


    
}

