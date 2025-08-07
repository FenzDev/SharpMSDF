using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpMSDF.Core
{
    public ref struct ShapeDistanceFinder<TCombiner, TDistanceSelector, TDistance>
        where  TDistanceSelector : IDistanceSelector<TDistance>, new()
        where TCombiner : IContourCombiner<TDistanceSelector,TDistance>, new()
    {
        public delegate double DistanceType(); // Will be overridden by TContourCombiner.DistanceType

        private readonly Shape Shape;
        private readonly IContourCombiner<TDistanceSelector, TDistance> ContourCombiner;
        private readonly EdgeCache[] ShapeEdgeCache; // real type: TContourCombiner.EdgeSelectorType.EdgeCache

        public ShapeDistanceFinder(Shape shape)
        {
            this.Shape = shape;
            ContourCombiner = new TCombiner();
            ContourCombiner.NonCtorInit(ref shape);
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
                        var edgeSelector = ContourCombiner.EdgeSelector(c);

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
                    }
                }

            }
            return ContourCombiner.Distance();
        }

        public unsafe static TDistance OneShotDistance(Shape shape, Vector2 origin)
        {
            var combiner = new TCombiner();
            combiner.NonCtorInit(ref shape);
            combiner.Reset(origin);

            for (int i = 0; i < shape.Contours.Count; ++i)
            {
                var contour = shape.Contours[i];
                if (contour.Edges.Count == 0)
                    continue;

                var edgeSelector = combiner.EdgeSelector(i);

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
            }

            return combiner.Distance();
        }
    }
}
