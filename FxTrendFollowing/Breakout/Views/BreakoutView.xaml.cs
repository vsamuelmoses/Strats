using FxTrendFollowing.Breakout.ViewModels;
using System.Windows;
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

            //DataContext = new BreakoutViewModel();
            //DataContext = new BOVm();
            //DataContext = new SimpleBreakout();
            DataContext = new CandleStickPatternStrategy();
        }
    }
}
