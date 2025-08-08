//using System;
//using System.Drawing;

//namespace SharpMSDF.Core
//{
//    public abstract class EdgeSegment_
//    {
//        public EdgeColor Color;

//        public const int MSDFGEN_CUBIC_SEARCH_STARTS = 4;
//        public const int MSDFGEN_CUBIC_SEARCH_STEPS = 4;

//        protected EdgeSegment(EdgeColor color = EdgeColor.White)
//        {
//            Color = color;
//        }

//        public static EdgeSegment Create(Vector2 p0, Vector2 p1, EdgeColor edgeColor = EdgeColor.White)
//            => new LinearSegment(p0, p1, edgeColor);

//        public static EdgeSegment Create(Vector2 p0, Vector2 p1, Vector2 p2, EdgeColor edgeColor = EdgeColor.White)
//        {
//            if (Vector2.Cross(p1 - p0, p2 - p1) == 0)
//                return new LinearSegment(p0, p2, edgeColor);
//            return new QuadraticSegment(p0, p1, p2, edgeColor);
//        }

//        public static EdgeSegment Create(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, EdgeColor edgeColor = EdgeColor.White)
//        {
//            Vector2 p12 = p2 - p1;
//            if (Vector2.Cross(p1 - p0, p12) == 0 && Vector2.Cross(p12, p3 - p2) == 0)
//                return new LinearSegment(p0, p3, edgeColor);
//            if ((p12 = 1.5 * (p1) - 0.5 * (p0)) == 1.5 * (p2) - 0.5 * (p3))
//                return new QuadraticSegment(p0, p12, p3, edgeColor);
//            return new CubicSegment(p0, p1, p2, p3, edgeColor);
//        }

//        public abstract EdgeSegment Clone();
//        public abstract int Type();
//        public abstract Vector2[] ControlPoints();
//        public abstract Vector2 Point(float param);
//        public abstract Vector2 Direction(float param);
//        public abstract Vector2 DirectionChange(float param);
//        public abstract SignedDistance SignedDistance(Vector2 origin, out float param);
//        public void DistanceToPerpendicularDistance(ref SignedDistance distance, Vector2 origin, float param)
//        {
//            if (param < 0)
//            {
//                Vector2 dir = Direction(0).Normalize();
//                Vector2 aq = origin - Point(0);
//                float ts = Vector2.Dot(aq, dir);
//                if (ts < 0)
//                {
//                    float perp = Vector2.Cross(aq, dir);
//                    if (Math.Abs(perp) <= Math.Abs(distance.Distance))
//                    {
//                        distance.Distance = perp;
//                        distance.Dot = 0;
//                    }
//                }
//            }
//            else if (param > 1)
//            {
//                Vector2 dir = Direction(1).Normalize();
//                Vector2 bq = origin - Point(1);
//                float ts = Vector2.Dot(bq, dir);
//                if (ts > 0)
//                {
//                    float perp = Vector2.Cross(bq, dir);
//                    if (Math.Abs(perp) <= Math.Abs(distance.Distance))
//                    {
//                        distance.Distance = perp;
//                        distance.Dot = 0;
//                    }
//                }
//            }
//        }
//        public abstract int ScanlineIntersections(Span<float> x, Span<int> dy, float y);
//        public abstract void Bound(ref float l, ref float b, ref float r, ref float t);
//        public abstract void Reverse();
//        public abstract void MoveStartPoint(Vector2 to);
//        public abstract void MoveEndPoint(Vector2 to);
//        public abstract void SplitInThirds(out EdgeSegment part0, out EdgeSegment part1, out EdgeSegment part2);

//        protected static void PointBounds(Vector2 p, ref float l, ref float b, ref float r, ref float t)
//        {
//            if (p.X < l) l = p.X;
//            if (p.Y < b) b = p.Y;
//            if (p.X > r) r = p.X;
//            if (p.Y > t) t = p.Y;
//        }
//    }

//    public class LinearSegment : EdgeSegment
//    {
//        public const int EDGE_TYPE = 1;
//        public readonly Vector2[] P = new Vector2[2];

