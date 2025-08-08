using Typography.OpenFont;
using SharpMSDF.Core;
using static System.Formats.Asn1.AsnWriter;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using SharpMSDF.Utilities;
using System.Numerics;

namespace SharpMSDF.IO
{
    public enum FontCoordinateScaling
    {
        None,
        EmNormalized,
        LegacyNormalized
    }

    /// Global metrics of a typeface (in face units).
    public struct FontMetrics
    {
        /// The size of one EM.
        public float EmSize;
        /// The vertical position of the ascender and descender relative to the baseline.
        public float AscenderY, DescenderY;
        /// The vertical difference between consecutive baselines.
        public float LineHeight;
        /// The vertical position and thickness of the underline.
        public float UnderlineY/*, UnderlineThickness*/;
    };

    public static class FontImporter
    {

        public static Typeface LoadFont(string filename)
        {
            using FileStream file = File.OpenRead(filename);
            OpenFontReader reader = new OpenFontReader();
            return reader.Read(file);
        }
        public static float GetFontCoordinateScale(Typeface face, FontCoordinateScaling coordinateScaling) {
            switch (coordinateScaling) {
                case FontCoordinateScaling.None:
                    return 1;
                case FontCoordinateScaling.EmNormalized:
                    return 1.0f / (face.UnitsPerEm!=0? face.UnitsPerEm: 1.0f);
                case FontCoordinateScaling.LegacyNormalized:
                    return 1.0f / 64.0f;
            }
            return 1;
        }


        public static bool GetFontMetrics(out FontMetrics metrics, Typeface face, FontCoordinateScaling coordinateScaling)
        {
            float scale = GetFontCoordinateScale(face, coordinateScaling);
            metrics.EmSize = scale * face.UnitsPerEm;
            metrics.AscenderY = scale * face.Ascender;
            metrics.DescenderY = scale * face.Descender;
			metrics.LineHeight = scale * (face.Ascender - face.Descender + face.LineGap);
			metrics.UnderlineY = scale * face.UnderlinePosition;
            //metrics.UnderlineThickness = not implemented
            return true;
        }


        public static ushort PreEstimateGlyph(Typeface typeface, uint unicode, out int maxContours, out int maxSegments)
        {
            var index = typeface.GetGlyphIndex((int)unicode);
            var glyph = typeface.GetGlyph(index);

            maxContours = glyph.EndPoints.Length;
            maxSegments = 0;

            int start = 0; int count;
            for (int i = 0; i < glyph.EndPoints.Length; i++, start += count)
                maxSegments += (count = glyph.EndPoints[i] - start + 1) <= 0 ? 0: count == 1? 3: count; // see Shape.Normalize() it will be spliting segments in three if its 

            return index;
        }

