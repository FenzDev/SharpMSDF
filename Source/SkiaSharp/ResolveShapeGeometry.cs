#if MSDFGEN_USE_SKIA

using SharpMSDF.Core;
using SkiaSharp;

namespace SharpMSDF.SkiaSharp
{

	public static class ResolveShapeGeometry
	{

		public static SKPoint PointToSkiaPoint(Vector2 p)
		{
			return new SKPoint((float)p.X, (float)p.Y);
		}

		public static Vector2 PointFromSkiaPoint(SKPoint p)
		{
			return new Vector2(p.X, p.Y);
		}

		public static void ShapeToSkiaPath(SKPath skPath, Shape shape)
		{
			foreach (var contour in shape.Contours)
			{
				if (contour.Edges.Count > 0)
				{
					var edge = contour.Edges.LastOrDefault();
					var controlPoints = edge.ControlPoints();
					skPath.MoveTo(PointToSkiaPoint(controlPoints[0]));

					foreach (var nextEdge in contour.Edges)
					{
						var p = edge.ControlPoints();
						switch (edge.Type())
						{
							case 1:
								skPath.LineTo(PointToSkiaPoint(p[1]));
								break;
							case 2:
								skPath.QuadTo(PointToSkiaPoint(p[1]), PointToSkiaPoint(p[2]));
								break;
							case 3:
								skPath.CubicTo(PointToSkiaPoint(p[1]), PointToSkiaPoint(p[2]), PointToSkiaPoint(p[3]));
								break;
						}
						edge = nextEdge;
					}
				}
			}
		}

		public static void ShapeFromSkiaPath(Shape shape, SKPath skPath)
		{
			shape.Contours.Clear();
			Contour contour = shape.AddContour();

			using (var pathIterator = skPath.CreateIterator(true))
			{
				var edgePoints = new SKPoint[4];
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
				shape.Contours.RemoveAt(shape.Contours.Count - 1);
		}

		private static void PruneCrossedQuadrilaterals(Shape shape)
		{
			int n = 0;
			for (int i = 0; i < shape.Contours.Count; ++i)
			{
				var contour = shape.Contours[i];
				if (contour.Edges.Count == 4 &&
					contour.Edges[0].Type() == 0 &&
					contour.Edges[1].Type() == 0 &&
					contour.Edges[2].Type() == 0 &&
					contour.Edges[3].Type() == 0)
				{
					var sum = Sign(CrossProduct(contour.Edges[0].Direction(1), contour.Edges[1].Direction(0))) +
							  Sign(CrossProduct(contour.Edges[1].Direction(1), contour.Edges[2].Direction(0))) +
							  Sign(CrossProduct(contour.Edges[2].Direction(1), contour.Edges[3].Direction(0))) +
							  Sign(CrossProduct(contour.Edges[3].Direction(1), contour.Edges[0].Direction(0)));

					if (sum == 0)
					{
						contour.Edges.Clear();
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
				shape.Contours.RemoveAt(shape.Contours.Count - 1);
			}
		}

		public static bool Resolve(Shape shape)
		{
			using (var skPath = new SKPath())
			{
				shape.Normalize();
				ShapeToSkiaPath(skPath, shape);

				using (var simplifiedPath = skPath.Simplify())
				{
					if (simplifiedPath == null)
						return false;
					// Note: Skia's AsWinding doesn't seem to work for unknown reasons (from original comment)
					ShapeFromSkiaPath(shape, simplifiedPath);
					// In some rare cases, Skia produces tiny residual crossed quadrilateral contours,
					// which are not valid geometry, so they must be removed.
					PruneCrossedQuadrilaterals(shape);
					shape.OrientContours();
					return true;
				}
			}
		}
		private static int Sign(double value)
		{
			if (value > 0) return 1;
			if (value < 0) return -1;
			return 0;
		}

		private static double CrossProduct(Vector2 a, Vector2 b)
		{
			return a.X * b.Y - a.Y * b.X;
		}
	}
}

#endif