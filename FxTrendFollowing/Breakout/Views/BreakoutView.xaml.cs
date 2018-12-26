using System.Collections.Generic;
using System.Linq;
using FxTrendFollowing.Breakout.ViewModels;
using System.Windows;
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

            //StrategyVms = CurrencyPair.All().Select(symbol => new CandleStickPatternStrategy(symbol))
            //    .ToList();

            //StrategyVms = new List<CandleStickPatternStrategy>() {new CandleStickPatternStrategy(CurrencyPair.AUDCHF)};


            //DataContext = new BreakoutViewModel();
            //DataContext = new BOVm();
            //DataContext = new SimpleBreakout();
            DataContext = new CandleStickPatternStrategy(CurrencyPair.USDCAD);
        }

        public IEnumerable<CandleStickPatternStrategy> StrategyVms { get; private set; }
    }
}
