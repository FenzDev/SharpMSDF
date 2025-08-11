using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SharpMSDF.Atlas
{
    public struct RectanglePacker
    {
        public const int WORST_FIT = 0x7fffffff;

        public RectanglePacker() { }
        public RectanglePacker(int width, int height) 
        {
            if (width > 0 && height > 0)
                _Spaces.Add(new AtlasRectangle(0, 0, width, height));
        }


        static void RemoveFromUnorderedList<T>(ref Span<T> span, int index)
        {
            if (index != span.Length - 1)
                (span[index], span[^1]) = (span[^1], span[index]);
            span = span[.. (span.Length - 1)];
        }
        static void RemoveFromUnorderedList<T>(List<T> list, int index)
        {
            if (index != list.Count - 1)
                (list[index], list[^1]) = (list[^1], list[index]);
            list.RemoveAt(list.Count - 1);
        }

        /// <summary>
        /// Expands the packing area - both width and height must be greater or equal to the previous value
        /// </summary>
        public void Expand(int width, int height)
        {
            if (width > 0 && height > 0)
            {
                int oldWidth = 0, oldHeight = 0;
                for (int s = 0; s < _Spaces.Count; s++) {
                    var space = _Spaces[s];
                    if (space.X + space.Width > oldWidth)
                        oldWidth = space.X + space.Width;
                    if (space.Y + space.Height > oldHeight)
                        oldHeight = space.Y + space.Height;
                }
                _Spaces.Add(new AtlasRectangle(0, 0, width, height ));
                SplitSpace(_Spaces.Count - 1, oldWidth, oldHeight);
            }
        }
        /// <summary>
        /// Packs the rectangle array, returns how many didn't fit (0 on success)
        /// </summary>
        public int Pack(List<AtlasRectangle> rectangles, int start = 0)
        {
            Span<int> remainingRects = stackalloc int[rectangles.Count - start];

            for (int i = 0; i < rectangles.Count - start; ++i)
                remainingRects[i] = i + start;
            while (remainingRects.Length > 0)
            {
                int bestFit = WORST_FIT;
                int bestSpace = -1;
                int bestRect = -1;
                AtlasRectangle rect;
                for (int i = 0; i < _Spaces.Count; ++i)
                {
                    AtlasRectangle space = _Spaces[i];
                    for (int j = 0; j < remainingRects.Length; ++j)
                    {
                        rect = rectangles[remainingRects[j]];
                        if (rect.Width == space.Width && rect.Height == space.Height)
                        {
                            bestSpace = i;
                            bestRect = j;
                            goto BEST_FIT_FOUND;
                        }
                        if (rect.Width <= space.Width && rect.Height <= space.Height)
                        {
                            int fit = RateFit(rect.Width, rect.Height, space.Width, space.Height);
                            if (fit < bestFit)
                            {
                                bestSpace = i;
                                bestRect = j;
                                bestFit = fit;
                            }
                        }
                    }
                }
                if (bestSpace < 0 || bestRect < 0)
                    break;

                BEST_FIT_FOUND: 

                rect = rectangles[remainingRects[bestRect]];
                rect.X = _Spaces[bestSpace].X;
                rect.Y = _Spaces[bestSpace].Y;
                rectangles[remainingRects[bestRect]] = rect;
                SplitSpace(bestSpace, rect.Width, rect.Height);
                RemoveFromUnorderedList(ref remainingRects, bestRect);
            }
            return remainingRects.Length;
        }
        public int Pack(List<AtlasRectangle> spaces, List<AtlasRectangle> rectangles)
        {
            Span<int> remainingRects = stackalloc int[rectangles.Count];

            for (int i = 0; i < rectangles.Count; ++i)
                remainingRects[i] = i;
            while (remainingRects.Length > 0)
            {
                int bestFit = WORST_FIT;
                int bestSpace = -1;
                int bestRect = -1;
                bool bestRotated = false;
                AtlasRectangle rect;
                for (int i = 0; i < spaces.Count; ++i)
                {
                    AtlasRectangle space = spaces[i];
                    for (int j = 0; j < remainingRects.Length; ++j)
                    {
                        rect = rectangles[remainingRects[j]];
                        if (rect.Width == space.Width && rect.Height == space.Height)
                        {
                            bestSpace = i;
                            bestRect = j;
                            bestRotated = false;
                            goto BEST_FIT_FOUND;
                        }
                        if (rect.Height == space.Width && rect.Width == space.Height)
                        {
                            bestSpace = i;
                            bestRect = j;
                            bestRotated = true;
                            goto BEST_FIT_FOUND;
                        }
                        if (rect.Width <= space.Width && rect.Height <= space.Height)
                        {
                            int fit = RateFit(rect.Width, rect.Height, space.Width, space.Height);
                            if (fit < bestFit)
                            {
                                bestSpace = i;
                                bestRect = j;
                                bestRotated = false;
                                bestFit = fit;
                            }
                        }
                        if (rect.Height <= space.Width && rect.Width <= space.Height)
                        {
                            int fit = RateFit(rect.Height, rect.Width, space.Width, space.Height);
                            if (fit < bestFit)
                            {
                                bestSpace = i;
                                bestRect = j;
                                bestRotated = true;
                                bestFit = fit;
                            }
                        }

                    }
                }
                if (bestSpace < 0 || bestRect < 0)
                    break;

                BEST_FIT_FOUND:

                rect = rectangles[remainingRects[bestRect]];
                rect.X = spaces[bestSpace].X;
                rect.Y = spaces[bestSpace].Y;
                rectangles[remainingRects[bestRect]] = rect;
                if (bestRotated)
                    SplitSpace(bestSpace, rect.Height, rect.Width);
                else
                    SplitSpace(bestSpace, rect.Width, rect.Height);
                    
                RemoveFromUnorderedList(ref remainingRects, bestRect);
            }
            return remainingRects.Length;
        }

        List<AtlasRectangle> _Spaces = [];

        static int RateFit(int w, int h, int sw, int sh)
        {
            return Math.Min(sw-w, sh-h); 
        }

        void SplitSpace(int index, int w, int h)
        {
            AtlasRectangle space = _Spaces[index];
            RemoveFromUnorderedList(_Spaces, index);
            AtlasRectangle a = new ( space.X, space.Y + h, w, space.Height - h );
            AtlasRectangle b = new ( space.X + w, space.Y, space.Width - w, h );
            if (w * (space.Height - h) < h * (space.Width - w))
                a.Width = space.Width;
            else
                b.Height = space.Height;
            if (a.Width > 0 && a.Height > 0)
                _Spaces.Add(a);
            if (b.Width > 0 && b.Height > 0)
                _Spaces.Add(b);
        }

        static void CopyRectanglePlacement(ref AtlasRectangle dst, AtlasRectangle src)
        {
            dst.X = src.X;
            dst.Y = src.Y;
            dst.Rotated = src.Rotated;
        }


        /// <summary>
        /// Packs the rectangle array into an atlas with fixed dimensions.
        /// Returns the error code (0 on success, >0 if some didn't fit).
        /// </summary>
        public static int Pack(List<AtlasRectangle> rectangles, int width, int height, int spacing = 0)
        {
            // Expand each box by spacing
            if (spacing != 0)
            {
                for (int r = 0; r < rectangles.Count; r++)
                {
                    var rect = rectangles[r];
                    rect.Width += spacing;
                    rect.Height += spacing;
                    rectangles[r] = rect;
                }
            }

            // Delegate to your instance-based packer
            var packer = new RectanglePacker(width + spacing, height + spacing);
            int result = packer.Pack(rectangles);

            // Shrink back
            if (spacing != 0)
            {
                for (int r = 0; r < rectangles.Count; r++)
                {
                    var rect = rectangles[r];
                    rect.Width -= spacing;
                    rect.Height -= spacing;
                    rectangles[r] = rect;
                }
            }

            return result;
        }

        /// <summary>
        /// Packs the rectangle array into an atlas of unknown size.
        /// Uses a size‐selector to iterate possible atlas sizes until it fits.
        /// Returns the chosen (width, height).
        /// </summary>
        public static (int Width, int Height) PackWithSelector<SizeSelector>(List<AtlasRectangle> rectangles, int spacing = 0)
            where SizeSelector : ISizeSelector, new()
        {
            // Make a copy and expand by spacing
            var copy = rectangles
                .Select(r =>
                {
                    var c = new AtlasRectangle();
                    c.Width = r.Width + spacing;
                    c.Height = r.Height + spacing;
                    return c;
                })
                .ToList();

            // Compute total area (without spacing)
            int totalArea = rectangles.Sum(r => r.Width * r.Height);

            // Initialize selector
            var selector = new SizeSelector();
            selector.Initialize(totalArea);

            (int Width, int Height) dimensions = default;

            while (selector.Next(out int w, out int h))
            {
                var packer = new RectanglePacker(w + spacing, h + spacing);
                if (packer.Pack(copy) == 0)
                {
                    // success: record dims and copy placements back
                    dimensions = (w, h);
                    for (int i = 0; i < rectangles.Count; i++)
                    {
                        var tmpRect = rectangles[i];
                        CopyRectanglePlacement(ref tmpRect, copy[i]);
                        rectangles[i] = tmpRect;
                    }
                    selector.Decrement();
                }
                else
                {
                    selector.Increment();
                }
            }

            return dimensions;
        }
    }
}