//        public LinearSegment(Vector2 p0, Vector2 p1, EdgeColor c = EdgeColor.White) : base(c)
//        {
//            P[0] = p0; P[1] = p1;
//        }
//        public override EdgeSegment Clone() => new LinearSegment(P[0], P[1], Color);
//        public override int Type() => EDGE_TYPE;
//        public override Vector2[] ControlPoints() => P;
//        public override Vector2 Point(float t) => Arithmetic.Mix(P[0], P[1], t);
//        public override Vector2 Direction(float t) => P[1] - P[0];
//        public override Vector2 DirectionChange(float t) => new Vector2 { X = 0, Y = 0 };
//        public float Length() => (P[1] - P[0]).Length();

//        public override SignedDistance SignedDistance(Vector2 origin, out float param)
//        {
//            Vector2 aq = origin - P[0];
//            Vector2 ab = P[1] - P[0];
//            param = Vector2.Dot(aq, ab) / Vector2.Dot(ab, ab);
//            Vector2 eq = (param > .5) ? P[1]-origin : P[0]-origin;
//            float endpointDist = eq.Length();
//            if (param > 0 && param < 1)
//            {
//                float ortho = Vector2.Dot(ab.GetOrthonormal(false), aq);
//                if (Math.Abs(ortho) < endpointDist)
//                    return new SignedDistance(ortho, 0);
//            }
//            float sign = Arithmetic.NonZeroSign(Vector2.Cross(aq, ab));
//            return new SignedDistance(sign * endpointDist,
//                Math.Abs(Vector2.Dot(ab.Normalize(), eq.Normalize())));
//        }

//        public override int ScanlineIntersections(Span<float> x, Span<int> dy, float y)
//        {
//            if ((y >= P[0].Y && y < P[1].Y) || (y >= P[1].Y && y < P[0].Y))
//            {
//                float param = (y - P[0].Y) / (P[1].Y - P[0].Y);
//                x[0] = Arithmetic.Mix(P[0].X, P[1].X, param);
//                dy[0] = Arithmetic.Sign(P[1].Y - P[0].Y);
//                return 1;
//            }
//            return 0;
//        }

//        public override void Bound(ref float l, ref float b, ref float r, ref float t)
//        {
//            if (P[0].X < l) l = P[0].X;
//            if (P[0].Y < b) b = P[0].Y;
//            if (P[0].X > r) r = P[0].X;
//            if (P[0].Y > t) t = P[0].Y;
//            if (P[1].X < l) l = P[1].X;
//            if (P[1].Y < b) b = P[1].Y;
//            if (P[1].X > r) r = P[1].X;
//            if (P[1].Y > t) t = P[1].Y;
//        }

//        public override void Reverse()
//        {
//            var tmp = P[0];
//            P[0] = P[1];
//            P[1] = tmp;
//        }
//        public override void MoveStartPoint(Vector2 to) => P[0] = to;
//        public override void MoveEndPoint(Vector2 to) => P[1] = to;

//        public override void SplitInThirds(out EdgeSegment part0, out EdgeSegment part1, out EdgeSegment part2)
//        {
//            part0 = new LinearSegment(P[0], Point(1.0f / 3.0f), Color);
//            part1 = new LinearSegment(Point(1.0f / 3.0f), Point(2.0f / 3.0f), Color);
//            part2 = new LinearSegment(Point(2.0f / 3.0f), P[1], Color);
//        }
//    }

//    public class QuadraticSegment : EdgeSegment
//    {
//        public const int EDGE_TYPE = 2;
//        public readonly Vector2[] P = new Vector2[3];

//        public QuadraticSegment(Vector2 p0, Vector2 p1, Vector2 p2, EdgeColor c = EdgeColor.White) : base(c)
//        {
//            P[0] = p0; P[1] = p1; P[2] = p2;
//        }
//        public override EdgeSegment Clone() => new QuadraticSegment(P[0], P[1], P[2], Color);
//        public override int Type() => EDGE_TYPE;
//        public override Vector2[] ControlPoints() => P;
//        public override Vector2 Point(float t)
//            => Arithmetic.Mix(Arithmetic.Mix(P[0], P[1], t), Arithmetic.Mix(P[1], P[2], t), t);
//        public override Vector2 Direction(float t)
//        {
//            Vector2 tangent = Arithmetic.Mix(P[1] - P[0], P[2] - P[1], t);
//            return tangent.Length() == 0 ? P[2] - P[0] : tangent;
//        }
//        public override Vector2 DirectionChange(float t)
//            => new Vector2
//            {
//                X = (P[2] - P[1]).X - (P[1] - P[0]).X,
//                Y = (P[2] - P[1]).Y - (P[1] - P[0]).Y
//            };

