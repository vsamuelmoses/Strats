using Carvers.IB.App;
using Carvers.IBApi;
using Carvers.IBApi.Extensions;
using Carvers.Infra.ViewModels;
using Carvers.Models;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Windows.Input;

namespace FxTrendFollowing.Breakout.ViewModels
{
    public class BreakoutViewModel
    {
        public BreakoutViewModel()
        {
            Ibtws = new IBTWSSimulator(Utility.FxFilePathGetter, new DateTimeOffset(2017, 01, 01, 0, 0, 0, TimeSpan.Zero));
            //Ibtws = new IBTWSSimulator((cxPair, dt) => Utility.FxIBDATAPathGetter(cxPair), new DateTimeOffset(2018, 04, 24, 0, 0, 0, TimeSpan.Zero));
            IbtwsViewModel = new IBTWSViewModel(Ibtws);

            var interestedPairs = new[] { CurrencyPair.EURUSD };


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
    }


    public interface IRule
    {
        IRule Evaluate(Candle candle);
        
    }
}

