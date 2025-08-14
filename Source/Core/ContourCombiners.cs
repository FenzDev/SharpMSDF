using SharpMSDF.Utilities;
using System.Numerics;

namespace SharpMSDF.Core
{
    internal static class DistanceUtils
    {
        public static void InitDistance<T>(ref T d)
        {
            if (d is float dDouble)
                InitDistance(ref dDouble);
            else if (d is MultiDistance dMulti)
                InitDistance(ref dMulti);
            else if (d is MultiAndTrueDistance dMultiTrue)
                InitDistance(ref dMultiTrue);
            else
                throw new InvalidCastException("Unexpected size for DistanceUtils.ResolveDistance");
        }

        public static void InitDistance(ref float d) => d = -float.MaxValue;

        public static void InitDistance(ref MultiDistance d)
        {
            d.R = -float.MaxValue;
            d.G = -float.MaxValue;
            d.B = -float.MaxValue;
        }

        public static void InitDistance(ref MultiAndTrueDistance d)
        {
            d.R = -float.MaxValue;
            d.G = -float.MaxValue;
            d.B = -float.MaxValue;
            d.A = -float.MaxValue;
        }

        public static float ResolveDistance<T>(T d)
        {
            if (d is float dDouble)
                return ResolveDistance(dDouble);
            else if (d is MultiDistance dMulti)
                return ResolveDistance(dMulti);
            else if (d is MultiAndTrueDistance dMultiTrue)
                return ResolveDistance(dMultiTrue);
            else
                throw new InvalidCastException("Unexpected size for DistanceUtils.ResolveDistance");
        }

        public static float ResolveDistance(float d) => d;

        public static float ResolveDistance(MultiDistance d) =>
            Arithmetic.Median(d.R, d.G, d.B); // Implement median as needed

        public static float ResolveDistance(MultiAndTrueDistance d) =>
            Arithmetic.Median(d.R, d.G, d.B); // or include A as needed
    }

    public unsafe interface IContourCombiner<TDistanceSelector, TDistance>
    {
        public void NonCtorInit(Shape shape, int* windings, TDistanceSelector* selector);
        public void Reset(Vector2 origin);
        public TDistanceSelector GetEdgeSelector(int contourIndex);
        public void SetEdgeSelector(int contourIndex, TDistanceSelector selector);
        public TDistance Distance();
    }


    /// <summary>
    /// Simply selects the nearest contour.
    /// </summary>
    public unsafe struct SimpleContourCombiner<TDistanceSelector, TDistance> : IContourCombiner<TDistanceSelector, TDistance>
    where TDistanceSelector : IDistanceSelector<TDistanceSelector, TDistance>, new()
    {
        private TDistanceSelector shapeEdgeSelector = new();

        public SimpleContourCombiner() { }

        public void NonCtorInit(Shape shape, int* windings, TDistanceSelector* selector) { }

        public void Reset(Vector2 p)
        {
            shapeEdgeSelector.Reset(p);
        }

        public readonly TDistanceSelector GetEdgeSelector(int i) => shapeEdgeSelector;
        public void SetEdgeSelector(int i, TDistanceSelector selector) { shapeEdgeSelector = selector; }

        public TDistance Distance() => shapeEdgeSelector.Distance();

    }

