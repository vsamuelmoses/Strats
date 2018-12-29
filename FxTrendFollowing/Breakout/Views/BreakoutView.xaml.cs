using System.Collections.Generic;
using System.Linq;
using FxTrendFollowing.Breakout.ViewModels;
using System.Windows;
using System.Windows.Input;
using Carvers.Infra.Extensions;
using Carvers.Infra.ViewModels;
using Carvers.Models;
using FxTrendFollowing.Strategies;

namespace FxTrendFollowing.Breakout.Views
{
    /// <summary>
    /// Interaction logic for BreakoutView.xaml
    /// </summary>
    public partial class BreakoutView : Window
    {
        public BreakoutView()
        {
            InitializeComponent();

            StrategyVms = CurrencyPair.All().Select(symbol => new CandleStickPatternStrategy(symbol))
                .ToList();

            //StrategyVms = new List<CandleStickPatternStrategy>() {new CandleStickPatternStrategy(CurrencyPair.AUDCHF)};
            StartCommand = new RelayCommand(_ => StrategyVms.Foreach(strat => strat.StartCommand.Execute(_)));
            StopCommand = new RelayCommand(_ => StrategyVms.Foreach(strat => strat.StopCommand.Execute(_)));

            //DataContext = new BreakoutViewModel();
            //DataContext = new BOVm();
            //DataContext = new SimpleBreakout();
            DataContext = this;
        }

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }

        public IEnumerable<CandleStickPatternStrategy> StrategyVms { get; private set; }
    }
}