//        public float Length()
//        {
//            Vector2 ab = P[1] - P[0];
//            Vector2 br = (P[2] - P[1]) - ab;
//            float abab = Vector2.Dot(ab, ab), abbr = Vector2.Dot(ab, br), brbr = Vector2.Dot(br, br);
//            float abLen = Math.Sqrt(abab), brLen = Math.Sqrt(brbr);
//            float crs = Vector2.Cross(ab, br);
//            float h = Math.Sqrt(abab + 2 * abbr + brbr);
//            return (
//                brLen * ((abbr + brbr) * h - abbr * abLen) +
//                crs * crs * Math.Log((brLen * h + abbr + brbr) / (brLen * abLen + abbr))
//            ) / (brbr * brLen);
//        }
//        public override SignedDistance SignedDistance(Vector2 origin, out float param)
//        {
//            // compute helper vectors
//            Vector2 qa = P[0] - origin;
//            Vector2 ab = P[1] - P[0];
//            Vector2 br = (P[2] - P[1]) - ab;

//            // cubic coefficients for |Q(param)|² derivative = 0
//            float a = Vector2.Dot(br, br);
//            float b = 3 * Vector2.Dot(ab, br);
//            float c = 2 * Vector2.Dot(ab, ab) + Vector2.Dot(qa, br);
//            float d = Vector2.Dot(qa, ab);

//            // solve for param in [0,1]
//            Span<float> t = stackalloc float[3];
//            int solutions = EquationSolver.SolveCubic(t, a, b, c, d);

//            // start by assuming the closest is at param=0 (Point A)
//            Vector2 epDir = Direction(0);
//            float minDistance = Arithmetic.NonZeroSign(Vector2.Cross(epDir, qa)) * qa.Length();
//            param = -Vector2.Dot(qa, epDir) / Vector2.Dot(epDir, epDir);

//            // check endpoint B (param=1)
//            epDir = Direction(1);
//            float distB = (new Vector2 (P[2].X - origin.X, P[2].Y - origin.Y)).Length();
//            if (distB < Math.Abs(minDistance))
//            {
//                minDistance = Arithmetic.NonZeroSign(Vector2.Cross(epDir, new Vector2 (P[2].X - origin.X, P[2].Y - origin.Y))) * distB;
//                param = Vector2.Dot(new Vector2 (origin.X - P[1].X, origin.Y - P[1].Y), epDir)
//                        / Vector2.Dot(epDir, epDir);
//            }

//            // check interior critical points
//            for (int i = 0; i < solutions; ++i)
//            {
//                if (t[i] > 0 && t[i] < 1)
//                {
//                    // Q(param) = qa + 2t·ab + param²·br
//                    Vector2 qe = new Vector2
//                    (
//                        qa.X + 2 * t[i] * ab.X + t[i] * t[i] * br.X,
//                        qa.Y + 2 * t[i] * ab.Y + t[i] * t[i] * br.Y
//                    );
//                    float dist = qe.Length();
//                    if (dist <= Math.Abs(minDistance))
//                    {
//                        minDistance = Arithmetic.NonZeroSign(Vector2.Cross(ab + t[i] * br, qe)) * dist;
//                        param = t[i];
//                    }
//                }
//            }

//            // choose return form depending on where the closest param lies
//            if (param >= 0 && param <= 1)
//            {
//                return new SignedDistance(minDistance, 0);
//            }
//            else if (param < 0.5)
//            {
//                var dir0 = Direction(0).Normalize();
//                return new SignedDistance(
//                    minDistance,
//                    Math.Abs(Vector2.Dot(dir0, qa.Normalize()))
//                );
//            }
//            else
//            {
//                var dir1 = Direction(1).Normalize();
//                var bq = new Vector2 (P[2].X - origin.X, P[2].Y - origin.Y).Normalize();
//                return new SignedDistance(
//                    minDistance,
//                    Math.Abs(Vector2.Dot(dir1, bq))
//                );
//            }
//        }

