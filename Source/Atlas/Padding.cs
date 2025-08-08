using SharpMSDF.Core;
using System.Runtime.CompilerServices;

namespace SharpMSDF.Atlas
{
    public struct Padding
    {
        public float L, B, R, T;

        public Padding(float uniformPadding)
        {
            L = B = R = T = uniformPadding;
        }
        public Padding(float l, float b, float r, float t)
        {
            L = l;
            B = b;
            R = r;
            T = t;
        }

        public static Padding operator -(Padding padding)
        {
            return new Padding(-padding.L, -padding.B, -padding.R, -padding.T);
        }

        public static Padding operator +(Padding a, Padding b)
        {
            return new Padding(a.L + b.L, a.B + b.B, a.R + b.R, a.T + b.T);
        }

        public static Padding operator -(Padding a, Padding b)
        {
            return new Padding(a.L - b.L, a.B - b.B, a.R - b.R, a.T - b.T);
        }

        public static Padding operator *(float factor, Padding padding)
        {
            return new Padding(factor * padding.L, factor * padding.B, factor * padding.R, factor * padding.T);
        }

        public static Padding operator *(Padding padding, float factor)
        {
            return new Padding(padding.L * factor, padding.B * factor, padding.R * factor, padding.T * factor);
        }

        public static Padding operator /(Padding padding, float divisor)
        {
            return new Padding(padding.L / divisor, padding.B / divisor, padding.R / divisor, padding.T / divisor);
        }
    }

    public static class PaddingUtils
    {
        public static void Pad(ref Shape.Bounds bounds, in Padding padding)
        {
            bounds.l -= padding.L;
            bounds.b -= padding.B;
            bounds.r += padding.R;
            bounds.t += padding.T;
        }
    }
}
