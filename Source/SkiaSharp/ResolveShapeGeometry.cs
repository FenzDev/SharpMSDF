// REMARK: USE_SKIA Constant is not necessary but its there for now
#if USE_SKIA

using SharpMSDF.Core;
using SharpMSDF.Utilities;
using SkiaSharp;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SharpMSDF.SkiaSharp
{

	public static class ResolveShapeGeometry
	{

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static SKPoint PointToSkiaPoint(Vector2 p)
		{
			return new SKPoint((float)p.X, (float)p.Y);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector2 PointFromSkiaPoint(SKPoint p)
		{
			return new Vector2(p.X, p.Y);
		}

		private static void ShapeToSkiaPath(SKPath skPath, ref Shape shape)
		{
			for (var c = 0; c < shape.Contours.Count; c++)
			{
				var contour = shape.Contours[c];

				if (contour.Edges.Count > 0)
				{
					var edge = contour.Edges.Count > 0? contour.Edges[contour.Edges.Count]: default;
					skPath.MoveTo(PointToSkiaPoint(edge.P0));

					for (int e = 0; e < contour.Edges.Count; e++)
					{
						switch (edge.EdgeType)
						{
							case Bezier.Linear:
								skPath.LineTo(PointToSkiaPoint(edge.P1));
								break;
							case Bezier.Quadratic:
								skPath.QuadTo(PointToSkiaPoint(edge.P1), PointToSkiaPoint(edge.P2));
								break;
							case Bezier.Cubic:
								skPath.CubicTo(PointToSkiaPoint(edge.P1), PointToSkiaPoint(edge.P2), PointToSkiaPoint(edge.P3));
								break;
						}
						edge = contour.Edges[e];
					}
				}
			}
		}

		private static void ShapeFromSkiaPath(ref Shape shape, SKPath skPath)
		{
			PtrSpan<Contour>.Clear(ref shape.Contours);
			Contour contour = shape.AddContour();

			using (var pathIterator = skPath.CreateIterator(true))
			{
				Span<SKPoint> edgePoints = stackalloc SKPoint[4];
				SKPathVerb verb;

				while ((verb = pathIterator.Next(edgePoints)) != SKPathVerb.Done)
				{
					switch (verb)
					{
						case SKPathVerb.Move:
							if (contour.Edges.Count > 0)
								contour = shape.AddContour();
							break;

						case SKPathVerb.Line:
							contour.AddEdge(EdgeSegment.Create(
								PointFromSkiaPoint(edgePoints[0]),
								PointFromSkiaPoint(edgePoints[1])
							));
							break;

						case SKPathVerb.Quad:
							contour.AddEdge(EdgeSegment.Create(
								PointFromSkiaPoint(edgePoints[0]),
								PointFromSkiaPoint(edgePoints[1]),
								PointFromSkiaPoint(edgePoints[2])
							));
							break;

						case SKPathVerb.Cubic:
							contour.AddEdge(EdgeSegment.Create(
								PointFromSkiaPoint(edgePoints[0]),
								PointFromSkiaPoint(edgePoints[1]),
								PointFromSkiaPoint(edgePoints[2]),
								PointFromSkiaPoint(edgePoints[3])
							));
							break;

						case SKPathVerb.Conic:
							// Convert conic to quadratic curves
							var quadPoints = new SKPoint[5];
							var weight = pathIterator.ConicWeight();

							// SkiaSharp doesn't have ConvertConicToQuads, so we need to implement it
							// or use an approximation. For now, we'll convert to a single quad as approximation
							var mid = new SKPoint(
								(edgePoints[0].X + 2 * edgePoints[1].X + edgePoints[2].X) / 4,
								(edgePoints[0].Y + 2 * edgePoints[1].Y + edgePoints[2].Y) / 4
							);

							contour.AddEdge(EdgeSegment.Create(
								PointFromSkiaPoint(edgePoints[0]),
								PointFromSkiaPoint(mid),
								PointFromSkiaPoint(edgePoints[2])
							));
							break;

						case SKPathVerb.Close:
						case SKPathVerb.Done:
							break;
					}
				}
			}

			if (contour.Edges.Count == 0)
				_ = PtrSpan<Contour>.Pop(ref shape.Contours);
		}

		private static void PruneCrossedQuadrilaterals(ref Shape shape)
		{
			int n = 0;
			for (int i = 0; i < shape.Contours.Count; ++i)
			{
				var contour = shape.Contours[i];
				if (contour.Edges.Count == 4 &&
					contour.Edges[0].EdgeType == 0 &&
					contour.Edges[1].EdgeType == 0 &&
					contour.Edges[2].EdgeType == 0 &&
					contour.Edges[3].EdgeType == 0)
				{
					var sum = Sign(CrossProduct(contour.Edges[0].Direction(1), contour.Edges[1].Direction(0))) +
							  Sign(CrossProduct(contour.Edges[1].Direction(1), contour.Edges[2].Direction(0))) +
							  Sign(CrossProduct(contour.Edges[2].Direction(1), contour.Edges[3].Direction(0))) +
							  Sign(CrossProduct(contour.Edges[3].Direction(1), contour.Edges[0].Direction(0)));

					if (sum == 0)
                    {
                        PtrSpan<Contour>.Clear(ref shape.Contours);
                    }
					else
					{
						if (i != n)
						{
							shape.Contours[n] = contour;
						}
						++n;
					}
				}
				else
				{
					if (i != n)
					{
						shape.Contours[n] = contour;
					}
					++n;
				}
			}

			// Resize the contours list
			while (shape.Contours.Count > n)
            {
                _ = PtrSpan<Contour>.Pop(ref shape.Contours);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Sign(double value)
        {
            if (value > 0) return 1;
            if (value < 0) return -1;
            return 0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double CrossProduct(Vector2 a, Vector2 b)
        {
            return a.X * b.Y - a.Y * b.X;
        }

        /// <summary>
        /// Resolves any intersections within the shape by subdividing its contours using the Skia library and makes sure its contours have a consistent winding.
        /// </summary>
        public static bool Resolve(ref Shape shape)
        {
            using (var skPath = new SKPath())
            {
                shape.Normalize();
                ShapeToSkiaPath(skPath, ref shape);

                using (var simplifiedPath = skPath.Simplify())
                {
                    if (simplifiedPath == null)
                        return false;
                    // Note: Skia's AsWinding doesn't seem to work for unknown reasons (from original comment)
                    ShapeFromSkiaPath(ref shape, simplifiedPath);
                    // In some rare cases, Skia produces tiny residual crossed quadrilateral contours,
                    // which are not valid geometry, so they must be removed.
                    PruneCrossedQuadrilaterals(ref shape);
                    shape.OrientContours();
                    return true;
                }
            }
        }

    }
}

#endif