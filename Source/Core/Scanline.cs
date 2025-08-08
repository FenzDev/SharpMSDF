using static SharpMSDF.Core.Scanline;

namespace SharpMSDF.Core
{
    public struct Scanline
    {
        
        ///<summary>
        /// An intersection with the scanline.
        ///</summary>
        public struct Intersection
        {
            /// X coordinate.
            public float X;
            /// Normalized Y direction of the oriented edge at the Point of intersection.
            public int Direction;
        };


        public Scanline()
        {
            _LastIndex = 0;
        }


        private static bool InterpretFillRule(int intersections, FillRule fillRule)
        {
            switch (fillRule)
            {
                case FillRule.FILL_NONZERO:
                    return intersections != 0;
                case FillRule.FILL_ODD:
                    return (intersections & 1) == 1;
                case FillRule.FILL_POSITIVE:
                    return intersections > 0;
                case FillRule.FILL_NEGATIVE:
                    return intersections < 0;
                default:
                    break;
            }
            return false;
        }

        public static float Overlap(Scanline a, Scanline b, float xFrom, float xTo, FillRule fillRule)
        {
            float total = 0;
            bool aInside = false, bInside = false;
            int ai = 0, bi = 0;
            float ax = a._Intersections.Count != 0 ? a._Intersections[ai].X : xTo;
            float bx = b._Intersections.Count != 0 ? b._Intersections[bi].X : xTo;
            while (ax < xFrom || bx < xFrom)
            {
                float xNext = Math.Min(ax, bx);
                if (ax == xNext && ai < (int)a._Intersections.Count)
                {
                    aInside = InterpretFillRule(a._Intersections[ai].Direction, fillRule);
                    ax = ++ai < (int)a._Intersections.Count ? a._Intersections[ai].X : xTo;
                }
                if (bx == xNext && bi < (int)b._Intersections.Count)
                {
                    bInside = InterpretFillRule(b._Intersections[bi].Direction, fillRule);
                    bx = ++bi < (int)b._Intersections.Count ? b._Intersections[bi].X : xTo;
                }
            }
            float x = xFrom;
            while (ax < xTo || bx < xTo)
            {
                float xNext = Math.Min(ax, bx);
                if (aInside == bInside)
                    total += xNext - x;
                if (ax == xNext && ai < (int)a._Intersections.Count)
                {
                    aInside = InterpretFillRule(a._Intersections[ai].Direction, fillRule);
                    ax = ++ai < (int)a._Intersections.Count ? a._Intersections[ai].X : xTo;
                }
                if (bx == xNext && bi < (int)b._Intersections.Count)
                {
                    bInside = InterpretFillRule(b._Intersections[bi].Direction, fillRule);
                    bx = ++bi < (int)b._Intersections.Count ? b._Intersections[bi].X : xTo;
                }
                x = xNext;
            }
            if (aInside == bInside)
                total += xTo - x;
            return total;
        }

        /// Populates the intersection list.
        public void SetIntersections(List<Intersection> intersections)
        {
            _Intersections = intersections;
            Preprocess();
        }

        /// Returns the number of _Intersections left of x.
        public int CountIntersections(float x) => MoveTo(x) + 1;
            
        /// Returns the total sign of _Intersections left of x.
        public int SumIntersections(float x)
        {
            int index = MoveTo(x);
            if (index >= 0)
                return _Intersections[index].Direction;
            return 0;
        }
            
        /// Decides whether the scanline is filled at x based on fill rule.
        public bool Filled(float x, FillRule fillRule) => InterpretFillRule(SumIntersections(x), fillRule);

        List<Intersection> _Intersections;
        int _LastIndex;

        void Preprocess()
        {
            _LastIndex = 0;
            if (_Intersections.Count != 0)
            {
                _Intersections.Sort((a, b) => Math.Sign(a.X - b.X));
                int totalDirection = 0;
                for (int i = 0; i < _Intersections.Count; i++)
                {
                    totalDirection += _Intersections[i].Direction;

                    _Intersections[i] = _Intersections[i] with { Direction = totalDirection };
                }
            }
        }
        int MoveTo(float x)
        {
            if (_Intersections.Count == 0)
                return -1;
            int index = _LastIndex;
            if (x < _Intersections[index].X)
            {
                do
                {
                    if (index == 0)
                    {
                        _LastIndex = 0;
                        return -1;
                    }
                    --index;
                } while (x < _Intersections[index].X);
            }
            else
            {
                while (index < _Intersections.Count - 1 && x >= _Intersections[index + 1].X)
                    ++index;
            }
            _LastIndex = index;
            return index;
        }
    }

    /// <summary>
    /// Fill rule dictates how intersection total is interpreted during rasterization.
    /// </summary>
    public enum FillRule
    {
        FILL_NONZERO,
        FILL_ODD, // "even-odd"
        FILL_POSITIVE,
        FILL_NEGATIVE
    }
}
