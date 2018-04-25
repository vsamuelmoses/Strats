using System.Windows;

namespace FxTrendFollowing
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Strategy = new StrategyRunner(Dispatcher);
            DataContext = this;

            Strategy.Run();
        }

        public StrategyRunner Strategy { get; set; }
    }
}