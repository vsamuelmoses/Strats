using System.Windows;
using Carvers.IBApi;

namespace Carvers.IB.App
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new IBTWSViewModel(new IBTWS());
        }
    }
}
