using SciChart.Charting.Visuals.Annotations;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Carvers.Charting.Annotations
{
    /// <summary>
    /// Interaction logic for UpArrowAnnotation.xaml
    /// </summary>
    public partial class UpArrowAnnotation : CustomAnnotationForMvvm
    {
        public UpArrowAnnotation()
        {
            InitializeComponent();
        }

        public Brush FillBrush
        {
            get { return (Brush)GetValue(FillBrushProperty); }
            set { SetValue(FillBrushProperty, value); }
        }

        public static readonly DependencyProperty FillBrushProperty =
            DependencyProperty.Register("FillBrush", typeof(Brush), typeof(UpArrowAnnotation), new PropertyMetadata(new SolidColorBrush(Colors.Orange), OnFillBrushChanged));

        private static void OnFillBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((UpArrowAnnotation)d).arrowPath.Fill = (Brush)e.NewValue;
            ((UpArrowAnnotation)d).arrowPath.Stroke = (Brush)e.NewValue;
        }
    }
}
