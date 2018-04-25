using System.Windows;
using FxTrendFollowing.ViewModels;

namespace FxTrendFollowing.Views
{
    /// <summary>
    /// Interaction logic for FxMRMainWindow.xaml
    /// </summary>
    public partial class FxMRMainWindow : Window
    {
        public FxMRMainWindow()
        {
            InitializeComponent();
            DataContext = new FxMRViewModel();
        }
    }
}
