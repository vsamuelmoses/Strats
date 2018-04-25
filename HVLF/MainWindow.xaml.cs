using System.Threading;
using System.Windows;

namespace HVLF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Strategy = new StrategyRunner();
            DataContext = this;

            Strategy.Run();
        }

        public StrategyRunner Strategy { get; set; }
    }
}

