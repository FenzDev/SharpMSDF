using SharpMSDF.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SharpMSDF.Core
{
    public static class Rasterization
    {
        /// Rasterizes the Shape into a monochrome bitmap.
        public static void Rasterize(BitmapRef<float> output, in Shape shape, in Projection projection, FillRule fillRule = FillRule.FILL_NONZERO)
        {
            Scanline scanline = new();
            for (int y = 0; y < output.SubHeight; ++y)
            {
                int row = shape.InverseYAxis ? output.SubHeight - y - 1 : y;
                shape.Scanline(scanline, projection.UnprojectY(y + .5f));
                for (int x = 0; x < output.SubWidth; ++x)
                    output[x, row] = scanline.Filled(projection.UnprojectX(x + .5f), fillRule) ? 1.0f: 0.0f;
            }
        }
        /// Fixes the sign of the input signed distance field, so that it matches the Shape's rasterized fill.
        public static void DistanceSignCorrection(in BitmapRef<float> sdf, in Shape shape, in Projection projection, FillRule fillRule = FillRule.FILL_NONZERO)
        {
            Span<Scanline.Intersection> intersections = stackalloc Scanline.Intersection[shape.GetEdgesCount()];
            Scanline scanline = new(intersections);
            for (int y = 0; y < sdf.SubHeight; ++y)
            {
                int row = shape.InverseYAxis ? sdf.SubHeight - y - 1 : y;
                shape.Scanline(scanline, projection.UnprojectY(y + .5f));
                for (int x = 0; x < sdf.SubWidth; ++x)
                {
                    bool fill = scanline.Filled(projection.UnprojectX(x + .5f), fillRule);
                    if ((sdf[x, row] > .5f) != fill)
                        sdf[x, row] = 1.0f - sdf[x, row];
                }
            }
        }

        static unsafe void multiDistanceSignCorrection(BitmapRef<float> sdf, Shape shape, Projection projection, FillRule fillRule) {
            int w = sdf.SubWidth, h = sdf.SubHeight;
            if (w * h == 0)
                return;

            Scanline scanline = new Scanline();
            bool ambiguous = false;

            Span<sbyte> matchMap = stackalloc sbyte[w * h];
            int matchIndex = 0;

            fixed (float* pixels = sdf.Pixels)
            {

                for (int y = 0; y < h; ++y)
                {
                    int row = shape.InverseYAxis ? h - y - 1 : y;
                    shape.Scanline(scanline, projection.UnprojectY(y + 0.5f));

                    for (int x = 0; x < w; ++x)
                    {
                        bool fill = scanline.Filled(projection.UnprojectX(x + 0.5f), fillRule);

                        //float *Msd = sdf(x, row);
                        float* msd = pixels + sdf.GetIndex(x, y);

                        float sd = Arithmetic.Median(msd[0], msd[1], msd[2]);

                        if (sd == 0.5f)
                            ambiguous = true;
                        else if ((sd > 0.5f) != fill)
                        {
                            msd[0] = 1f - msd[0];
                            msd[1] = 1f - msd[1];
                            msd[2] = 1f - msd[2];
                            matchMap[matchIndex] = -1;
                        }
                        else
                            matchMap[matchIndex] = 1;

                        if (sdf.N >= 4 && ((msd[3] > 0.5f) != fill))
                            msd[3] = 1f - msd[3];

                        matchIndex++;
                    }
                }


                // This step is necessary to avoid artifacts when whole Shape is inverted
                if (ambiguous)
                {
                    for (int y = 0; y < h; ++y)
                    {
                        int row = shape.InverseYAxis ? h - y - 1 : y;

                        for (int x = 0; x < w; ++x)
                        {
                            int index = y * w + x;
                            if (matchMap[index] == 0)
                            {
                                int neighborMatch = 0;
                                if (x > 0) neighborMatch += matchMap[index - 1];
                                if (x < w - 1) neighborMatch += matchMap[index + 1];
                                if (y > 0) neighborMatch += matchMap[index - w];
                                if (y < h - 1) neighborMatch += matchMap[index + w];

                                if (neighborMatch < 0)
                                {
                                    //float *Msd = sdf(x, row);
                                    float* msd = pixels + sdf.GetIndex(x,y);
                                    msd[0] = 1f - msd[0];
                                    msd[1] = 1f - msd[1];
                                    msd[2] = 1f - msd[2];
                                }
                            }
                        }
                    }
                }

            }
        }

        static void DistanceSignCorrection(BitmapRef<float> sdf, Shape shape, Projection projection, FillRule fillRule)
        {
            // BitmapRef<float,3> BitmapRef<float, 4>
            multiDistanceSignCorrection(sdf, shape, projection, fillRule);
        }
    }
}
