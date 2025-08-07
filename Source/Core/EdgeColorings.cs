
using SharpMSDF.Utilities;
using System.ComponentModel;

namespace SharpMSDF.Core
{
    public static class EdgeColorings
    {
        private const int MSDFGEN_EDGE_LENGTH_PRECISION = 4;
        private const int MAX_RECOLOR_STEPS = 16;
        private const int EDGE_DISTANCE_PRECISION = 16;

        private static int SymmetricalTrichotomy(int position, int n)
        {
            return (int)(3 + 2.875 * position / (n - 1) - 1.4375 + 0.5) - 3;
        }

        private static bool IsCorner(Vector2 aDir, Vector2 bDir, double crossThreshold)
        {
            return Vector2.Dot(aDir, bDir) <= 0 || Math.Abs(Vector2.Cross(aDir, bDir)) > crossThreshold;
        }

        private static double EstimateEdgeLength(EdgeSegment edge)
        {
            double len = 0;
            Vector2 prev = edge.Point(0);
            for (int i = 1; i <= MSDFGEN_EDGE_LENGTH_PRECISION; ++i)
            {
                Vector2 cur = edge.Point((1.0 / MSDFGEN_EDGE_LENGTH_PRECISION) * i);
                len += (cur - prev).Length();
                prev = cur;
            }
            return len;
        }

        private static int SeedExtract2(ref ulong seed)
        {
            int v = (int)seed & 1;
            seed >>= 1;
            return v;
        }

        private static int SeedExtract3(ref ulong seed)
        {
            int v = (int)(seed % 3);
            seed /= 3;
            return v;
        }

        private static EdgeColor InitColor(ref ulong seed)
        {
            Span<EdgeColor> colors = [ EdgeColor.Cyan, EdgeColor.Magenta, EdgeColor.Yellow ];
            return colors[SeedExtract3(ref seed)];
        }

        private static void SwitchColor(ref EdgeColor color, ref ulong seed)
        {
            int shifted = (int)color << (1 + SeedExtract2(ref seed));
            color = (EdgeColor)((shifted | (shifted >> 3)) & (int)EdgeColor.White);
        }

        private static void SwitchColor(ref EdgeColor color, ref ulong seed, EdgeColor banned)
        {
            EdgeColor combined = color & banned;
            if (combined == EdgeColor.Red || combined == EdgeColor.Green || combined == EdgeColor.Blue)
                color = (EdgeColor)((int)combined ^ (int)EdgeColor.White);
            else
                SwitchColor(ref color, ref seed);
        }

        public static void Simple(ref Shape shape, double angleThreshold, ulong seed = 0)
        {
            double crossThreshold = Math.Sin(angleThreshold);
            EdgeColor color = InitColor(ref seed);

            Span<EdgeSegment> parts = stackalloc EdgeSegment[7];

            for (int c = 0; c < shape.Contours.Count; c++)
            {
                ref Contour contour = ref shape.Contours[c];
                if (contour.Edges.Count == 0)
                    continue;

                // Identify corners
                IdentifyCorners(ref seed, crossThreshold, ref color, parts, ref contour);
            }
        }

