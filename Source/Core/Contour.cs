using SharpMSDF.Core;
using System;
using System.Collections.Generic;

namespace SharpMSDF.Core
{

    public class Contour
    {

        public List<EdgeSegment> Edges = new List<EdgeSegment>();

        private static double Shoelace(Vector2 a, Vector2 b)
        {
            return (b.X - a.X) * (a.Y + b.Y);
        }

        public void AddEdge(EdgeSegment edge)
        {
            Edges.Add(edge);
        }

        private static void BoundPoint(ref double l, ref double b, ref double r, ref double t, Vector2 p)
        {
            if (p.X < l) l = p.X;
            if (p.Y < b) b = p.Y;
            if (p.X > r) r = p.X;
            if (p.Y > t) t = p.Y;
        }

        public void Bound(ref double l, ref double b, ref double r, ref double t)
        {
            for (int e  = 0; e < Edges.Count; e++)
                Edges[e].Bound(ref l, ref b, ref r, ref t);
        }

        public void BoundMiters(ref double l, ref double b, ref double r, ref double t, double border, double miterLimit, int polarity)
        {
            if (Edges.Count == 0)
                return;

            Vector2 prevDir = Edges[^1].Direction(1).Normalize(true);

            for (int e = 0; e < Edges.Count; e++)
            {
                Vector2 dir = -Edges[e].Direction(0).Normalize(true);
                if (polarity * Vector2.Cross(prevDir, dir) >= 0)
                {
                    double miterLength = miterLimit;
                    double q = 0.5 * (1 - Vector2.Dot(prevDir, dir));
                    if (q > 0)
                        miterLength = Math.Min(1 / Math.Sqrt(q), miterLimit);
                    Vector2 miter = Edges[e].Point(0) + border * miterLength * (prevDir + dir).Normalize(true);
                    BoundPoint(ref l, ref b, ref r, ref t, miter);
                }
                prevDir = Edges[e].Direction(1).Normalize(true);
            }
        }

        public int Winding()
        {
            if (Edges.Count == 0)
                return 0;

            double total = 0;

            if (Edges.Count == 1)
            {
                Vector2 a = Edges[0].Point(0);
                Vector2 b = Edges[0].Point(1.0 / 3.0);
                Vector2 c = Edges[0].Point(2.0 / 3.0);
                total += Shoelace(a, b);
                total += Shoelace(b, c);
                total += Shoelace(c, a);
            }
            else if (Edges.Count == 2)
            {
                Vector2 a = Edges[0].Point(0);
                Vector2 b = Edges[0].Point(0.5);
                Vector2 c = Edges[1].Point(0);
                Vector2 d = Edges[1].Point(0.5);
                total += Shoelace(a, b);
                total += Shoelace(b, c);
                total += Shoelace(c, d);
                total += Shoelace(d, a);
            }
            else
            {
                Vector2 prev = Edges[^1].Point(0);
                for (int e = 0; e < Edges.Count; e++)
                {
                    Vector2 cur = Edges[e].Point(0);
                    total += Shoelace(prev, cur);
                    prev = cur;
                }
            }

            return Arithmetic.Sign(total);
        }

        public void Reverse()
        {
            int count = Edges.Count;
            for (int i = 0; i < count / 2; ++i)
            {
                (Edges[i], Edges[count - i - 1]) = (Edges[count - i - 1], Edges[i]);
            }
            for (int e=0; e < Edges.Count; e++)
                Edges[e].Reverse();
        }
    }
}
