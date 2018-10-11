using System;
using Carvers.Infra.Result;
using System.Windows;

namespace Carvers.Infra.Math.Geometry
{

    public static class LineExtenstions
    {
        public static bool HasSameStartPoint(this Line<double, double> line1, Line<double, double> line2)
            => line1.Point1 == line2.Point1;

        public static bool HasSameEndPoint(this Line<double, double> line1, Line<double, double> line2)
            => line1.Point2 == line2.Point2;


        public static Result<Point> IntersectionPoint(this Line<double, double> line1, Line<double, double> line2)
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
            return delta == 0 
                ? Result<Point>.ToFailure(new Exception("No Intersection"))
                : new Point((b2 * c1 - b1 * c2) / delta, (a1 * c2 - a2 * c1) / delta).ToSuccess();
        }

        //// Find the point of intersection between
        //// the lines p1 --> p2 and p3 --> p4.
        //private void FindIntersection(
        //    PointF p1, PointF p2, PointF p3, PointF p4,
        //    out bool lines_intersect, out bool segments_intersect,
        //    out PointF intersection,
        //    out PointF close_p1, out PointF close_p2)
        //{
        //    // Get the segments' parameters.
        //    float dx12 = p2.X - p1.X;
        //    float dy12 = p2.Y - p1.Y;
        //    float dx34 = p4.X - p3.X;
        //    float dy34 = p4.Y - p3.Y;

        //    // Solve for t1 and t2
        //    float denominator = (dy12 * dx34 - dx12 * dy34);

        //    float t1 =
        //        ((p1.X - p3.X) * dy34 + (p3.Y - p1.Y) * dx34)
        //        / denominator;
        //    if (float.IsInfinity(t1))
        //    {
        //        // The lines are parallel (or close enough to it).
        //        lines_intersect = false;
        //        segments_intersect = false;
        //        intersection = new PointF(float.NaN, float.NaN);
        //        close_p1 = new PointF(float.NaN, float.NaN);
        //        close_p2 = new PointF(float.NaN, float.NaN);
        //        return;
        //    }
        //    lines_intersect = true;

        //    float t2 =
        //        ((p3.X - p1.X) * dy12 + (p1.Y - p3.Y) * dx12)
        //        / -denominator;

        //    // Find the point of intersection.
        //    intersection = new PointF(p1.X + dx12 * t1, p1.Y + dy12 * t1);

        //    // The segments intersect if t1 and t2 are between 0 and 1.
        //    segments_intersect =
        //        ((t1 >= 0) && (t1 <= 1) &&
        //         (t2 >= 0) && (t2 <= 1));

        //    // Find the closest points on the segments.
        //    if (t1 < 0)
        //    {
        //        t1 = 0;
        //    }
        //    else if (t1 > 1)
        //    {
        //        t1 = 1;
        //    }

        //    if (t2 < 0)
        //    {
        //        t2 = 0;
        //    }
        //    else if (t2 > 1)
        //    {
        //        t2 = 1;
        //    }

        //    close_p1 = new PointF(p1.X + dx12 * t1, p1.Y + dy12 * t1);
        //    close_p2 = new PointF(p3.X + dx34 * t2, p3.Y + dy34 * t2);
        //}

    }
}
