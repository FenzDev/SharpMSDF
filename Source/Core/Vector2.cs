using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SharpMSDF.Core
{
    public static class Vector2Extensions
    {
        /// Returns the normalized vector - one that has the same direction but unit length.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 Normalize(this Vector2 vector, bool allowZero = false) {
            float len = vector.Length();
            if (len != 0)
                return new (vector.X / len, vector.Y / len);
            return new (0, allowZero? 0.0f: 1.0f);
        }

    /// Returns a vector with unit length that is orthogonal to this one
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 GetOrthonormal(this Vector2 vector, bool polarity = true, bool allowZero = false) 
        {
            float len = vector.Length();
            if (len != 0)
                return polarity? new (-vector.Y / len, vector.X / len) : new (vector.Y / len, -vector.X / len);
            return polarity? new (0, allowZero ? 0 : 1) : new (0, -(allowZero? 0: 1));
        }


        /// Returns a vector with the same length that is orthogonal to this one.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 GetOrthogonal(this Vector2 vector, bool polarity = true) {
            return polarity? new (-vector.Y, vector.X) : new (vector.Y, -vector.X);
        }

        /// Dot product of two vectors.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Dot(this Vector2 a, Vector2 b)
        {
            return a.X * b.X + a.Y * b.Y;
        }

        /// A special version of the cross product for 2D vectors (returns scalar value).
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Cross(this Vector2 a, Vector2 b)
        {
            return a.X * b.Y - a.Y * b.X;
        }

    }
}