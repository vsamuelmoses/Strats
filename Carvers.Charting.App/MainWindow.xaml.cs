using System.Windows;
using Carvers.Charting.ViewModels;

namespace Carvers.Charting.App
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            RealtimeCandleStickViewModel = new RealtimeCandleStickChartViewModel();

            DataContext = this;
        }

        public RealtimeCandleStickChartViewModel RealtimeCandleStickViewModel { get; }
    }
}
