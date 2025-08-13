using System;
using System.Runtime.CompilerServices;

namespace SharpMSDF.Core
{
    /// Represents a signed distance and alignment, which together can be compared to uniquely determine the closest edge segment.
    public struct SignedDistance
    {
        public static readonly SignedDistance Infinite = new SignedDistance(-float.MaxValue, 1);

        public float Distance;
        public float Dot;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SignedDistance()
        {
            Distance = -float.MaxValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SignedDistance(float dist, float d)
        {
            Distance = dist;
            Dot = d;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(SignedDistance a, SignedDistance b)
        {
            return Math.Abs(a.Distance) < Math.Abs(b.Distance) ||
                   Math.Abs(a.Distance) == Math.Abs(b.Distance) && a.Dot < b.Dot;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(SignedDistance a, SignedDistance b)
        {
            return Math.Abs(a.Distance) > Math.Abs(b.Distance) ||
                   Math.Abs(a.Distance) == Math.Abs(b.Distance) && a.Dot > b.Dot;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <=(SignedDistance a, SignedDistance b)
        {
            return Math.Abs(a.Distance) < Math.Abs(b.Distance) ||
                   Math.Abs(a.Distance) == Math.Abs(b.Distance) && a.Dot <= b.Dot;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >=(SignedDistance a, SignedDistance b)
        {
            return Math.Abs(a.Distance) > Math.Abs(b.Distance) ||
                   Math.Abs(a.Distance) == Math.Abs(b.Distance) && a.Dot >= b.Dot;
        }
    }
}