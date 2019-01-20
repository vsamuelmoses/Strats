using System.Linq;
using System.Windows;
using Carvers.Models;
using ShadowStrengthStrategy.ViewModels;

namespace ShadowStrengthStrategy
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var exceptJpy = CurrencyPair.All().Where(pair => pair.BaseCurrency != Currency.JPY && pair.TargetCurrency != Currency.JPY);
            // DataContext = new ShadowBreakoutDiversified(new [] {CurrencyPair.EURUSD, CurrencyPair.EURAUD, CurrencyPair.EURCAD, CurrencyPair.EURGBP });
            DataContext = new SssViewModel(exceptJpy);
            //DataContext = new SssViewModel(new[] { CurrencyPair.AUDNZD });

        }
    }
}
