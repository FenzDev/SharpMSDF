using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SharpMSDF.Core
{
    /**
     * A 2-dimensional euclidean vector with double precision.
     * Implementation based on the Vector2 template from Artery Engine.
     * @author Viktor Chlumsky
     */
    public struct Vector2 : IEquatable<Vector2>
    {
        public double X, Y;

        public static Vector2 Zero { get; } = new (0, 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2(double val)
        {
            X = val;
            Y = val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2(double x, double y)
        {
            X = x;
            Y = y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Equals(Vector2 other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y);
        }

        public readonly override bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is Vector2 other && Equals(other);
        }

        public readonly override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        /// Returns the vector's length.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly double Length()
        {
            return Math.Sqrt(X * X + Y * Y);
        }

        /// Returns the normalized vector - one that has the same direction but unit length.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Vector2 Normalize(bool allowZero = false) {
            double len = Length();
            if (len != 0)
                return new (X / len, Y / len);
            return new (0, allowZero? 0.0: 1.0);
        }

    /// Returns a vector with unit length that is orthogonal to this one
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Vector2 GetOrthonormal(bool polarity = true, bool allowZero = false) 
        {
            double len = Length();
            if (len != 0)
                return polarity? new (-Y/len, X/len) : new (Y/len, -X/len);
            return polarity? new (0, allowZero ? 0 : 1) : new (0, -(allowZero? 0: 1));
        }


        /// Returns a vector with the same length that is orthogonal to this one.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Vector2 GetOrthogonal(bool polarity = true) {
            return polarity? new (-Y, X) : new (Y, -X);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !(Vector2 lhs)
        {
            return lhs.X == 0 && lhs.Y == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Vector2 lhs, Vector2 rhs)
        {
            return lhs.X == rhs.X && lhs.Y == rhs.Y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Vector2 lhs, Vector2 rhs)
        {
            return lhs.X != rhs.X || lhs.Y != rhs.Y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator +(Vector2 lhs)
        {
            return new Vector2(lhs.X, lhs.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator -(Vector2 lhs)
        {
            return new Vector2(-lhs.X, -lhs.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator +(Vector2 lhs, Vector2 rhs)
        {
            return new Vector2(lhs.X + rhs.X, lhs.Y + rhs.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator -(Vector2 lhs, Vector2 rhs)
        {
            return new Vector2(lhs.X - rhs.X, lhs.Y - rhs.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator *(Vector2 lhs, Vector2 rhs)
        {
            return new Vector2(lhs.X * rhs.X, lhs.Y * rhs.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator /(Vector2 lhs, Vector2 rhs)
        {
            return new Vector2(lhs.X / rhs.X, lhs.Y / rhs.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator *(Vector2 lhs, double value)
        {
            return new Vector2(lhs.X * value, lhs.Y * value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator /(Vector2 lhs, double value)
        {
            return new Vector2(lhs.X / value, lhs.Y / value);
        }

        /// Dot product of two vectors.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Dot(Vector2 a, Vector2 b)
        {
            return a.X * b.X + a.Y * b.Y;
        }

        /// A special version of the cross product for 2D vectors (returns scalar value).
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Cross(Vector2 a, Vector2 b)
        {
            return a.X * b.Y - a.Y * b.X;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator *(double value, Vector2 vector)
        {
            return new Vector2(value * vector.X, value * vector.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator /(double value, Vector2 vector)
        {
            return new Vector2(value / vector.X, value / vector.Y);
        }
    }
}