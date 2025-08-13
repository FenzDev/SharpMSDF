using SharpMSDF.Core; // for DoubleRange
using SharpMSDF.Atlas;
using System.Runtime.InteropServices; // for Padding, GlyphGeometry, DimensionsConstraint, RectanglePacker

namespace SharpMSDF.Atlas
{
    public class TightAtlasPacker
    {
        int width, height;
        int spacing;
        DimensionsConstraint dimensionsConstraint;
        float scale, minScale;
        DoubleRange unitRange, pxRange;
        float miterLimit;
        bool pxAlignOriginX, pxAlignOriginY;
        Padding innerUnitPadding, outerUnitPadding;
        Padding innerPxPadding, outerPxPadding;
        float scaleMaximizationTolerance;

        public TightAtlasPacker()
        {
            width = -1;
            height = -1;
            spacing = 0;
            dimensionsConstraint = DimensionsConstraint.PowerOfTwoRectangle;
            scale = -1;
            minScale = 1;
            unitRange = new DoubleRange(0);
            pxRange = new DoubleRange(0);
            miterLimit = 0;
            pxAlignOriginX = pxAlignOriginY = false;
            scaleMaximizationTolerance = 0.001f;
        }

        /// <summary>
        /// Computes the layout for the array of glyphs. Returns 0 on success
        /// </summary>
        public unsafe int Pack(ref List<GlyphGeometry> glyphs)
        {
            int packResult = -1;
            fixed (GlyphGeometry* glyphsPtr = CollectionsMarshal.AsSpan(glyphs))
            {
                packResult = Pack(glyphsPtr, glyphs.Count);
            }
            return packResult;
        }

        /// <summary>
        /// Computes the layout for the array of glyphs. Returns 0 on success
        /// </summary>
        public unsafe int Pack(GlyphGeometry* glyphs, int count)
        {
            float initialScale = scale > 0 ? scale : minScale;
            if (initialScale > 0)
            {
                int result = TryPack(glyphs, count, dimensionsConstraint, ref width, ref height, initialScale);
                if (result != 0) return result;
            }
            else if (width < 0 || height < 0)
            {
                return -1;
            }

            if (scale <= 0)
                scale = PackAndScale(glyphs, count);
            if (scale <= 0)
                return -1;

            return 0;
        }

        /// <summary>
        /// Sets the atlas's fixed dimensions
        /// </summary>
        public void SetDimensions(int w, int h)
        {
            width = w;
            height = h;
        }

        /// <summary>
        /// Sets the atlas's dimensions to be determined during pack
        /// </summary>
        public void UnsetDimensions()
        {
            width = height = -1;
        }

        /// <summary>
        /// Sets the constraint to be used when determining dimensions
        /// </summary>
        public void SetDimensionsConstraint(DimensionsConstraint dc)
            => dimensionsConstraint = dc;

        /// <summary>
        /// Sets the spacing between glyph boxes
        /// </summary>
        public void SetSpacing(int s)
            => spacing = s;

        /// <summary>
        /// Sets fixed glyph scale
        /// </summary>
        public void SetScale(float s)
            => scale = s;

        /// <summary>
        /// Sets the minimum glyph scale
        /// </summary>
        public void SetMinimumScale(float ms)
            => minScale = ms;

        /// <summary>
        /// Sets the unit component of the total distance range
        /// </summary>
        public void SetUnitRange(DoubleRange ur)
            => unitRange = ur;

        /// <summary>
        /// Sets the pixel component of the total distance range
        /// </summary>
        public void SetPixelRange(DoubleRange pr)
            => pxRange = pr;

        /// <summary>
        /// Sets the miter limit for bounds computation
        /// </summary>
        public void SetMiterLimit(float ml)
            => miterLimit = ml;

        /// <summary>
        /// Sets whether each glyph's origin point should stay aligned with the pixel grid
        /// </summary>
        public void SetOriginPixelAlignment(bool align)
            => pxAlignOriginX = pxAlignOriginY = align;

        /// <summary>
        /// Sets whether each glyph's origin point should stay aligned with the pixel grid
        /// </summary>
        public void SetOriginPixelAlignment(bool alignX, bool alignY)
        {
            pxAlignOriginX = alignX;
            pxAlignOriginY = alignY;
        }

        /// <summary>
        /// Sets the unit component of width of additional padding that is part of each glyph quad
        /// </summary>
        public void SetInnerUnitPadding(Padding p)
            => innerUnitPadding = p;

        /// <summary>
        /// Sets the unit component of width of additional padding around each glyph quad
        /// </summary>
        public void SetOuterUnitPadding(Padding p)
            => outerUnitPadding = p;

        /// <summary>
        /// Sets the pixel component of width of additional padding that is part of each glyph quad
        /// </summary>
        public void SetInnerPixelPadding(Padding p)
            => innerPxPadding = p;