        private static void IdentifyCorners(ref ulong seed, double crossThreshold, ref EdgeColor color, Span<EdgeSegment> parts, ref Contour contour)
        {
            Span<EdgeColor> colors = stackalloc EdgeColor[3];
            Span<int> corners = stackalloc int[contour.Edges.Count];
            int cornersCount = 0;
            Vector2 prevDirection = contour.Edges[contour.Edges.Count - 1].Direction(1);

            for (int i = 0; i < contour.Edges.Count; i++)
            {
                if (IsCorner(prevDirection.Normalize(), contour.Edges[i].Direction(0).Normalize(), crossThreshold))
                    corners[cornersCount++] = i;
                prevDirection = contour.Edges[i].Direction(1);
            }

            if (cornersCount == 0)
            {
                SwitchColor(ref color, ref seed);
                for (int i = 0; i < contour.Edges.Count; i++)
                {
                    EdgeSegment edge = contour.Edges[i];
                    edge.Color = color;
                    contour.Edges[i] = edge;
                }
            }
            else if (cornersCount == 1)
            {
                SwitchColor(ref color, ref seed);
                colors[0] = color;
                colors[1] = EdgeColor.White;
                SwitchColor(ref color, ref seed);
                colors[2] = color;
                int corner = corners[0];

                if (contour.Edges.Count >= 3)
                {
                    int m = contour.Edges.Count;
                    for (int i = 0; i < m; ++i)
                    {
                        int v = (corner + i) % m;
                        EdgeSegment edgeSegment = contour.Edges[v];
                        edgeSegment.Color = colors[1 + SymmetricalTrichotomy(i, m)];
                        contour.Edges[v] = edgeSegment;
                    }
                }
                else if (contour.Edges.Count >= 1)
                {
                    contour.Edges[0].SplitInThirds(out parts[0 + 3 * corner], out parts[1 + 3 * corner], out parts[2 + 3 * corner]);
                    if (contour.Edges.Count >= 2)
                    {
                        contour.Edges[1].SplitInThirds(out parts[3 - 3 * corner], out parts[4 - 3 * corner], out parts[5 - 3 * corner]);
                        parts[0].Color = parts[1].Color = colors[0];
                        parts[2].Color = parts[3].Color = colors[1];
                        parts[4].Color = parts[5].Color = colors[2];
                    }
                    else
                    {
                        parts[0].Color = colors[0];
                        parts[1].Color = colors[1];
                        parts[2].Color = colors[2];
                    }

                    PtrSpan<EdgeSegment>.Clear(ref contour.Edges);
                    for (int p = 0; p < parts.Length; p++)
                    {
                        //if (parts[p] != null)
                        PtrSpan<EdgeSegment>.Push(ref contour.Edges, parts[p]);
                    }
                }
            }
            else
            {
                int spline = 0;
                int start = corners[0];
                int m = contour.Edges.Count;
                SwitchColor(ref color, ref seed);
                EdgeColor initialColor = color;

                for (int i = 0; i < m; ++i)
                {
                    int index = (start + i) % m;
                    if (spline + 1 < cornersCount && corners[spline + 1] == index)
                    {
                        spline++;
                        SwitchColor(ref color, ref seed, (EdgeColor)((spline == cornersCount - 1) ? (int)initialColor : 0));
                    }
                    contour.Edges[index] = contour.Edges[index] with { Color = color };
                }
            }
        }

        private struct InkTrapCorner
        {
            public int Index;
            public double PrevEdgeLengthEstimate;
            public bool Minor;
            public EdgeColor Color;
        }

        public static void InkTrap(ref Shape shape, double angleThreshold, ulong seed = 0)
        {
            double crossThreshold = Math.Sin(angleThreshold);
            EdgeColor color = InitColor(ref seed);
            
            Span<EdgeColor> colors = stackalloc EdgeColor[3];

            for (int ctr = 0; ctr < shape.Contours.Count; ctr++)
            {
                ref Contour contour = ref shape.Contours[ctr];
                if (contour.Edges.Count == 0)
                    continue;

                double splineLength = 0;
                InkTrapIdentifyCorners(ref seed, crossThreshold, ref color, colors, ref contour, ref splineLength);
            }
        }

