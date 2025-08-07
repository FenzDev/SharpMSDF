using System;
using Typography.OpenFont;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using static System.Formats.Asn1.AsnWriter;
using NumericsVector2 = System.Numerics.Vector2;
using static System.Net.Mime.MediaTypeNames;
using SharpMSDF.Core;

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
        public double EmSize;
        /// The vertical position of the ascender and descender relative to the baseline.
        public double AscenderY, DescenderY;
        /// The vertical difference between consecutive baselines.
        public double LineHeight;
        /// The vertical position and thickness of the underline.
        public double UnderlineY/*, UnderlineThickness*/;
    };

    public static class FontImporter
    {

        public static Typeface LoadFont(string filename)
        {
            using FileStream file = File.OpenRead(filename);
            OpenFontReader reader = new OpenFontReader();
            return reader.Read(file);
        }
        public static double GetFontCoordinateScale(Typeface face, FontCoordinateScaling coordinateScaling) {
            switch (coordinateScaling) {
                case FontCoordinateScaling.None:
                    return 1;
                case FontCoordinateScaling.EmNormalized:
                    return 1.0 / (face.UnitsPerEm!=0? face.UnitsPerEm: 1.0);
                case FontCoordinateScaling.LegacyNormalized:
                    return 1.0 / 64.0;
            }
            return 1;
        }


        public static bool GetFontMetrics(out FontMetrics metrics, Typeface face, FontCoordinateScaling coordinateScaling)
        {
            double scale = GetFontCoordinateScale(face, coordinateScaling);
            metrics.EmSize = scale * face.UnitsPerEm;
            metrics.AscenderY = scale * face.Ascender;
            metrics.DescenderY = scale * face.Descender;
			metrics.LineHeight = scale * (face.Ascender - face.Descender + face.LineGap);
			metrics.UnderlineY = scale * face.UnderlinePosition;
            //metrics.UnderlineThickness = not implemented
            return true;
        }

        //public static double GetFontScale(Typeface face)
        //{
        //    return face.UnitsPerEm / 64.0;
        //}

        public static void GetFontWhitespaceWidth(ref double spaceAdvance, ref double tabAdvance, Typeface font)
        {
            ushort glyphIdx;
            glyphIdx = font.GetGlyphIndex(' ');
            spaceAdvance = font.GetAdvanceWidthFromGlyphIndex(glyphIdx) / 64.0;
            glyphIdx = font.GetGlyphIndex('\t');
            tabAdvance = font.GetAdvanceWidthFromGlyphIndex(glyphIdx) / 64.0;
        }

        public static ushort EstimateGlyphCapacities(Typeface typeface, uint unicode, out int maxContours, out int maxSegments)
        {
            var index = typeface.GetGlyphIndex((int)unicode);
            var glyph = typeface.GetGlyph(index);

            // TODO: Finish
            maxContours = 0;
            maxSegments = 0;
        }

        /// <summary>
        /// Loads a glyph from a Typography Typeface into a <see cref="Shape"/>,
        /// </summary>
        public static Shape LoadGlyph(
            Typeface typeface,
            uint unicode,
            FontCoordinateScaling scaling,

            ref double advance
            )
        {

            //const double scale = 1.0 / 64;
            ushort glyphIndex = typeface.GetGlyphIndex((int)unicode);
			if (glyphIndex == 0)
			{
				advance = 0;
			}
			var glyph = typeface.GetGlyph(glyphIndex);

            int advUnits = typeface.GetAdvanceWidthFromGlyphIndex(glyphIndex);

            //const int padding = 0;      // pixels

            // 1) Raw glyph bounds in face units
            var bounds = glyph.Bounds;
            double wUnits = bounds.XMax - bounds.XMin;
            double hUnits = bounds.YMax - bounds.YMin;

            // 2) Compute padded bitmap dimensions
            //idealWidth = (float)wUnits / 64f; // + padding * 2;
            //idealHeight = (float)hUnits / 64f; // + padding * 2;

            // 3) Compute offset so that glyph’s bottom‐left maps to (padding, padding)


            double scale = GetFontCoordinateScale(typeface, scaling);
            advance = advUnits * scale;

            //double offsetX = bounds.XMin /*/ div*/; // + padding
            //double offsetY = -bounds.YMin /*/ div*/; // + padding

            (double X, double Y) ToShapeSpace(GlyphPointF p)
                => ((p.X) * scale, (p.Y) * scale);

            Span<GlyphPointF> pts = glyph.GlyphPoints;
            Span<ushort> ends = glyph.EndPoints;
            int start = 0;
            for (int i = 0; i < ends.Length; i++)
            {
                int count = ends[i] - start + 1;
                if (count <= 0) { start = ends[i] + 1; continue; }
                var contourPts = GlyphPointF[count];
                for (int e = start; e <= ends[e]; e++)
                    contourPts.Add(pts[e]);

                Contour contour = new Contour();

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

                for (int e = idx0; e < contourPts.Count; e++)
                {
                    var pt = contourPts[e];
                    if (pt.onCurve)
                    {
                        if (pendingOff.HasValue)
                        {
                            var c0 = ToShapeSpace(currentOn);
                            var c1 = ToShapeSpace(pendingOff.Value);
                            var c2 = ToShapeSpace(pt);
                            
                            contour.Edges.Add(new (
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
                            var c0 = ToShapeSpace(currentOn);
                            var c1 = ToShapeSpace(pt);
                            contour.Edges.Add(new (
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

                            var c0 = ToShapeSpace(currentOn);
                            var c1 = ToShapeSpace(lastOff);
                            var c2 = ToShapeSpace(implied);
                            contour.Edges.Add(new (
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
                    var c0 = ToShapeSpace(currentOn);
                    var c1 = ToShapeSpace(pendingOff.Value);
                    var c2 = ToShapeSpace(firstPt);
                    contour.Edges.Add(new (
                        new Vector2((float)c0.X, (float)c0.Y),
                        new Vector2((float)c1.X, (float)c1.Y),
                        new Vector2((float)c2.X, (float)c2.Y),
                        EdgeColor.White
                    ));
                }
                else
                {
                    var c0 = ToShapeSpace(currentOn);
                    var c1 = ToShapeSpace(firstPt);
                    contour.Edges.Add(new (
                        new Vector2((float)c0.X, (float)c0.Y),
                        new Vector2((float)c1.X, (float)c1.Y)
                    ));
                }

                shape.Contours.Add(contour);
                start = end + 1;
            }

            return shape;
        }


        private static Vector2 ToVec(GlyphPointF pt, double scale) =>
            new (pt.P.X / scale, pt.P.Y / scale);

        public static bool GetKerning(out double kerning, Typeface font, uint unicode1, uint unicode2, FontCoordinateScaling scaling)
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

        private static Span<int> GiveShit(int count)
        {
            Span<int> some = stackalloc int[count];
            return some;
        }

    }
}