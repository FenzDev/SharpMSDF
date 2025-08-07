namespace SharpMSDF.Core
{
    internal static class DistanceUtils
    {
        public static void InitDistance<T>(ref T d)
        {
            if (d is double dDouble)
                InitDistance(ref dDouble);
            else if (d is MultiDistance dMulti)
                InitDistance(ref dMulti);
            else if (d is MultiAndTrueDistance dMultiTrue)
                InitDistance(ref dMultiTrue);
            else
                throw new InvalidCastException("Unexpected size for DistanceUtils.ResolveDistance");
        }

        public static void InitDistance(ref double d) => d = -double.MaxValue;

        public static void InitDistance(ref MultiDistance d)
        {
            d.R = -double.MaxValue;
            d.G = -double.MaxValue;
            d.B = -double.MaxValue;
        }

        public static void InitDistance(ref MultiAndTrueDistance d)
        {
            d.R = -double.MaxValue;
            d.G = -double.MaxValue;
            d.B = -double.MaxValue;
            d.A = -double.MaxValue;
        }

        public static double ResolveDistance<T>(T d)
        {
            if (d is double dDouble)
                return ResolveDistance(dDouble);
            else if (d is MultiDistance dMulti)
                return ResolveDistance(dMulti);
            else if (d is MultiAndTrueDistance dMultiTrue)
                return ResolveDistance(dMultiTrue);
            else
                throw new InvalidCastException("Unexpected size for DistanceUtils.ResolveDistance");
        }

        public static double ResolveDistance(double d) => d;

        public static double ResolveDistance(MultiDistance d) =>
            Arithmetic.Median(d.R, d.G, d.B); // Implement median as needed

        public static double ResolveDistance(MultiAndTrueDistance d) =>
            Arithmetic.Median(d.R, d.G, d.B); // or include A as needed
    }

    public interface IContourCombiner<TDistanceSelector, TDistance>
    {
        public virtual void NonCtorInit(ref Shape shape) { }
        public void Reset(Vector2 origin);
        public TDistanceSelector EdgeSelector(int contourIndex);
        public TDistance Distance();
    }


    /// <summary>
    /// Simply selects the nearest contour.
    /// </summary>
    public struct SimpleContourCombiner<TDistanceSelector, TDistance> : IContourCombiner<TDistanceSelector, TDistance>
    where TDistanceSelector : IDistanceSelector<TDistance>, new()
    {
        private readonly TDistanceSelector shapeEdgeSelector = new();

        public SimpleContourCombiner() { }
        public SimpleContourCombiner(Shape shape){ }

        public void Reset(Vector2 p)
        {
            shapeEdgeSelector.Reset(p);
        }

        public readonly TDistanceSelector EdgeSelector(int i) => shapeEdgeSelector;

        public TDistance Distance() => shapeEdgeSelector.Distance();

    }

    /// <summary>
    /// Selects the nearest contour that actually forms a border between filled and unfilled area.
    /// </summary>
    public struct OverlappingContourCombiner<TDistanceSelector, TDistance> : IContourCombiner<TDistanceSelector, TDistance>
    where TDistanceSelector : IDistanceSelector<TDistance>, new()
    {
        private Vector2 p;
        private readonly List<int> windings = new();
        private readonly List<TDistanceSelector> edgeSelectors = new();

        public OverlappingContourCombiner() {}

        public OverlappingContourCombiner(ref Shape shape) 
        {
            NonCtorInit(ref shape);
        }

        public void NonCtorInit(ref Shape shape)
        {
            for (int c = 0; c < shape.Contours.Count; c++)
            {

                windings.Add(shape.Contours[c].Winding());
                edgeSelectors.Add(new TDistanceSelector());
            }
        }

        public void Reset(Vector2 p)
        {
            this.p = p;
            foreach (var selector in edgeSelectors)
                selector.Reset(p);
        }

        public TDistanceSelector EdgeSelector(int i) => edgeSelectors[i];


        public TDistance Distance()
        {
            int contourCount = edgeSelectors.Count;

            var shapeEdgeSelector = new TDistanceSelector();
            var innerEdgeSelector = new TDistanceSelector();
            var outerEdgeSelector = new TDistanceSelector();

            shapeEdgeSelector.Reset(p);
            innerEdgeSelector.Reset(p);
            outerEdgeSelector.Reset(p);

            for (int i = 0; i < contourCount; ++i)
            {
                TDistance edgeDistance = edgeSelectors[i].Distance();
                shapeEdgeSelector.Merge(edgeSelectors[i]);

                double dist = DistanceUtils.ResolveDistance(edgeDistance);
                if (windings[i] > 0 && dist >= 0)
                    innerEdgeSelector.Merge(edgeSelectors[i]);
                if (windings[i] < 0 && dist <= 0)
                    outerEdgeSelector.Merge(edgeSelectors[i]);
            }

            TDistance shapeDistance = shapeEdgeSelector.Distance();
            TDistance innerDistance = innerEdgeSelector.Distance();
            TDistance outerDistance = outerEdgeSelector.Distance();

            double innerDist = DistanceUtils.ResolveDistance(innerDistance);
            double outerDist = DistanceUtils.ResolveDistance(outerDistance);

            TDistance distance = default!;
            DistanceUtils.InitDistance(ref distance);

            int winding = 0;

            if (innerDist >= 0 && Math.Abs(innerDist) <= Math.Abs(outerDist))
            {
                distance = innerDistance;
                winding = 1;
                for (int i = 0; i < contourCount; ++i)
                {
                    if (windings[i] > 0)
                    {
                        var contourDist = edgeSelectors[i].Distance();
                        double contourRes = DistanceUtils.ResolveDistance(contourDist);
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
                    if (windings[i] < 0)
                    {
                        var contourDist = edgeSelectors[i].Distance();
                        double contourRes = DistanceUtils.ResolveDistance(contourDist);
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
                if (windings[i] != winding)
                {
                    var contourDist = edgeSelectors[i].Distance();
                    double res = DistanceUtils.ResolveDistance(contourDist);
                    double distRes = DistanceUtils.ResolveDistance(distance);
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