//        public override int ScanlineIntersections(Span<float> x, Span<int> dy, float y)
//        {
//            int total = 0;
//            int nextDY = y > P[0].Y ? 1 : -1;
//            x[total] = P[0].X;
//            if (P[0].Y == y)
//            {
//                if (P[0].Y < P[1].Y || (P[0].Y == P[1].Y && P[0].Y < P[2].Y))
//                    dy[total++] = 1;
//                else
//                    nextDY = 1;
//            }
//            {
//                Vector2 ab = P[1] - P[0];
//                Vector2 br = P[2] - P[1] - ab;
//                Span<float> t = stackalloc float[2];
//                int solutions = EquationSolver.SolveQuadratic(t, br.Y, 2 * ab.Y, P[0].Y - y);
//                // Sort solutions
//                if (solutions >= 2 && t[0] > t[1])
//                    (t[0], t[1]) = (t[1], t[0]);
//                for (int i = 0; i < solutions && total < 2; ++i)
//                {
//                    if (t[i] >= 0 && t[i] <= 1)
//                    {
//                        x[total] = P[0].X + 2 * t[i] * ab.X + t[i] * t[i] * br.X;
//                        if (nextDY * (ab.Y + t[i] * br.Y) >= 0)
//                        {
//                            dy[total++] = nextDY;
//                            nextDY = -nextDY;
//                        }
//                    }
//                }
//            }
//            if (P[2].Y == y)
//            {
//                if (nextDY > 0 && total > 0)
//                {
//                    --total;
//                    nextDY = -1;
//                }
//                if ((P[2].Y < P[1].Y || (P[2].Y == P[1].Y && P[2].Y < P[0].Y)) && total < 2)
//                {
//                    x[total] = P[2].X;
//                    if (nextDY < 0)
//                    {
//                        dy[total++] = -1;
//                        nextDY = 1;
//                    }
//                }
//            }
//            if (nextDY != (y >= P[2].Y ? 1 : -1))
//            {
//                if (total > 0)
//                    --total;
//                else
//                {
//                    if (Math.Abs(P[2].Y - y) < Math.Abs(P[0].Y - y))
//                        x[total] = P[2].X;
//                    dy[total++] = nextDY;
//                }
//            }
//            return total;
//        }

//        public override void Bound(ref float l, ref float b, ref float r, ref float t)
//        {
//            PointBounds(P[0], ref l, ref b, ref r, ref t);
//            PointBounds(P[2], ref l, ref b, ref r, ref t);
//            Vector2 bot = (P[1] - P[0]) - (P[2] - P[1]);
//            if (bot.X != 0)
//            {
//                float param = (P[1].X - P[0].X) / bot.X;
//                if (param > 0 && param < 1)
//                    PointBounds(Point(param), ref l, ref b, ref r, ref t);
//            }
//            if (bot.Y != 0)
//            {
//                float param = (P[1].Y - P[0].Y) / bot.Y;
//                if (param > 0 && param < 1)
//                    PointBounds(Point(param), ref l, ref b, ref r, ref t);
//            }
//        }

//        public override void Reverse()
//        {
//            (P[0], P[2]) = (P[2], P[0]);
//        }

//        public override void MoveStartPoint(Vector2 to)
//        {
//            Vector2 origSDir = P[0] - P[1];
//            Vector2 origP1 = P[1];
//            P[1] += Vector2.Cross(P[0] - P[1], to - P[0]) / Vector2.Cross(P[0] - P[1], P[2] - P[1]) * (P[2] - P[1]);
//            P[0] = to;
//            if (Vector2.Dot(origSDir, P[0] - P[1]) < 0)
//                P[1] = origP1;
//        }
//        public override void MoveEndPoint(Vector2 to)
//        {
//            Vector2 origEDir = P[2] - P[1];
//            Vector2 origP1 = P[1];
//            P[1] += Vector2.Cross(P[2] - P[1], to - P[2]) / Vector2.Cross(P[2] - P[1], P[0] - P[1]) * (P[0] - P[1]);
//            P[2] = to;
//            if (Vector2.Dot(origEDir, P[2] - P[1]) < 0)
//                P[1] = origP1;
//        }

