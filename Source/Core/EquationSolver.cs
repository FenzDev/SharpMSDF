using System;
using System.Runtime.CompilerServices;

namespace SharpMSDF.Core
{
    public static class EquationSolver
    {
        public static int SolveQuadratic(Span<float> x, float a, float b, float c)
        {
            // a == 0 -> linear equation
            if (a == 0 || Math.Abs(b) > 1e12 * Math.Abs(a))
            {
                // a == 0, b == 0 -> no solution
                if (b == 0)
                {
                    if (c == 0)
                        return -1; // 0 == 0 (infinite solutions)
                    return 0;
                }
                x[0] = -c / b;
                return 1;
            }

            float dscr = b * b - 4 * a * c;
            if (dscr > 0)
            {
                dscr = MathF.Sqrt(dscr);
                x[0] = (-b + dscr) / (2 * a);
                x[1] = (-b - dscr) / (2 * a);
                return 2;
            }
            else if (dscr == 0)
            {
                x[0] = -b / (2 * a);
                return 1;
            }
            else
            {
                return 0;
            }
        }

        private static int SolveCubicNormed(Span<float> x, float a, float b, float c)
        {
            float a2 = a * a;
            float q = (1.0f / 3.0f) * (a2 - 3 * b);
            float r = (1.0f / 54.0f) * (a * (2 * a2 - 9 * b) + 27 * c);
            float r2 = r * r;
            float q3 = q * q * q;
            a /= 3.0f;

            if (r2 < q3)
            {
                float t = r / MathF.Sqrt(q3);
                if (t < -1) t = -1;
                if (t > 1) t = 1;
                t = MathF.Acos(t);
                q = -2 * MathF.Sqrt(q);
                x[0] = q * MathF.Cos(t / 3.0f) - a;
                x[1] = q * MathF.Cos((t + 2 * MathF.PI) / 3.0f) - a;
                x[2] = q * MathF.Cos((t - 2 * MathF.PI) / 3.0f) - a;
                return 3;
            }
            else
            {
                float u = (r < 0 ? 1 : -1) * MathF.Pow(MathF.Abs(r) + MathF.Sqrt(r2 - q3), 1.0f / 3.0f);
                float v = (u == 0) ? 0 : q / u;
                x[0] = (u + v) - a;
                if (u == v || MathF.Abs(u - v) < 1e-12 * MathF.Abs(u + v))
                {
                    x[1] = -0.5f * (u + v) - a;
                    return 2;
                }
                return 1;
            }
        }

        public static int SolveCubic(Span<float> x, float a, float b, float c, float d)
        {
            if (a != 0)
            {
                float bn = b / a;
                if (Math.Abs(bn) < 1e6)
                {
                    // Above this ratio, the numerical error gets larger than if we treated a as zero
                    return SolveCubicNormed(x, bn, c / a, d / a);
                }
            }
            return SolveQuadratic(x, b, c, d);
        }
    }
}