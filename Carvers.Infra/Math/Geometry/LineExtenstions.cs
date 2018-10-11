using System.Windows;

namespace Carvers.Infra.Math.Geometry
{

    public static class LineExtenstions
    {
        public static Point FindIntersection(this Line<double, double> line1, Line<double, double> line2)
        {
            var s1 = line1.Point1;
            var e1 = line1.Point2;

            var s2 = line2.Point1;
            var e2 = line2.Point2;

            double a1 = e1.Y - s1.Y;
            double b1 = s1.X - e1.X;
            double c1 = a1 * s1.X + b1 * s1.Y;

            double a2 = e2.Y - s2.Y;
            double b2 = s2.X - e2.X;
            double c2 = a2 * s2.X + b2 * s2.Y;

            double delta = a1 * b2 - a2 * b1;
            //If lines are parallel, the result will be (NaN, NaN).
            return delta == 0 ? new Point(double.NaN, double.NaN)
                : new Point((b2 * c1 - b1 * c2) / delta, (a1 * c2 - a2 * c1) / delta);
        }

    }
}
