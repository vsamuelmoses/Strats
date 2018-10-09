using System.Windows.Media;

namespace Carvers.Charting.Annotations
{
    public class BuyArrowAnnotation : UpArrowAnnotation
    {
        public BuyArrowAnnotation()
        {
            FillBrush = new SolidColorBrush(Colors.Green);
        }
    }
}