namespace Carvers.Infra.Math.Geometry
{
    public class Point<TX, TY>
    {
        public Point(TX x, TY y)
        {
            X = x;
            Y = y;
        }

        public TX X { get; }
        public TY Y { get; }
    }
}
