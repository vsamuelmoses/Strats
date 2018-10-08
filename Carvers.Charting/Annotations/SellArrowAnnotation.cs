using System.Windows.Media;

namespace Carvers.Charting.Annotations
{

    public class SellArrowAnnotation : DownArrowAnnotation
    {
        public SellArrowAnnotation()
        {
            FillBrush = new SolidColorBrush(Colors.Red);
        }
    }
}
