using System;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Carvers.IBApi;
using Carvers.IBApi.Extensions;
using Carvers.Models;

namespace Carvers.IB.App.ViewModels
{
    public class RealTimeBarDataViewModel
    {
        public ObservableCollection<Candle> Candles { get; }
        public CurrencyPair Pair { get; }
        public string Symbol => Pair.ToString();
        public int RequestId { get; }

        public RealTimeBarDataViewModel(IEngine ibtws, CurrencyPair pair, int requestId, TimeSpan span)
        {
            Candles = new ObservableCollection<Candle>();
            Pair = pair;
            RequestId = requestId;
            ibtws.RealTimeBarStream
                .Where(msg => msg.RequestId == requestId)
                .Subscribe(msg => Candles.Add(msg.ToCandle(span)));
        }
    }

}
