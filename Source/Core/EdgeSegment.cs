using System.Numerics;
using System.Runtime.CompilerServices;

namespace SharpMSDF.Core
{
    public enum Bezier : byte
    {
        Linear,
        Quadratic,
        Cubic
    }

    public struct EdgeSegment
    {
        public const int MSDFGEN_CUBIC_SEARCH_STARTS = 4;
        public const int MSDFGEN_CUBIC_SEARCH_STEPS = 4;

        public bool IsAssigned;
        public readonly Bezier EdgeType;
        public EdgeColor Color;
        public Vector2 P0;
        public Vector2 P1;
        public Vector2 P2;
        public Vector2 P3;

        public static EdgeSegment Create(Vector2 p0, Vector2 p1, EdgeColor edgeColor = EdgeColor.White)
            => new(p0, p1, edgeColor);

        public static EdgeSegment Create(Vector2 p0, Vector2 p1, Vector2 p2, EdgeColor edgeColor = EdgeColor.White)
        {
            if ((p1 - p0).Cross(p2 - p1) == 0)
                return new(p0, p2, edgeColor);
            return new(p0, p1, p2, edgeColor);
        }

        public static EdgeSegment Create(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, EdgeColor edgeColor = EdgeColor.White)
        {
            Vector2 p12 = p2 - p1;
            if ((p1 - p0).Cross(p12) == 0 && p12.Cross(p3 - p2) == 0)
                return new(p0, p3, edgeColor);
            if ((p12 = 1.5f * (p1) - 0.5f * (p0)) == 1.5f * (p2) - 0.5f * (p3))
                return new(p0, p12, p3, edgeColor);
            return new(p0, p1, p2, p3, edgeColor);
        }

        public EdgeSegment(Vector2 p0, Vector2 p1, EdgeColor c = EdgeColor.White) : this(c)
        {
            EdgeType = Bezier.Linear;
            P0 = p0;
            P1 = p1;
        }
        public EdgeSegment(Vector2 p0, Vector2 p1, Vector2 p2, EdgeColor c = EdgeColor.White) : this(c)
        {
            EdgeType = Bezier.Quadratic;
            P0 = p0;
            P1 = p1;
            P2 = p2;
        }
        public EdgeSegment(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, EdgeColor c = EdgeColor.White) : this(c)
        {
            EdgeType = Bezier.Cubic;
            P0 = p0;
            P1 = p1;
            P2 = p2;
            P3 = p3;
        }
        private EdgeSegment(EdgeColor c)
        {
            IsAssigned = true; Color = c;
        }

        public readonly Vector2 this[int i] => i == 0 ? P0 : i == 1 ? P1 : i == 2 ? P2 : P3;

