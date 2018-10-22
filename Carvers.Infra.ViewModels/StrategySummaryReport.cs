using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Carvers.Models;
using Carvers.Models.Events;

namespace Carvers.Infra.ViewModels
{

    public class StrategySummaryReport : ViewModel
    {
        private readonly Subject<DateTimeEvent<Price>> _profitLossStream;

        public StrategySummaryReport(IEnumerable<IStrategy> strategies)
        {
            _profitLossStream = new Subject<DateTimeEvent<Price>>();
            var closedOrders = strategies
                .Select(strat => strat.CloseddOrders)
                .Merge()
                .Subscribe(recentClosedOrder => Report(recentClosedOrder, strategies));
        }

        private void Report(IOrder recentClosedOrder, IEnumerable<IStrategy> stockData)
        {
            var orders = stockData.SelectMany(stock => stock.ClosedOrders).ToList();
            TotalTrades = orders.Count();
            ProfitableTrades = orders.Count(o => o.ProfitLoss > Price.ZeroUSD);
            LoosingTrades = orders.Count(o => o.ProfitLoss < Price.ZeroUSD);

            if (orders.Any())
                WinningPercentage = (profitableTrades * 100) / orders.Count();

            ProfitLoss = orders.ProfitLoss();
            SharpeRatio = SharpeRatioCalculator.Calculate(orders);

            _profitLossStream.OnNext(new DateTimeEvent<Price>(recentClosedOrder.OrderInfo.TimeStamp, ProfitLoss));
        }

        private int totalTrades;
        public int TotalTrades
        {
            get { return totalTrades; }
            set { totalTrades = value; OnPropertyChanged(); }
        }

        private int profitableTrades;
        public int ProfitableTrades
        {
            get { return profitableTrades; }
            set { profitableTrades = value; OnPropertyChanged(); }
        }

        private int loosingTrades;
        public int LoosingTrades
        {
            get { return loosingTrades; }
            set { loosingTrades = value; OnPropertyChanged(); }
        }


        private double winningPercentage;

        public double WinningPercentage
        {
            get { return winningPercentage; }
            set { winningPercentage = value; OnPropertyChanged(); }
        }


        private Price profitLoss;

        public Price ProfitLoss
        {
            get { return profitLoss; }
            set
            {
                profitLoss = value;
                OnPropertyChanged();
                
            }
        }


        private double sharpeRatio;

        public double SharpeRatio
        {
            get { return sharpeRatio; }
            set { sharpeRatio = value; OnPropertyChanged(); }
        }

        public IObservable<DateTimeEvent<Price>> ProfitLossStream => _profitLossStream;

    }
}