//        public override void SplitInThirds(out EdgeSegment part0, out EdgeSegment part1, out EdgeSegment part2)
//        {
//            part0 = new QuadraticSegment(P[0], Arithmetic.Mix(P[0], P[1], 1 / 3.0f), Point(1 / 3.0f), Color);
//            part1 = new QuadraticSegment(Point(1 / 3.0f), Arithmetic.Mix(Arithmetic.Mix(P[0], P[1], 5 / 9.0f), Arithmetic.Mix(P[1], P[2], 4 / 9.0f), .5), Point(2 / 3.0f), Color);
//            part2 = new QuadraticSegment(Point(2 / 3.0f), Arithmetic.Mix(P[1], P[2], 2 / 3.0f), P[2], Color);
//        }

//        public EdgeSegment ConvertToCubic() =>
//            new CubicSegment(P[0],
//                             Arithmetic.Mix(P[0], P[1], 2.0f / 3.0f),
//                             Arithmetic.Mix(P[1], P[2], 1.0f / 3.0f),
//                             P[2],
//                             Color);
//    }

//    public class CubicSegment : EdgeSegment
//    {
//        public const int EDGE_TYPE = 3;
//        public readonly Vector2[] P = new Vector2[4];

//        public CubicSegment(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, EdgeColor c = EdgeColor.White) : base(c)
//        {
//            P[0] = p0; P[1] = p1; P[2] = p2; P[3] = p3;
//        }
//        public override EdgeSegment Clone() => new CubicSegment(P[0], P[1], P[2], P[3], Color);
//        public override int Type() => EDGE_TYPE;
//        public override Vector2[] ControlPoints() => P;
//        public override Vector2 Point(float t)
//        {
//            Vector2 p12 = Arithmetic.Mix(P[1], P[2], t);
//            return (Vector2)Arithmetic.Mix(Arithmetic.Mix(Arithmetic.Mix(P[0], P[1], t), p12, t),
//                               Arithmetic.Mix(p12, Arithmetic.Mix(P[2], P[3], t), t), t);
//        }
//        public override Vector2 Direction(float t)
//        {
//            Vector2 tangent = Arithmetic.Mix(Arithmetic.Mix(P[1] - P[0], P[2] - P[1], t),
//                                   Arithmetic.Mix(P[2] - P[1], P[3] - P[2], t), t);
//            if (tangent.Length() == 0)
//            {
//                if (t == 0) return P[2] - P[0];
//                if (t == 1) return P[3] - P[1];
//            }
//            return tangent;
//        }
//        public override Vector2 DirectionChange(float t) =>
//            Arithmetic.Mix((P[2] - P[1]) - (P[1] - P[0]),
//                (P[3] - P[2]) - (P[2] - P[1]), t);

//        public override SignedDistance SignedDistance(Vector2 origin, out float param)
//        {
//            Vector2 qa = P[0] - origin;
//            Vector2 ab = P[1] - P[0];
//            Vector2 br = P[2] - P[1] - ab;
//            Vector2 as_ = (P[3] - P[2]) - (P[2] - P[1]) - br;