    /// <summary>
    /// Selects the nearest contour that actually forms a border between filled and unfilled area.
    /// </summary>
    public unsafe struct OverlappingContourCombiner<TDistanceSelector, TDistance> : IContourCombiner<TDistanceSelector, TDistance>
    where TDistanceSelector : IDistanceSelector<TDistanceSelector, TDistance>, new()
    {
        private Vector2 p;
        private int* _Windings;
        private TDistanceSelector* _EdgeSelectors;
        private int _ContoursCount;

        public OverlappingContourCombiner() {}


        public void NonCtorInit(Shape shape, int* windings, TDistanceSelector* selector)
        {
            _ContoursCount = shape.Contours.Count;
            _Windings = windings;
            _EdgeSelectors = selector;

            for (int c = 0; c < _ContoursCount; c++)
            {
                windings[c] = shape.Contours[c].Winding();
                _EdgeSelectors[c] = new TDistanceSelector();
            }
        }

        public void Reset(Vector2 p)
        {
            this.p = p;
            for (int s = 0; s < _ContoursCount; s++)
                _EdgeSelectors[s].Reset(p);
        }

        public readonly TDistanceSelector GetEdgeSelector(int i) => _EdgeSelectors[i];
        public void SetEdgeSelector(int i, TDistanceSelector selector) { _EdgeSelectors[i] = selector; }

        public TDistance Distance()
        {
            int contourCount = _ContoursCount;

            var shapeEdgeSelector = new TDistanceSelector();
            var innerEdgeSelector = new TDistanceSelector();
            var outerEdgeSelector = new TDistanceSelector();

            shapeEdgeSelector.Reset(p);
            innerEdgeSelector.Reset(p);
            outerEdgeSelector.Reset(p);

            for (int i = 0; i < contourCount; ++i)
            {
                TDistance edgeDistance = _EdgeSelectors[i].Distance();
                shapeEdgeSelector.Merge(_EdgeSelectors[i]);

                float dist = DistanceUtils.ResolveDistance(edgeDistance);
                if (_Windings[i] > 0 && dist >= 0)
                    innerEdgeSelector.Merge(_EdgeSelectors[i]);
                if (_Windings[i] < 0 && dist <= 0)
                    outerEdgeSelector.Merge(_EdgeSelectors[i]);
            }

            TDistance shapeDistance = shapeEdgeSelector.Distance();
            TDistance innerDistance = innerEdgeSelector.Distance();
            TDistance outerDistance = outerEdgeSelector.Distance();

            float innerDist = DistanceUtils.ResolveDistance(innerDistance);
            float outerDist = DistanceUtils.ResolveDistance(outerDistance);

            TDistance distance = default!;
            DistanceUtils.InitDistance(ref distance);

            int winding = 0;

            if (innerDist >= 0 && Math.Abs(innerDist) <= Math.Abs(outerDist))
            {
                distance = innerDistance;
                winding = 1;
                for (int i = 0; i < contourCount; ++i)
                {
                    if (_Windings[i] > 0)
                    {
                        var contourDist = _EdgeSelectors[i].Distance();
                        float contourRes = DistanceUtils.ResolveDistance(contourDist);
                        if (Math.Abs(contourRes) < Math.Abs(outerDist) && contourRes > DistanceUtils.ResolveDistance(distance))
                            distance = contourDist;
                    }
                }
            }
            else if (outerDist <= 0 && Math.Abs(outerDist) < Math.Abs(innerDist))
            {
                distance = outerDistance;
                winding = -1;
                for (int i = 0; i < contourCount; ++i)
                {
                    if (_Windings[i] < 0)
                    {
                        var contourDist = _EdgeSelectors[i].Distance();
                        float contourRes = DistanceUtils.ResolveDistance(contourDist);
                        if (Math.Abs(contourRes) < Math.Abs(innerDist) && contourRes < DistanceUtils.ResolveDistance(distance))
                            distance = contourDist;
                    }
                }
            }
            else
            {
                return shapeDistance;
            }

            for (int i = 0; i < contourCount; ++i)
            {
                if (_Windings[i] != winding)
                {
                    var contourDist = _EdgeSelectors[i].Distance();
                    float res = DistanceUtils.ResolveDistance(contourDist);
                    float distRes = DistanceUtils.ResolveDistance(distance);
                    if (res * distRes >= 0 && Math.Abs(res) < Math.Abs(distRes))
                        distance = contourDist;
                }
            }

            if (DistanceUtils.ResolveDistance(distance) == DistanceUtils.ResolveDistance(shapeDistance))
                distance = shapeDistance;

            return distance;
        }

    }

}