        /// <summary>
        /// Loads a glyph from a Typography Typeface into a <see cref="Shape"/>,
        /// </summary>
        public static Shape LoadGlyph(
            Typeface typeface,
            ushort glyphIndex,
            FontCoordinateScaling scaling,
            ref PtrPool<Contour> contoursPool,
            ref PtrPool<EdgeSegment> segmentsPool,
            ref float advance
            )
        {
			if (glyphIndex == 0)
			{
				advance = 0;
			}
			var glyph = typeface.GetGlyph(glyphIndex);

            int advUnits = typeface.GetAdvanceWidthFromGlyphIndex(glyphIndex);

            //const int padding = 0;      // pixels

            // 1) Raw glyph bounds in face units
            var bounds = glyph.Bounds;
            //double wUnits = bounds.XMax - bounds.XMin;
            //double hUnits = bounds.YMax - bounds.YMin;

            // 2) Compute padded bitmap dimensions
            //idealWidth = (float)wUnits / 64f; // + padding * 2;
            //idealHeight = (float)hUnits / 64f; // + padding * 2;

            // 3) Compute offset so that glyph’s bottom‐left maps to (padding, padding)

            float scale = GetFontCoordinateScale(typeface, scaling);
            advance += advUnits * scale;

            //double offsetX = bounds.XMin /*/ div*/; // + padding
            //double offsetY = -bounds.YMin /*/ div*/; // + padding

            Span<GlyphPointF> pts = glyph.GlyphPoints;
            Span<ushort> ends = glyph.EndPoints;
            int start = 0;
            Shape shape = new()
            {
                Contours = contoursPool.Reserve(ends.Length)
            };

            for (int i = 0; i < ends.Length; i++)
            {
                
                if (!FillAndAddContour(scale, pts, ends, ref segmentsPool, ref shape, start, i))
                {
                    start = ends[i] + 1;
                    continue;
                }
                start = ends[i] + 1;
            }

            return shape;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector2 ToShapeSpace(float scale, GlyphPointF p)
            => new (p.X * scale, p.Y * scale);

        private static bool FillAndAddContour(float scale, Span<GlyphPointF> pts, Span<ushort> ends, ref PtrPool<EdgeSegment> segmentsPool, ref Shape shape, int start, int i)
        {
            int end = ends[i];
            int count = end - start + 1;
            if (count <= 0) 
                return false;
            ReadOnlySpan<GlyphPointF> contourPts = pts.Slice(start, count);
            //for (int e = start; e <= ends[e]; e++)
            //    contourPts[e - start] = pts[e];

            Contour contour = new()
            {
                Edges = segmentsPool.Reserve(count == 1 ? 3 : count) // See PreEstimateGlyph()
            };

            bool firstOff = !contourPts[0].onCurve;
            GlyphPointF firstPt = firstOff
                ? new GlyphPointF(
                    (contourPts[^1].X + contourPts[0].X) * 0.5f,
                    (contourPts[^1].Y + contourPts[0].Y) * 0.5f,
                    true)
                : contourPts[0];

            var currentOn = firstPt;
            GlyphPointF? pendingOff = null;
            int idx0 = firstOff ? 0 : 1;

            for (int e = idx0; e < contourPts.Length; e++)
            {
                var pt = contourPts[e];
                if (pt.onCurve)
                {
                    if (pendingOff.HasValue)
                    {
                        var c0 = ToShapeSpace(scale, currentOn);
                        var c1 = ToShapeSpace(scale, pendingOff.Value);
                        var c2 = ToShapeSpace(scale, pt);

                        PtrSpan<EdgeSegment>.Push(ref contour.Edges, new(
                            new Vector2((float)c0.X, (float)c0.Y),
                            new Vector2((float)c1.X, (float)c1.Y),
                            new Vector2((float)c2.X, (float)c2.Y),
                            EdgeColor.White
                        ));
                        pendingOff = null;
                        currentOn = pt;
                    }
                    else
                    {
                        var c0 = ToShapeSpace(scale, currentOn);
                        var c1 = ToShapeSpace(scale, pt);
                        PtrSpan<EdgeSegment>.Push(ref contour.Edges, new(
                            new Vector2((float)c0.X, (float)c0.Y),
                            new Vector2((float)c1.X, (float)c1.Y)
                        ));
                        currentOn = pt;
                    }
                }
                else
                {
                    if (!pendingOff.HasValue)
                    {
                        pendingOff = pt;
                    }
                    else
                    {
                        var lastOff = pendingOff.Value;
                        GlyphPointF implied = new GlyphPointF(
                            (lastOff.X + pt.X) * 0.5f,
                            (lastOff.Y + pt.Y) * 0.5f,
                            true);

                        var c0 = ToShapeSpace(scale, currentOn);
                        var c1 = ToShapeSpace(scale, lastOff);
                        var c2 = ToShapeSpace(scale, implied);
                        PtrSpan<EdgeSegment>.Push(ref contour.Edges, new(
                            new Vector2((float)c0.X, (float)c0.Y),
                            new Vector2((float)c1.X, (float)c1.Y),
                            new Vector2((float)c2.X, (float)c2.Y),
                            EdgeColor.White
                        ));

                        currentOn = implied;
                        pendingOff = pt;
                    }
                }
            }

            // Close
            if (pendingOff.HasValue)
            {
                var c0 = ToShapeSpace(scale, currentOn);
                var c1 = ToShapeSpace(scale, pendingOff.Value);
                var c2 = ToShapeSpace(scale, firstPt);
                PtrSpan<EdgeSegment>.Push(ref contour.Edges, new(
                    new Vector2((float)c0.X, (float)c0.Y),
                    new Vector2((float)c1.X, (float)c1.Y),
                    new Vector2((float)c2.X, (float)c2.Y),
                    EdgeColor.White
                ));
            }
            else
            {
                var c0 = ToShapeSpace(scale, currentOn);
                var c1 = ToShapeSpace(scale, firstPt);
                PtrSpan<EdgeSegment>.Push(ref contour.Edges, new(
                    new Vector2((float)c0.X, (float)c0.Y),
                    new Vector2((float)c1.X, (float)c1.Y)
                ));
            }
            
            shape.AddContour(contour);
            return true;
        }

        //private static Vector2 ToVec(GlyphPointF pt, double scale) =>
        //    new (pt.P.X / scale, pt.P.Y / scale);

        public static bool GetKerning(out float kerning, Typeface font, uint unicode1, uint unicode2, FontCoordinateScaling scaling)
        {
            kerning = 0;
            if (font.KernTable == null)
                return false;

            kerning = font.GetKernDistance(font.GetGlyphIndex((int)unicode1), font.GetGlyphIndex((int)unicode2));
            
            if (kerning == 0)
                return false;

            kerning *= GetFontCoordinateScale(font, scaling);
            return true;
        }

    }
}