        private static void InkTrapIdentifyCorners(ref ulong seed, double crossThreshold, ref EdgeColor color,  Span<EdgeColor> colors, ref Contour contour, ref double splineLength)
        {
            Span<InkTrapCorner> corners = stackalloc InkTrapCorner[contour.Edges.Count];
            int cornersCount = 0;
            Vector2 prevDirection = contour.Edges[contour.Edges.Count - 1].Direction(1);
            for (int e = 0; e < contour.Edges.Count; e++)
            {
                var edge = contour.Edges[e];
                if (IsCorner(prevDirection.Normalize(), edge.Direction(0).Normalize(), crossThreshold))
                {
                    corners[cornersCount++] = new InkTrapCorner
                    {
                        Index = e,
                        PrevEdgeLengthEstimate = splineLength
                    };
                    splineLength = 0;
                }

                splineLength += EstimateEdgeLength(edge);
                prevDirection = edge.Direction(1);
            }

            if (cornersCount == 0)
            {
                SwitchColor(ref color, ref seed);
                for (int e = 0; e < contour.Edges.Count; e++)
                    contour.Edges[e] = contour.Edges[e] with { Color = color };
            }
            else if (cornersCount == 1)
            {
                SwitchColor(ref color, ref seed);
                colors[0] = color;
                colors[1] = EdgeColor.White;
                SwitchColor(ref color, ref seed);
                colors[2] = color;

                int corner = corners[0].Index;
                if (contour.Edges.Count >= 3)
                {
                    int m = contour.Edges.Count;
                    for (int i = 0; i < m; ++i)
                    {
                        int v = (corner + i) % m;
                        contour.Edges[v] = contour.Edges[v] with { Color = colors[1 + SymmetricalTrichotomy(i, m)] };
                    }
                }
                else if (contour.Edges.Count >= 1)
                {
                    EdgeSegment[] parts = new EdgeSegment[7];
                    contour.Edges[0].SplitInThirds(out parts[0 + 3 * corner], out parts[1 + 3 * corner], out parts[2 + 3 * corner]);
                    if (contour.Edges.Count >= 2)
                    {
                        contour.Edges[1].SplitInThirds(out parts[3 - 3 * corner], out parts[4 - 3 * corner], out parts[5 - 3 * corner]);
                        parts[0].Color = parts[1].Color = colors[0];
                        parts[2].Color = parts[3].Color = colors[1];
                        parts[4].Color = parts[5].Color = colors[2];
                    }
                    else
                    {
                        parts[0].Color = colors[0];
                        parts[1].Color = colors[1];
                        parts[2].Color = colors[2];
                    }

                    PtrSpan<EdgeSegment>.Clear(ref contour.Edges);
                    for (int p = 0; p < parts.Length; p++)
                    {
                        //if (parts[p] != null)
                        PtrSpan<EdgeSegment>.Push(ref contour.Edges, parts[p]);
                    }
                }
            }
            else
            {
                InkTrapCorner corner;
                int majorCornerCount = cornersCount;

                if (cornersCount > 3)
                {
                    corner = corners[0];
                    corner.PrevEdgeLengthEstimate += splineLength;
                    corners[0] = corner;
                    for (int i = 0; i < cornersCount; i++)
                    {
                        double a = corners[i].PrevEdgeLengthEstimate;
                        double b = corners[(i + 1) % cornersCount].PrevEdgeLengthEstimate;
                        double c = corners[(i + 2) % cornersCount].PrevEdgeLengthEstimate;

                        if (a > b && b < c)
                        {
                            corner = corners[i];
                            corner.Minor = true;
                            majorCornerCount--;
                            corners[i] = corner;
                        }
                    }
                }

                EdgeColor initialColor = EdgeColor.Black;
                for (int i = 0; i < cornersCount; i++)
                {
                    if (!corners[i].Minor)
                    {
                        majorCornerCount--;
                        SwitchColor(ref color, ref seed, majorCornerCount == 0 ? initialColor : 0);
                        corner = corners[i];
                        corner.Color = color;
                        corners[i] = corner;
                        if (initialColor == EdgeColor.Black)
                            initialColor = color;
                    }
                }

                for (int i = 0; i < cornersCount; i++)
                {
                    if (corners[i].Minor)
                    {
                        EdgeColor nextColor = corners[(i + 1) % cornersCount].Color;
                        corner = corners[i];
                        corner.Color = (EdgeColor)((int)(color & nextColor) ^ (int)EdgeColor.White);
                        corners[i] = corner;
                    }
                    else
                    {
                        color = corners[i].Color;
                    }
                }

                int spline = 0;
                int start = corners[0].Index;
                color = corners[0].Color;
                int m = contour.Edges.Count;

                for (int i = 0; i < m; ++i)
                {
                    int index = (start + i) % m;
                    if (spline + 1 < cornersCount && corners[spline + 1].Index == index)
                        color = corners[++spline].Color;
                    contour.Edges[index] = contour.Edges[index] with { Color = color };
                }
            }
        }
    }
}