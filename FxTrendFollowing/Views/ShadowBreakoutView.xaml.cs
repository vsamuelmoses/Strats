using System.Linq;
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
            var exceptJpy = CurrencyPair.All().Where(pair => pair.BaseCurrency != Currency.JPY && pair.TargetCurrency != Currency.JPY);
            // DataContext = new ShadowBreakoutDiversified(new [] {CurrencyPair.EURUSD, CurrencyPair.EURAUD, CurrencyPair.EURCAD, CurrencyPair.EURGBP });
            //DataContext = new ShadowBreakoutDiversified(new[] { CurrencyPair.EURUSD });
            DataContext = new ShadowBreakoutDiversified(exceptJpy);
        }
    }
}
