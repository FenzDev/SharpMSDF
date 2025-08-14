using SharpMSDF.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SharpMSDF.Core
{
    public ref struct ShapeDistanceFinder<TCombiner, TDistanceSelector, TDistance>
        where  TDistanceSelector : IDistanceSelector<TDistanceSelector, TDistance>, new()
        where TCombiner : IContourCombiner<TDistanceSelector,TDistance>, new()
    {
        public delegate float DistanceType(); // Will be overridden by TContourCombiner.DistanceType

        private Shape Shape;
        private TCombiner ContourCombiner;
        private readonly Span<EdgeCache> ShapeEdgeCache; // real type: TContourCombiner.EdgeSelectorType.EdgeCache

        public unsafe ShapeDistanceFinder(Shape shape, Span<EdgeCache> edgeCache, int* windings, TDistanceSelector* selector)
        {
            Shape = shape;
            ContourCombiner = new TCombiner();
            ContourCombiner.NonCtorInit(shape, windings, selector);
            ShapeEdgeCache = edgeCache;
        }

        public TDistance Distance(Vector2 origin)
        {
            ContourCombiner.Reset(origin);

            int e = 0;
            for (int c = 0; c < Shape.Contours.Count; c++)
            {
                var contour = Shape.Contours[c];
                if (contour.Edges.Count > 0)
                {
                    var edgeSelector = ContourCombiner.GetEdgeSelector(c);

                    EdgeSegment prevEdge = contour.Edges.Count >= 2
                        ? contour.Edges[contour.Edges.Count - 2]
                        : contour.Edges[0];

                    EdgeSegment curEdge = contour.Edges[contour.Edges.Count - 1];

                    for (int i = 0; i < contour.Edges.Count; i++)
                    {
                        EdgeSegment nextEdge = contour.Edges[i];
                        edgeSelector.AddEdge(ShapeEdgeCache, e++, prevEdge, curEdge, nextEdge);
                        //ShapeEdgeCache[edgeCacheIndex++] = temp;
                        prevEdge = curEdge;
                        curEdge = nextEdge;
                    }

                    ContourCombiner.SetEdgeSelector(c, edgeSelector);
                }
            }

            return ContourCombiner.Distance();
        }

        public unsafe static TDistance OneShotDistance(Shape shape, int* windings, TDistanceSelector* selector, Vector2 origin)
        {
            Span<EdgeCache> cache = stackalloc EdgeCache[shape.GetEdgesCount()];
            var combiner = new TCombiner();
            combiner.NonCtorInit(shape, windings, selector);
            combiner.Reset(origin);

            int iCache = 0;

            for (int i = 0; i < shape.Contours.Count; ++i)
            {
                var contour = shape.Contours[i];
                if (contour.Edges.Count == 0)
                    continue;

                var edgeSelector = combiner.GetEdgeSelector(i);

                EdgeSegment prevEdge = contour.Edges.Count >= 2
                    ? contour.Edges[contour.Edges.Count - 2]
                    : contour.Edges[0];

                EdgeSegment curEdge = contour.Edges[contour.Edges.Count - 1];

                for (int e = 0; e < contour.Edges.Count; e++)
                {
                    EdgeSegment nextEdge = contour.Edges[e];
                    edgeSelector.AddEdge(cache, iCache++, prevEdge, curEdge, nextEdge);

                    prevEdge = curEdge;
                    curEdge = nextEdge;
                }

                combiner.SetEdgeSelector(i, edgeSelector);
            }

            return combiner.Distance();
        }
    }
}
