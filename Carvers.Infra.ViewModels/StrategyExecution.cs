using System;
using System.Collections.Generic;
using System.Linq;
using Carvers.Models;
using Carvers.Models.Extensions;

namespace Carvers.Infra.ViewModels
{
    public class StrategyExecution<TStrat, TStratOptions> : ViewModel
        where TStrat: IStrategy
        where TStratOptions : IStrategyOptions
    {
        private Price profitLoss;
        private Reporters reporters;
        public List<TStrat> Strategies { get; }
        public TStratOptions Options { get; }

        public StrategyExecution(TStratOptions options)
        {
            Strategies = new List<TStrat>();
            Options = options;
        }


        public void ComputePL(DateTimeOffset dateTime)
        {
            ProfitLoss = Strategies.Select(strat => OrderExtensions.ProfitLoss(strat.ClosedOrders)).Total();
        }

        public Price ProfitLoss
        {
            get { return profitLoss; }
            private set { profitLoss = value; OnPropertyChanged();}
        }

        public Reporters Reporters
        {
            get => reporters;
            set
            {
                reporters = value;
                OnPropertyChanged();
            }
        }
    }
}