//            Vector2 epDir = Direction(0);
//            float minDistance = Arithmetic.NonZeroSign(Vector2.Cross(epDir, qa)) * qa.Length(); // distance from A
//            param = -Vector2.Dot(qa, epDir) / Vector2.Dot(epDir, epDir);
//            {
//                epDir = Direction(1);
//                float distance = (P[3] - origin).Length(); // distance from B
//                if (distance < Math.Abs(minDistance))
//                {
//                    minDistance = Arithmetic.NonZeroSign(Vector2.Cross(epDir, P[3] - origin)) * distance;
//                    param = Vector2.Dot(epDir - (P[3] - origin), epDir) / Vector2.Dot(epDir, epDir);
//                }
//            }
//            // Iterative minimum distance search
//            for (int i = 0; i <= MSDFGEN_CUBIC_SEARCH_STARTS; ++i)
//            {
//                float t = (double)i / MSDFGEN_CUBIC_SEARCH_STARTS;
//                Vector2 qe = qa + 3 * t * ab + 3 * t * t * br + t * t * t *as_;
//                for (int step = 0; step < MSDFGEN_CUBIC_SEARCH_STEPS; ++step)
//                {
//                    // Improve t
//                    Vector2 d1 = 3 * ab + 6 * t * br + 3 * t * t *as_;
//                    Vector2 d2 = 6 * br + 6 * t *as_;
//                    t -= Vector2.Dot(qe, d1) / (Vector2.Dot(d1, d1) + Vector2.Dot(qe, d2));
//                    if (t <= 0 || t >= 1)
//                        break;
//                    qe = qa + 3 * t * ab + 3 * t * t * br + t * t * t *as_;
//                    float distance = qe.Length();
//                    if (distance < Math.Abs(minDistance))
//                    {
//                        minDistance = Arithmetic.NonZeroSign(Vector2.Cross(d1, qe)) * distance;
//                        param = t;
//                    }
//                }
//            }

//            if (param >= 0 && param <= 1)
//                return new SignedDistance(minDistance, 0);
//            if (param < .5)
//                return new SignedDistance(minDistance, Math.Abs(Vector2.Dot(Direction(0).Normalize(), qa.Normalize())));
//            else
//                return new SignedDistance(minDistance, Math.Abs(Vector2.Dot(Direction(1).Normalize(), (P[3] - origin).Normalize())));

//        }

//        public override int ScanlineIntersections(Span<float> x, Span<int> dy, float y)
//        {
//            int total = 0;
//            int nextDY = y > P[0].Y ? 1 : -1;
//            x[total] = P[0].X;
//            if (P[0].Y == y)
//            {
//                if (P[0].Y < P[1].Y || (P[0].Y == P[1].Y && (P[0].Y < P[2].Y || (P[0].Y == P[2].Y && P[0].Y < P[3].Y))))
//                    dy[total++] = 1;
//                else
//                    nextDY = 1;
//            }
//            {
//                Vector2 ab = P[1] - P[0];
//                Vector2 br = P[2] - P[1] - ab;
//                Vector2 as_ = (P[3] - P[2]) - (P[2] - P[1]) - br;
//                Span<float> t = stackalloc float[3];
//                int solutions = EquationSolver.SolveCubic(t, as_.Y, 3 * br.Y, 3 * ab.Y, P[0].Y - y);
//                // Sort solutions
//                if (solutions >= 2)
//                {
//                    if (t[0] > t[1])
//                        (t[0], t[1]) = (t[1], t[0]);
//                    if (solutions >= 3 && t[1] > t[2])
//                    {
//                        (t[2], t[1]) = (t[1], t[2]);
//                        if (t[0] > t[1])
//                            (t[0], t[1]) = (t[1], t[0]);
//                    }
//                }
//                for (int i = 0; i < solutions && total < 3; ++i)
//                {
//                    if (t[i] >= 0 && t[i] <= 1)
//                    {
//                        x[total] = P[0].X + 3 * t[i] * ab.X + 3 * t[i] * t[i] * br.X + t[i] * t[i] * t[i] * as_.X;
//                        if (nextDY * (ab.Y + 2 * t[i] * br.Y + t[i] * t[i] * as_.Y) >= 0)
//                        {
//                            dy[total++] = nextDY;
//                            nextDY = -nextDY;
//                        }
//                    }
//                }
//            }
//            if (P[3].Y == y)
//            {
//                if (nextDY > 0 && total > 0)
//                {
//                    --total;
//                    nextDY = -1;
//                }
//                if ((P[3].Y < P[2].Y || (P[3].Y == P[2].Y && (P[3].Y < P[1].Y || (P[3].Y == P[1].Y && P[3].Y < P[0].Y)))) && total < 3)
//                {
//                    x[total] = P[3].X;
//                    if (nextDY < 0)
//                    {
//                        dy[total++] = -1;
//                        nextDY = 1;
//                    }
//                }
//            }
//            if (nextDY != (y >= P[3].Y ? 1 : -1))
//            {
//                if (total > 0)
//                    --total;
//                else
//                {
//                    if (Math.Abs(P[3].Y - y) < Math.Abs(P[0].Y - y))
//                        x[total] = P[3].X;
//                    dy[total++] = nextDY;
//                }
//            }
//            return total;
//        }

