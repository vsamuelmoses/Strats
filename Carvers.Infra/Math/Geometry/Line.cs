using System.Windows;

namespace Carvers.Infra.Math.Geometry
{
    public class Line<TX, TY>
    {
        public Line(TX x1, TY y1, TX x2, TY y2)
        {
            Start = new Point<TX, TY>(x1, y1);
            End = new Point<TX, TY>(x2, y2);
        }

        public Line(Point<TX, TY> p1, Point<TX, TY> p2)
        {
            Start = p1;
            End = p2;
        }
        public Point<TX, TY> Start { get; }
        public Point<TX, TY> End { get; }
    }

    public class VectorLine
    {
        public VectorLine()
        {
            Vector v = new Vector(10, 10);
        }
    }
}
