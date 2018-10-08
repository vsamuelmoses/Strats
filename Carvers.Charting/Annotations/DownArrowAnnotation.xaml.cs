using SciChart.Charting.Visuals.Annotations;
using System.Windows;
using System.Windows.Media;

namespace Carvers.Charting.Annotations
{
    /// <summary>
    /// Interaction logic for ArrowAnnotation.xaml
    /// </summary>
    public partial class DownArrowAnnotation : CustomAnnotationForMvvm
    {
        public DownArrowAnnotation()
        {
            InitializeComponent();
        }

        public Brush FillBrush
        {
            get { return (Brush)GetValue(FillBrushProperty); }
            set { SetValue(FillBrushProperty, value); }
        }

        public static readonly DependencyProperty FillBrushProperty =
            DependencyProperty.Register("FillBrush", typeof(Brush), typeof(DownArrowAnnotation), new PropertyMetadata(new SolidColorBrush(Colors.Orange), OnFillBrushChanged));

        private static void OnFillBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DownArrowAnnotation)d).arrowPath.Fill = (Brush)e.NewValue;
            ((DownArrowAnnotation)d).arrowPath.Stroke = (Brush)e.NewValue;
        }
    }
}
