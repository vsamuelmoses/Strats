using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Carvers.Models;

namespace Carvers.Infra.ViewModels
{

    public class StrategySummaryReport : ViewModel
    {
        public StrategySummaryReport(IEnumerable<IStrategy> strategies)
        {
            var closedOrders = strategies
                .Select(strat => strat.CloseddOrders)
                .Merge()
                .Subscribe(info => Report(strategies));
        }

        private void Report(IEnumerable<IStrategy> stockData)
        {
            var orders = stockData.SelectMany(stock => stock.ClosedOrders).ToList();
            TotalTrades = orders.Count();
            ProfitableTrades = orders.Count(o => o.ProfitLoss > Price.ZeroUSD);
            LoosingTrades = orders.Count(o => o.ProfitLoss < Price.ZeroUSD);

            if (orders.Any())
                WinningPercentage = (profitableTrades * 100) / orders.Count();

            ProfitLoss = orders.ProfitLoss();
            SharpeRatio = SharpeRatioCalculator.Calculate(orders);
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
            set { profitLoss = value; OnPropertyChanged(); }
        }


        private double sharpeRatio;

        public double SharpeRatio
        {
            get { return sharpeRatio; }
            set { sharpeRatio = value; OnPropertyChanged(); }
        }



    }
}