using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SharpMSDF.Core
{
    public ref struct ShapeDistanceFinder<TCombiner, TDistanceSelector, TDistance>
        where  TDistanceSelector : IDistanceSelector<TDistance>, new()
        where TCombiner : IContourCombiner<TDistanceSelector,TDistance>, new()
    {
        public delegate float DistanceType(); // Will be overridden by TContourCombiner.DistanceType

        private Shape Shape;
        private readonly IContourCombiner<TDistanceSelector, TDistance> ContourCombiner;
        private readonly EdgeCache[] ShapeEdgeCache; // real type: TContourCombiner.EdgeSelectorType.EdgeCache

        public ShapeDistanceFinder(Shape shape)
        {
            Shape = shape;
            ContourCombiner = new TCombiner();
            ContourCombiner.NonCtorInit(shape);
            ShapeEdgeCache = new EdgeCache[shape.EdgeCount()];
        }

        public unsafe TDistance Distance(Vector2 origin)
        {
            ContourCombiner.Reset(origin);

            fixed (EdgeCache* edgeCacheStart = ShapeEdgeCache)
            {
                EdgeCache* edgeCache = edgeCacheStart;
                //int edgeCacheIndex = 0;

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
                            edgeSelector.AddEdge(edgeCache++, prevEdge, curEdge, nextEdge);
                            //ShapeEdgeCache[edgeCacheIndex++] = temp;
                            prevEdge = curEdge;
                            curEdge = nextEdge;
                        }

                        ContourCombiner.SetEdgeSelector(c, edgeSelector);
                    }
                }

            }
            return ContourCombiner.Distance();
        }

        public unsafe static TDistance OneShotDistance(Shape shape, Vector2 origin)
        {
            var combiner = new TCombiner();
            combiner.NonCtorInit(shape);
            combiner.Reset(origin);

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
                    var dummyCache = new EdgeCache(); // or default!
                    edgeSelector.AddEdge(&dummyCache, prevEdge, curEdge, nextEdge);

                    prevEdge = curEdge;
                    curEdge = nextEdge;
                }

                combiner.SetEdgeSelector(i, edgeSelector);
            }

            return combiner.Distance();
        }
    }
}
