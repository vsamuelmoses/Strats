using System.Windows;
using Carvers.Models;
using FxTrendFollowing.Strategies;

namespace FxTrendFollowing.Views
{
    /// <summary>
    /// Interaction logic for ShadowBreakoutView.xaml
    /// </summary>
    public partial class ShadowBreakoutView : Window
    {
        public ShadowBreakoutView()
        {
            InitializeComponent();

            DataContext = new ShadowBreakoutDiversified(new [] {CurrencyPair.EURUSD, CurrencyPair.EURAUD, CurrencyPair.EURCAD, CurrencyPair.EURGBP });
        }
    }
}
