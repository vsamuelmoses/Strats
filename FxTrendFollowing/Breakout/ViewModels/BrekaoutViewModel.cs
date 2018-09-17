using Carvers.IB.App;
using Carvers.IBApi;
using Carvers.IBApi.Extensions;
using Carvers.Infra.ViewModels;
using Carvers.Models;
using Carvers.Models.Indicators;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Windows.Input;

namespace FxTrendFollowing.Breakout.ViewModels
{
    public class BreakoutViewModel : ViewModel
    {
        public BreakoutViewModel()
        {
            Ibtws = new IBTWSSimulator(Utility.FxFilePathGetter, new DateTimeOffset(2017, 01, 01, 0, 0, 0, TimeSpan.Zero));
            //Ibtws = new IBTWSSimulator((cxPair, dt) => Utility.FxIBDATAPathGetter(cxPair), new DateTimeOffset(2018, 04, 24, 0, 0, 0, TimeSpan.Zero));
            IbtwsViewModel = new IBTWSViewModel(Ibtws);

            var interestedPairs = new[] { CurrencyPair.EURAUD };


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

    public class BOVm
    {
        public BOVm()
        {
            Ibtws = new IBTWSSimulator(Utility.FxFilePathGetter, new DateTimeOffset(2017, 01, 01, 0, 0, 0, TimeSpan.Zero));
            //Ibtws = new IBTWSSimulator((cxPair, dt) => Utility.FxIBDATAPathGetter(cxPair), new DateTimeOffset(2018, 04, 24, 0, 0, 0, TimeSpan.Zero));
            IbtwsViewModel = new IBTWSViewModel(Ibtws);

            var interestedPairs = new[] { CurrencyPair.EURAUD };

            Strategy = new Strategy("MomentumBO");
            var context = new StrategyContext(Strategy, new Lookback(30, new ConcurrentQueue<Candle>()), new EmptyContext());
            var nextCondition = BOStrategy.Strategy;

            //TODO: for manual feed, change the candle span
            Ibtws.RealTimeBarStream.Subscribe(msg =>
            {
                (nextCondition, context) = nextCondition().Evaluate(context, msg.ToCandle(TimeSpan.FromMinutes(1)));
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
}

