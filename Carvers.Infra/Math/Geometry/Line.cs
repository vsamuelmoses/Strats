using System.Windows;

namespace Carvers.Infra.Math.Geometry
{
    public class Line<TX, TY>
    {
        public Line(TX x1, TY y1, TX x2, TY y2)
        {
            Point1 = new Point<TX, TY>(x1, y1);
            Point2 = new Point<TX, TY>(x2, y2);
        }

        public Line(Point<TX, TY> p1, Point<TX, TY> p2)
        {
            Point1 = p1;
            Point2 = p2;
        }
        public Point<TX, TY> Point1 { get; }
        public Point<TX, TY> Point2 { get; }
    }
}