//        public override void Bound(ref float l, ref float b, ref float r, ref float t)
//        {
//            PointBounds(P[0], ref l, ref b, ref r, ref t);
//            PointBounds(P[3], ref l, ref b, ref r, ref t);
//            Vector2 a0 = P[1] - P[0];
//            Vector2 a1 = 2 * (P[2] - P[1] - a0);
//            Vector2 a2 = P[3] - 3 * P[2] + 3 * P[1] - P[0];
//            Span<float> prms = stackalloc float[2];
//            int solutions;
//            solutions = EquationSolver.SolveQuadratic(prms, a2.X, a1.X, a0.X);
//            for (int i = 0; i < solutions; ++i)
//                if (prms[i] > 0 && prms[i] < 1)
//                    PointBounds(Point(prms[i]), ref l, ref b, ref r, ref t);
//            solutions = EquationSolver.SolveQuadratic(prms, a2.Y, a1.Y, a0.Y);
//            for (int i = 0; i < solutions; ++i)
//                if (prms[i] > 0 && prms[i] < 1)
//                    PointBounds(Point(prms[i]), ref l, ref b, ref r, ref t); PointBounds(P[0], ref l, ref b, ref r, ref t);
//        }

//        public override void Reverse()
//        {
//            (P[0], P[3]) = (P[3], P[0]);
//            (P[1], P[2]) = (P[2], P[1]);
//        }

//        public override void MoveStartPoint(Vector2 to)
//        {
//            P[1] += to-P[0];
//            P[0] = to;
//        }
//        public override void MoveEndPoint(Vector2 to)
//        {
//            P[2] += to - P[3];
//            P[3] = to;
//        }

//        public override void SplitInThirds(out EdgeSegment part0, out EdgeSegment part1, out EdgeSegment part2)
//        {
//            part0 = new CubicSegment(P[0], P[0] == P[1] ? P[0] : Arithmetic.Mix(P[0], P[1], 1 / 3.0f), Arithmetic.Mix(Arithmetic.Mix(P[0], P[1], 1 / 3.0f), Arithmetic.Mix(P[1], P[2], 1 / 3.0f), 1 / 3.0f), Point(1 / 3.0f), Color);
//            part1 = new CubicSegment(Point(1 / 3.0f),
//                Arithmetic.Mix(Arithmetic.Mix(Arithmetic.Mix(P[0], P[1], 1 / 3.0f), Arithmetic.Mix(P[1], P[2], 1 / 3.0f), 1 / 3.0f), Arithmetic.Mix(Arithmetic.Mix(P[1], P[2], 1 / 3.0f), Arithmetic.Mix(P[2], P[3], 1 / 3.0f), 1 / 3.0f), 2 / 3.0f),
//                Arithmetic.Mix(Arithmetic.Mix(Arithmetic.Mix(P[0], P[1], 2 / 3.0f), Arithmetic.Mix(P[1], P[2], 2 / 3.0f), 2 / 3.0f), Arithmetic.Mix(Arithmetic.Mix(P[1], P[2], 2 / 3.0f), Arithmetic.Mix(P[2], P[3], 2 / 3.0f), 2 / 3.0f), 1 / 3.0f),
//                Point(2 / 3.0f), Color);
//            part2 = new CubicSegment(Point(2 / 3.0f), Arithmetic.Mix(Arithmetic.Mix(P[1], P[2], 2 / 3.0f), Arithmetic.Mix(P[2], P[3], 2 / 3.0f), 2 / 3.0f), P[2] == P[3] ? P[3] : Arithmetic.Mix(P[2], P[3], 2 / 3.0f), P[3], Color);

//        }
//    }
//}
