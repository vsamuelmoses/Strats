using System;

namespace Carvers.Infra.Math.Geometry
{
    public struct Point<TX, TY>
    {
        public Point(TX x, TY y)
        {
            X = x;
            Y = y;
        }

        public TX X { get; }
        public TY Y { get; }

        public override bool Equals(Object obj)
        {
            return obj is Point<TX, TY> && this == (Point<TX, TY>)obj;
        }
        public override int GetHashCode()
        {
            return X.GetHashCode() ^ Y.GetHashCode();
        }
        public static bool operator ==(Point<TX,TY> x, Point<TX,TY> y)
        {
            return x.X.Equals(y.X) && x.Y.Equals(y.Y);
        }
        public static bool operator !=(Point<TX, TY> x, Point<TX, TY> y)
        {
            return !(x == y);
        }
    }
}