        public Vector2 Point(float t)
        {
            if (EdgeType == Bezier.Linear)
                return Arithmetic.Mix(P0, P1, t);
            else if (EdgeType == Bezier.Quadratic)
                return Arithmetic.Mix(Arithmetic.Mix(P0, P1, t), Arithmetic.Mix(P1, P2, t), t);
            else
            {
                Vector2 p12 = Arithmetic.Mix(P1, P2, t);
                return Arithmetic.Mix(Arithmetic.Mix(Arithmetic.Mix(P0, P1, t), p12, t),
                                    Arithmetic.Mix(p12, Arithmetic.Mix(P2, P3, t), t), t);
            }
        }
        public Vector2 Direction(float t)
        {
            if (EdgeType == Bezier.Linear)
                return P1 - P0;
            else if (EdgeType == Bezier.Quadratic)
            {
                Vector2 tangent = Arithmetic.Mix(P1 - P0, P2 - P1, t);
                return tangent.Length() == 0 ? P2 - P0 : tangent;
            }
            else
            {
                Vector2 tangent = Arithmetic.Mix(Arithmetic.Mix(P1 - P0, P2 - P1, t),
                                       Arithmetic.Mix(P2 - P1, P3 - P2, t), t);
                if (tangent.Length() == 0)
                {
                    if (t == 0) return P2 - P0;
                    if (t == 1) return P3 - P1;
                }
                return tangent;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 DirectionChange(float t)
        {
            return EdgeType switch
            {
                Bezier.Linear => new(0),
                Bezier.Quadratic => new Vector2
                {
                    X = (P2 - P1).X - (P1 - P0).X,
                    Y = (P2 - P1).Y - (P1 - P0).Y
                },
                _ => Arithmetic.Mix((P2 - P1) - (P1 - P0),
                                    (P3 - P2) - (P2 - P1), t)
            };
        }
        public float Length()
        {
            if (EdgeType==Bezier.Linear)
                return (P1 - P0).Length();
            else 
            {
                Vector2 ab = P1 - P0;
                Vector2 br = (P2 - P1) - ab;
                float abab = Vector2.Dot(ab, ab), abbr = Vector2.Dot(ab, br), brbr = Vector2.Dot(br, br);
                float abLen = MathF.Sqrt(abab), brLen = MathF.Sqrt(brbr);
                float crs = ab.Cross(br);
                float h = MathF.Sqrt(abab + 2 * abbr + brbr);
                return (
                    brLen * ((abbr + brbr) * h - abbr * abLen) +
                    crs * crs * MathF.Log((brLen * h + abbr + brbr) / (brLen * abLen + abbr))
                ) / (brbr * brLen);
            }
            // there is no cubic only quadratic
        }

        public SignedDistance SignedDistance(Vector2 origin, out float param)
        {
            if (EdgeType == Bezier.Linear)
            {
                Vector2 aq = origin - P0;
                Vector2 ab = P1 - P0;
                param = Vector2.Dot(aq, ab) / Vector2.Dot(ab, ab);
                Vector2 eq = (param > .5) ? P1 - origin : P0 - origin;
                float endpointDist = eq.Length();
                if (param > 0 && param < 1)
                {
                    float ortho = Vector2.Dot(ab.GetOrthonormal(false), aq);
                    if (Math.Abs(ortho) < endpointDist)
                        return new SignedDistance(ortho, 0);
                }
                float sign = Arithmetic.NonZeroSign(aq.Cross(ab));
                return new SignedDistance(sign * endpointDist,
                    Math.Abs(Vector2.Dot(ab.Normalize(), eq.Normalize())));
            }
            else if (EdgeType == Bezier.Quadratic)
            {

                // compute helper vectors
                Vector2 qa = P0 - origin;
                Vector2 ab = P1 - P0;
                Vector2 br = (P2 - P1) - ab;

                // cubic coefficients for |Q(param)|² derivative = 0
                float a = Vector2.Dot(br, br);
                float b = 3 * Vector2.Dot(ab, br);
                float c = 2 * Vector2.Dot(ab, ab) + Vector2.Dot(qa, br);
                float d = Vector2.Dot(qa, ab);

                // solve for param in [0,1]
                Span<float> t = stackalloc float[3];
                int solutions = EquationSolver.SolveCubic(t, a, b, c, d);

                // start by assuming the closest is at param=0 (Point A)
                Vector2 epDir = Direction(0);
                float minDistance = Arithmetic.NonZeroSign(epDir.Cross(qa)) * qa.Length();
                param = -Vector2.Dot(qa, epDir) / Vector2.Dot(epDir, epDir);

                // check endpoint B (param=1)
                epDir = Direction(1);
                float distB = (new Vector2(P2.X - origin.X, P2.Y - origin.Y)).Length();
                if (distB < Math.Abs(minDistance))
                {
                    minDistance = Arithmetic.NonZeroSign(epDir.Cross(new Vector2(P2.X - origin.X, P2.Y - origin.Y))) * distB;
                    param = Vector2.Dot(new Vector2(origin.X - P1.X, origin.Y - P1.Y), epDir)
                            / Vector2.Dot(epDir, epDir);
                }

                // check interior critical points
                for (int i = 0; i < solutions; ++i)
                {
                    if (t[i] > 0 && t[i] < 1)
                    {
                        // Q(param) = qa + 2t·ab + param²·br
                        Vector2 qe = new
                        (
                            qa.X + 2 * t[i] * ab.X + t[i] * t[i] * br.X,
                            qa.Y + 2 * t[i] * ab.Y + t[i] * t[i] * br.Y
                        );
                        float dist = qe.Length();
                        if (dist <= Math.Abs(minDistance))
                        {
                            minDistance = Arithmetic.NonZeroSign((ab + t[i] * br).Cross(qe)) * dist;
                            param = t[i];
                        }
                    }
                }

                // choose return form depending on where the closest param lies
                if (param >= 0 && param <= 1)
                {
                    return new SignedDistance(minDistance, 0);
                }
                else if (param < 0.5)
                {
                    var dir0 = Direction(0).Normalize();
                    return new SignedDistance(
                        minDistance,
                        Math.Abs(Vector2.Dot(dir0, qa.Normalize()))
                    );
                }
                else
                {
                    var dir1 = Direction(1).Normalize();
                    var bq = new Vector2(P2.X - origin.X, P2.Y - origin.Y).Normalize();
                    return new SignedDistance(
                        minDistance,
                        Math.Abs(Vector2.Dot(dir1, bq))
                    );
                }
            }
            else
            {

                Vector2 qa = P0 - origin;
                Vector2 ab = P1 - P0;
                Vector2 br = P2 - P1 - ab;
                Vector2 as_ = (P3 - P2) - (P2 - P1) - br;

                Vector2 epDir = Direction(0);
                float minDistance = Arithmetic.NonZeroSign(epDir.Cross(qa)) * qa.Length(); // distance from A
                param = -Vector2.Dot(qa, epDir) / Vector2.Dot(epDir, epDir);
                {
                    epDir = Direction(1);
                    float distance = (P3 - origin).Length(); // distance from B
                    if (distance < Math.Abs(minDistance))
                    {
                        minDistance = Arithmetic.NonZeroSign(epDir.Cross(P3 - origin)) * distance;
                        param = Vector2.Dot(epDir - (P3 - origin), epDir) / Vector2.Dot(epDir, epDir);
                    }
                }
                // Iterative minimum distance search
                for (int i = 0; i <= MSDFGEN_CUBIC_SEARCH_STARTS; ++i)
                {
                    float t = (float)i / MSDFGEN_CUBIC_SEARCH_STARTS;
                    Vector2 qe = qa + 3 * t * ab + 3 * t * t * br + t * t * t * as_;
                    for (int step = 0; step < MSDFGEN_CUBIC_SEARCH_STEPS; ++step)
                    {
                        // Improve t
                        Vector2 d1 = 3 * ab + 6 * t * br + 3 * t * t * as_;
                        Vector2 d2 = 6 * br + 6 * t * as_;
                        t -= Vector2.Dot(qe, d1) / (Vector2.Dot(d1, d1) + Vector2.Dot(qe, d2));
                        if (t <= 0 || t >= 1)
                            break;
                        qe = qa + 3 * t * ab + 3 * t * t * br + t * t * t * as_;
                        float distance = qe.Length();
                        if (distance < Math.Abs(minDistance))
                        {
                            minDistance = Arithmetic.NonZeroSign(d1.Cross(qe)) * distance;
                            param = t;
                        }
                    }
                }

                if (param >= 0 && param <= 1)
                    return new SignedDistance(minDistance, 0);
                if (param < .5)
                    return new SignedDistance(minDistance, Math.Abs(Vector2.Dot(Direction(0).Normalize(), qa.Normalize())));
                else
                    return new SignedDistance(minDistance, Math.Abs(Vector2.Dot(Direction(1).Normalize(), (P3 - origin).Normalize())));

            }
        }

        public int ScanlineIntersections(Span<float> x, Span<int> dy, float y)
        {
            if (EdgeType == Bezier.Linear)
            {
                if ((y >= P0.Y && y < P1.Y) || (y >= P1.Y && y < P0.Y))
                {
                    float param = (y - P0.Y) / (P1.Y - P0.Y);
                    x[0] = Arithmetic.Mix(P0.X, P1.X, param);
                    dy[0] = Arithmetic.Sign(P1.Y - P0.Y);
                    return 1;
                }
            }
            else if (EdgeType == Bezier.Quadratic)
            {

                int total = 0;
                int nextDY = y > P0.Y ? 1 : -1;
                x[total] = P0.X;
                if (P0.Y == y)
                {
                    if (P0.Y < P1.Y || (P0.Y == P1.Y && P0.Y < P2.Y))
                        dy[total++] = 1;
                    else
                        nextDY = 1;
                }
                {
                    Vector2 ab = P1 - P0;
                    Vector2 br = P2 - P1 - ab;
                    Span<float> t = stackalloc float[2];
                    int solutions = EquationSolver.SolveQuadratic(t, br.Y, 2 * ab.Y, P0.Y - y);
                    // Sort solutions
                    if (solutions >= 2 && t[0] > t[1])
                        (t[0], t[1]) = (t[1], t[0]);
                    for (int i = 0; i < solutions && total < 2; ++i)
                    {
                        if (t[i] >= 0 && t[i] <= 1)
                        {
                            x[total] = P0.X + 2 * t[i] * ab.X + t[i] * t[i] * br.X;
                            if (nextDY * (ab.Y + t[i] * br.Y) >= 0)
                            {
                                dy[total++] = nextDY;
                                nextDY = -nextDY;
                            }
                        }
                    }
                }
                if (P2.Y == y)
                {
                    if (nextDY > 0 && total > 0)
                    {
                        --total;
                        nextDY = -1;
                    }
                    if ((P2.Y < P1.Y || (P2.Y == P1.Y && P2.Y < P0.Y)) && total < 2)
                    {
                        x[total] = P2.X;
                        if (nextDY < 0)
                        {
                            dy[total++] = -1;
                            nextDY = 1;
                        }
                    }
                }
                if (nextDY != (y >= P2.Y ? 1 : -1))
                {
                    if (total > 0)
                        --total;
                    else
                    {
                        if (Math.Abs(P2.Y - y) < Math.Abs(P0.Y - y))
                            x[total] = P2.X;
                        dy[total++] = nextDY;
                    }
                }
                return total;
            }
            else
            {

                int total = 0;
                int nextDY = y > P0.Y ? 1 : -1;
                x[total] = P0.X;
                if (P0.Y == y)
                {
                    if (P0.Y < P1.Y || (P0.Y == P1.Y && (P0.Y < P2.Y || (P0.Y == P2.Y && P0.Y < P3.Y))))
                        dy[total++] = 1;
                    else
                        nextDY = 1;
                }
                {
                    Vector2 ab = P1 - P0;
                    Vector2 br = P2 - P1 - ab;
                    Vector2 as_ = (P3 - P2) - (P2 - P1) - br;
                    Span<float> t = stackalloc float[3];
                    int solutions = EquationSolver.SolveCubic(t, as_.Y, 3 * br.Y, 3 * ab.Y, P0.Y - y);
                    // Sort solutions
                    if (solutions >= 2)
                    {
                        if (t[0] > t[1])
                            (t[0], t[1]) = (t[1], t[0]);
                        if (solutions >= 3 && t[1] > t[2])
                        {
                            (t[2], t[1]) = (t[1], t[2]);
                            if (t[0] > t[1])
                                (t[0], t[1]) = (t[1], t[0]);
                        }
                    }
                    for (int i = 0; i < solutions && total < 3; ++i)
                    {
                        if (t[i] >= 0 && t[i] <= 1)
                        {
                            x[total] = P0.X + 3 * t[i] * ab.X + 3 * t[i] * t[i] * br.X + t[i] * t[i] * t[i] * as_.X;
                            if (nextDY * (ab.Y + 2 * t[i] * br.Y + t[i] * t[i] * as_.Y) >= 0)
                            {
                                dy[total++] = nextDY;
                                nextDY = -nextDY;
                            }
                        }
                    }
                }
                if (P3.Y == y)
                {
                    if (nextDY > 0 && total > 0)
                    {
                        --total;
                        nextDY = -1;
                    }
                    if ((P3.Y < P2.Y || (P3.Y == P2.Y && (P3.Y < P1.Y || (P3.Y == P1.Y && P3.Y < P0.Y)))) && total < 3)
                    {
                        x[total] = P3.X;
                        if (nextDY < 0)
                        {
                            dy[total++] = -1;
                            nextDY = 1;
                        }
                    }
                }
                if (nextDY != (y >= P3.Y ? 1 : -1))
                {
                    if (total > 0)
                        --total;
                    else
                    {
                        if (Math.Abs(P3.Y - y) < Math.Abs(P0.Y - y))
                            x[total] = P3.X;
                        dy[total++] = nextDY;
                    }
                }
                return total;
            }
            return 0;
        }
        public void Bound(ref float l, ref float b, ref float r, ref float t)
        {

            if (EdgeType == Bezier.Linear)
            {
                if (P0.X < l) l = P0.X;
                if (P0.Y < b) b = P0.Y;
                if (P0.X > r) r = P0.X;
                if (P0.Y > t) t = P0.Y;
                if (P1.X < l) l = P1.X;
                if (P1.Y < b) b = P1.Y;
                if (P1.X > r) r = P1.X;
                if (P1.Y > t) t = P1.Y;
            }
            else if (EdgeType == Bezier.Quadratic)
            {

                PointBounds(P0, ref l, ref b, ref r, ref t);
                PointBounds(P2, ref l, ref b, ref r, ref t);
                Vector2 bot = (P1 - P0) - (P2 - P1);
                if (bot.X != 0)
                {
                    float param = (P1.X - P0.X) / bot.X;
                    if (param > 0 && param < 1)
                        PointBounds(Point(param), ref l, ref b, ref r, ref t);
                }
                if (bot.Y != 0)
                {
                    float param = (P1.Y - P0.Y) / bot.Y;
                    if (param > 0 && param < 1)
                        PointBounds(Point(param), ref l, ref b, ref r, ref t);
                }
            }
            else
            {
                PointBounds(P0, ref l, ref b, ref r, ref t);
                PointBounds(P3, ref l, ref b, ref r, ref t);
                Vector2 a0 = P1 - P0;
                Vector2 a1 = 2 * (P2 - P1 - a0);
                Vector2 a2 = P3 - 3 * P2 + 3 * P1 - P0;
                Span<float> prms = stackalloc float[2];
                int solutions;
                solutions = EquationSolver.SolveQuadratic(prms, a2.X, a1.X, a0.X);
                for (int i = 0; i < solutions; ++i)
                    if (prms[i] > 0 && prms[i] < 1)
                        PointBounds(Point(prms[i]), ref l, ref b, ref r, ref t);
                solutions = EquationSolver.SolveQuadratic(prms, a2.Y, a1.Y, a0.Y);
                for (int i = 0; i < solutions; ++i)
                    if (prms[i] > 0 && prms[i] < 1)
                        PointBounds(Point(prms[i]), ref l, ref b, ref r, ref t); PointBounds(P0, ref l, ref b, ref r, ref t);

            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reverse()
        {
            if (EdgeType == Bezier.Linear)
                (P0, P1) = (P1, P0);
            else if (EdgeType == Bezier.Quadratic)
                (P0, P2) = (P2, P0);
            else
            {
                (P0, P3) = (P3, P0);
                (P1, P2) = (P2, P1);
            }
            
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MoveStartPoint(Vector2 to)
        {
            if (EdgeType == Bezier.Linear)
                P0 = to;
            else if (EdgeType == Bezier.Quadratic)
            {
                Vector2 origSDir = P0 - P1;
                Vector2 origP1 = P1;
                P1 += (P0 - P1).Cross(to - P0) / (P0 - P1).Cross(P2 - P1) * (P2 - P1);
                P0 = to;
                if (Vector2.Dot(origSDir, P0 - P1) < 0)
                    P1 = origP1;
            }
            else
            {
                P1 += to - P0;
                P0 = to;
            }

        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MoveEndPoint(Vector2 to)
        {
            if (EdgeType == Bezier.Linear)
                P1 = to;
            else if (EdgeType == Bezier.Quadratic)
            {
                Vector2 origEDir = P2 - P1;
                Vector2 origP1 = P1;
                P1 += (P2 - P1).Cross(to - P2) / (P2 - P1).Cross(P0 - P1) * (P0 - P1);
                P2 = to;
                if (Vector2.Dot(origEDir, P2 - P1) < 0)
                    P1 = origP1;
            }
            else
            {
                P2 += to - P3;
                P3 = to;
            }
        }
        public void SplitInThirds(out EdgeSegment part0, out EdgeSegment part1, out EdgeSegment part2)
        {
            if (EdgeType==Bezier.Linear)
            {       
                part0 = new (P0, Point(1.0f / 3.0f), Color);
                part1 = new (Point(1.0f / 3.0f), Point(2.0f / 3.0f), Color);
                part2 = new (Point(2.0f / 3.0f), P1, Color);
            }
            else if (EdgeType == Bezier.Quadratic)
            {
                part0 = new (P0, Arithmetic.Mix(P0, P1, 1 / 3.0f), Point(1 / 3.0f), Color);
                part1 = new (Point(1 / 3.0f), Arithmetic.Mix(Arithmetic.Mix(P0, P1, 5 / 9.0f), Arithmetic.Mix(P1, P2, 4 / 9.0f), .5f), Point(2 / 3.0f), Color);
                part2 = new (Point(2 / 3.0f), Arithmetic.Mix(P1, P2, 2 / 3.0f), P2, Color);
            }
            else
            {
                part0 = new (P0, P0 == P1 ? P0 : Arithmetic.Mix(P0, P1, 1 / 3.0f), Arithmetic.Mix(Arithmetic.Mix(P0, P1, 1 / 3.0f), Arithmetic.Mix(P1, P2, 1 / 3.0f), 1 / 3.0f), Point(1 / 3.0f), Color);
                part1 = new (Point(1 / 3.0f),
                    Arithmetic.Mix(Arithmetic.Mix(Arithmetic.Mix(P0, P1, 1 / 3.0f), Arithmetic.Mix(P1, P2, 1 / 3.0f), 1 / 3.0f), Arithmetic.Mix(Arithmetic.Mix(P1, P2, 1 / 3.0f), Arithmetic.Mix(P2, P3, 1 / 3.0f), 1 / 3.0f), 2 / 3.0f),
                    Arithmetic.Mix(Arithmetic.Mix(Arithmetic.Mix(P0, P1, 2 / 3.0f), Arithmetic.Mix(P1, P2, 2 / 3.0f), 2 / 3.0f), Arithmetic.Mix(Arithmetic.Mix(P1, P2, 2 / 3.0f), Arithmetic.Mix(P2, P3, 2 / 3.0f), 2 / 3.0f), 1 / 3.0f),
                    Point(2 / 3.0f), Color);
                part2 = new (Point(2 / 3.0f), Arithmetic.Mix(Arithmetic.Mix(P1, P2, 2 / 3.0f), Arithmetic.Mix(P2, P3, 2 / 3.0f), 2 / 3.0f), P2 == P3 ? P3 : Arithmetic.Mix(P2, P3, 2 / 3.0f), P3, Color);
            }
        }

        public EdgeSegment ConvertToCubic() =>
            new (P0,
                Arithmetic.Mix(P0, P1, 2.0f / 3.0f),
                Arithmetic.Mix(P1, P2, 1.0f / 3.0f),
                P2,
                Color);

        private static void PointBounds(Vector2 p, ref float l, ref float b, ref float r, ref float t)
        {
            if (p.X < l) l = p.X;
            if (p.Y < b) b = p.Y;
            if (p.X > r) r = p.X;
            if (p.Y > t) t = p.Y;
        }
        public void DistanceToPerpendicularDistance(ref SignedDistance distance, Vector2 origin, float param)
        {
            if (param < 0)
            {
                Vector2 dir = Direction(0).Normalize();
                Vector2 aq = origin - Point(0);
                float ts = Vector2.Dot(aq, dir);
                if (ts < 0)
                {
                    float perp = aq.Cross(dir);
                    if (Math.Abs(perp) <= Math.Abs(distance.Distance))
                    {
                        distance.Distance = perp;
                        distance.Dot = 0;
                    }
                }
            }
            else if (param > 1)
            {
                Vector2 dir = Direction(1).Normalize();
                Vector2 bq = origin - Point(1);
                float ts = Vector2.Dot(bq, dir);
                if (ts > 0)
                {
                    float perp = bq.Cross(dir);
                    if (Math.Abs(perp) <= Math.Abs(distance.Distance))
                    {
                        distance.Distance = perp;
                        distance.Dot = 0;
                    }
                }
            }
        }

    }
}