        /// <summary>
        /// Sets the pixel component of width of additional padding around each glyph quad
        /// </summary>
        public void SetOuterPixelPadding(Padding p)
            => outerPxPadding = p;

        /// <summary>
        /// Outputs the atlas's final dimensions
        /// </summary>
        public void GetDimensions(out int w, out int h)
        {
            w = width;
            h = height;
        }

        /// <summary>
        /// Returns the final glyph scale
        /// </summary>
        public float GetScale() => scale;

        /// <summary>
        /// Returns the final combined pixel range (including converted unit range)
        /// </summary>
        public DoubleRange GetPixelRange() => pxRange + unitRange * scale;


        //------------------------------------------------------------------------------
        // Internals
        //------------------------------------------------------------------------------

        unsafe int TryPack(GlyphGeometry* glyphs, int count, DimensionsConstraint dc, ref int w, ref int h, float s)
        {
            // Prepare boxes
            var rects = new List<AtlasRectangle>(count);
            fixed (GlyphGeometry** rectGlyphs = new GlyphGeometry*[count])
            {
                int rectGlyphsI = 0;
                var attribs = new GlyphGeometry.GlyphAttributes
                {
                    Scale = s,
                    Range = unitRange + pxRange / s,
                    InnerPadding = innerUnitPadding + innerPxPadding / s,
                    OuterPadding = outerUnitPadding + outerPxPadding / s,
                    MiterLimit = miterLimit,
                    PxAlignOriginX = pxAlignOriginX,
                    PxAlignOriginY = pxAlignOriginY
                };

                for (int i = 0; i < count; i++)
                {
                    var g = glyphs[i];
                    if (!g.IsWhitespace())
                    {
                        g.WrapBox(attribs);
                        g.GetBoxSize(out int rw, out int rh);
                        if (rw > 0 && rh > 0)
                        {
                            rects.Add(new(0, 0, rw, rh));
                            rectGlyphs[rectGlyphsI++] = &glyphs[i];
                        }
                        glyphs[i] = g;
                    }
                }

                if (rects.Count == 0)
                {
                    if (w < 0 || h < 0) { w = h = 0; }
                    return 0;
                }

                // Pack
                if (w < 0 || h < 0)
                {
                    (int pw, int ph) = dc switch
                    {
                        DimensionsConstraint.PowerOfTwoSquares => RectanglePacker.PackWithSelector<SquarePowerOfTwoSizeSelector>(rects, spacing),
                        DimensionsConstraint.PowerOfTwoRectangle => RectanglePacker.PackWithSelector<PowerOfTwoSizeSelector>(rects, spacing),
                        DimensionsConstraint.MultipleOfFourSquare => RectanglePacker.PackWithSelector<SquareSizeSelector>(rects, spacing),
                        DimensionsConstraint.EvenSquare => RectanglePacker.PackWithSelector<SquareSizeSelector>(rects, spacing),
                        _ /*Square*/                            => RectanglePacker.PackWithSelector<SquareSizeSelector>(rects, spacing)
                    };
                    if (pw <= 0 || ph <= 0) return -1;
                    w = pw; h = ph;
                }
                else
                {
                    int result = RectanglePacker.Pack(rects, w, h, spacing);
                    if (result != 0) return result;
                }

                // Place
                for (int i = 0; i < rects.Count; i++)
                {
                    var box = rects[i];
                    // flip Y origin
                    *rectGlyphs[i] = (*rectGlyphs[i]).PlaceBox(box.X, h - (box.Y + box.Height));
                }

                return 0;

            }
        }

        unsafe float PackAndScale(GlyphGeometry* glyphs, int count)
        {
            bool success;
            int w = width, h = height;
            float lo = 1, hi = 1;

            if (success = TryPack(glyphs, count, dimensionsConstraint, ref w, ref h, 1) == 0)
            {
                // find upper bound
                while (hi < 1e32 && (success = TryPack(glyphs, count, dimensionsConstraint, ref w, ref h, hi = 2*lo) == 0))
                    lo = hi;
            }
            else
            {
                // find lower bound
                while (lo > 1e-32 && !(success = TryPack(glyphs, count, dimensionsConstraint, ref w, ref h, lo = .5f*hi) == 0))
                    hi = lo;
            }

            if (lo == hi)
                return 0;

            // binary search
            while (lo / hi < 1 - scaleMaximizationTolerance)
            {
                float mid = 0.5f * (lo + hi);
                if (success = TryPack(glyphs, count, dimensionsConstraint, ref w, ref h, mid) == 0)
                    lo = mid;
                else
                    hi = mid;
            }

            if (!success)
                TryPack(glyphs, count, dimensionsConstraint, ref w, ref h, lo);

            return lo;
        }
    }
}
