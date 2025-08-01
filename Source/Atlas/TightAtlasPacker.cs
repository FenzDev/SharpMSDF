using SharpMSDF.Core; // for DoubleRange
using SharpMSDF.Atlas; // for Padding, GlyphGeometry, DimensionsConstraint, RectanglePacker

namespace SharpMSDF.Atlas
{
    public class TightAtlasPacker
    {
        int width, height;
        int spacing;
        DimensionsConstraint dimensionsConstraint;
        double scale, minScale;
        DoubleRange unitRange, pxRange;
        double miterLimit;
        bool pxAlignOriginX, pxAlignOriginY;
        Padding innerUnitPadding, outerUnitPadding;
        Padding innerPxPadding, outerPxPadding;
        double scaleMaximizationTolerance;

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
            scaleMaximizationTolerance = 0.001;
        }

        public unsafe int Pack(ref List<GlyphGeometry> glyphs)
        {
            GlyphGeometry[] glyphsArr = new GlyphGeometry[glyphs.Count];
            glyphs.CopyTo(glyphsArr, 0);
            int packResult = -1;
            fixed (GlyphGeometry* glyphsPtr = glyphsArr)
            {
                packResult = Pack(glyphsPtr, glyphsArr.Length);
                glyphs = new(glyphsArr);
            }
            return packResult;
        }
        public unsafe int Pack(GlyphGeometry* glyphs, int count)
        {
            double initialScale = scale > 0 ? scale : minScale;
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

        public void SetDimensions(int w, int h)
        {
            width = w;
            height = h;
        }

        public void UnsetDimensions()
        {
            width = height = -1;
        }

        public void SetDimensionsConstraint(DimensionsConstraint dc)
            => dimensionsConstraint = dc;

        public void SetSpacing(int s)
            => spacing = s;

        public void SetScale(double s)
            => scale = s;

        public void SetMinimumScale(double ms)
            => minScale = ms;

        public void SetUnitRange(DoubleRange ur)
            => unitRange = ur;

        public void SetPixelRange(DoubleRange pr)
            => pxRange = pr;

        public void SetMiterLimit(double ml)
            => miterLimit = ml;

        public void SetOriginPixelAlignment(bool align)
            => pxAlignOriginX = pxAlignOriginY = align;

        public void SetOriginPixelAlignment(bool alignX, bool alignY)
        {
            pxAlignOriginX = alignX;
            pxAlignOriginY = alignY;
        }

        public void SetInnerUnitPadding(Padding p)
            => innerUnitPadding = p;

        public void SetOuterUnitPadding(Padding p)
            => outerUnitPadding = p;

        public void SetInnerPixelPadding(Padding p)
            => innerPxPadding = p;

        public void SetOuterPixelPadding(Padding p)
            => outerPxPadding = p;

        public void GetDimensions(out int w, out int h)
        {
            w = width;
            h = height;
        }

        public double GetScale() => scale;

        public DoubleRange GetPixelRange() => pxRange + unitRange * scale;


        //------------------------------------------------------------------------------
        // Internals
        //------------------------------------------------------------------------------

        unsafe int TryPack(GlyphGeometry* glyphs, int count, DimensionsConstraint dc, ref int w, ref int h, double s)
        {
            // Prepare boxes
            var rects = new List<Rectangle>(count);
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
                        g.WrapBox(ref g, attribs);
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
                        DimensionsConstraint.PowerOfTwoSquares => RectanglePacker.PackWithSelector<SquarePowerOfTwoSizeSelector, Rectangle>(rects, spacing),
                        DimensionsConstraint.PowerOfTwoRectangle => RectanglePacker.PackWithSelector<PowerOfTwoSizeSelector, Rectangle>(rects, spacing),
                        DimensionsConstraint.MultipleOfFourSquare => RectanglePacker.PackWithSelector<SquareSizeSelector, Rectangle>(rects, spacing),
                        DimensionsConstraint.EvenSquare => RectanglePacker.PackWithSelector<SquareSizeSelector, Rectangle>(rects, spacing),
                        _ /*Square*/                            => RectanglePacker.PackWithSelector<SquareSizeSelector, Rectangle>(rects, spacing)
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

        unsafe double PackAndScale(GlyphGeometry* glyphs, int count)
        {
            bool success;
            int w = width, h = height;
            double lo = 1, hi = 1;

            if (success = TryPack(glyphs, count, dimensionsConstraint, ref w, ref h, 1) == 0)
            {
                // find upper bound
                while (hi < 1e32 && (success = TryPack(glyphs, count, dimensionsConstraint, ref w, ref h, hi = 2*lo) == 0))
                    lo = hi;
            }
            else
            {
                // find lower bound
                while (lo > 1e-32 && !(success = TryPack(glyphs, count, dimensionsConstraint, ref w, ref h, lo = .5*hi) == 0))
                    hi = lo;
            }

            if (lo == hi)
                return 0;

            // binary search
            while (lo / hi < 1 - scaleMaximizationTolerance)
            {
                double mid = 0.5 * (lo + hi);
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
