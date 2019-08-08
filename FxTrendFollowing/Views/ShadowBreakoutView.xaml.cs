using System;
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


            Console.WriteLine(string.Join(",", CurrencyPair.All().Select(p => $"\"{p}\"")));//.ForEach(p => Console.WriteLine(p)));

            var exceptJpy = CurrencyPair.All()
                .Where(pair => pair.BaseCurrency != Currency.JPY && pair.TargetCurrency != Currency.JPY);
                //.Where(pair => pair.BaseCurrency != Currency.GBP && pair.TargetCurrency != Currency.GBP);
            

            // DataContext = new ShadowBreakoutDiversified(new [] {CurrencyPair.EURUSD, CurrencyPair.EURAUD, CurrencyPair.EURCAD, CurrencyPair.EURGBP });
            //DataContext = new ShadowBreakoutDiversified(new[] { CurrencyPair.EURUSD });
            DataContext = new CurrencyStrengthStrategy(exceptJpy);
        }
    }
}
