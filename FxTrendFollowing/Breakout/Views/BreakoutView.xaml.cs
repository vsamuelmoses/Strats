using FxTrendFollowing.Breakout.ViewModels;
using System.Windows;

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
            DataContext = new BOVm();
        }
    }
}
