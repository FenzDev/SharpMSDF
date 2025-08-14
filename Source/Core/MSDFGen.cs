using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
// Equivalent to: typedef BitmapRef<float, 1>
using BitmapRefSingle = SharpMSDF.Core.BitmapRef<float>;
// Equivalent to: typedef BitmapRef<float, 3>
using BitmapRefMulti = SharpMSDF.Core.BitmapRef<float>;
// Equivalent to: typedef BitmapRef<float, 4>
using BitmapRefMultiAndTrue = SharpMSDF.Core.BitmapRef<float>;
using Typography.OpenFont;
using Typography.OpenFont.Tables;
using System.Numerics;
using System.Runtime.InteropServices;
using SharpMSDF.Utilities;


namespace SharpMSDF.Core
{
    public unsafe static class MSDFGen
    {
        public unsafe interface DistancePixelConversion<TDistanceSelector, TDistance>
            where TDistanceSelector : IDistanceSelector<TDistanceSelector, TDistance>
        {
            public DistanceMapping Mapping { get; set; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Convert(float* pixels, TDistance distance, int s = 0, int s2 = 0);
        }

        public unsafe struct DistancePixelConversionTrue : DistancePixelConversion<TrueDistanceSelector, float>
        {
            public DistanceMapping Mapping { get; set; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Convert(float* pixels, float distance, int s=0, int s2=0)
            {
                *pixels = (float)Mapping[distance];
            }
        }
        public unsafe struct DistancePixelConversionPerpendicular : DistancePixelConversion<PerpendicularDistanceSelector, float>
        {
            public DistanceMapping Mapping { get; set; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Convert(float* pixels, float distance, int s=0, int s2=0)
            {
                *pixels = (float)Mapping[distance];
            }
        }

        public unsafe struct DistancePixelConversionMulti : DistancePixelConversion<MultiDistanceSelector, MultiDistance>
        {
            public DistanceMapping Mapping { get; set; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Convert(float* pixels, MultiDistance distance, int s=0, int s2=0)
            {
                *pixels = (float)Mapping[distance.R];
                *(pixels+1) = (float)Mapping[distance.G];
                *(pixels+2) = (float)Mapping[distance.B];
            }
        }

        public unsafe struct DistancePixelConversionMultiAndTrue : DistancePixelConversion<MultiAndTrueDistanceSelector,MultiAndTrueDistance>
        {
            public DistanceMapping Mapping { get; set; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Convert(float* pixels, MultiAndTrueDistance distance, int s=0, int s2=0)
            {
                *pixels = (float)Mapping[distance.R];
                *(pixels+1) = (float)Mapping[distance.G];
                *(pixels+2) = (float)Mapping[distance.B];
                *(pixels+3) = (float)Mapping[distance.A];
            }
        }

        /// <summary>
        /// Generates a conventional single-channel signed distance field.
        /// </summary>
        public unsafe static void GenerateDistanceField<TCombiner, TConverter, TDistanceSelector, TDistance>(BitmapRefSingle output, Shape shape, Span<EdgeCache> cache, int* windingsCache, TDistanceSelector* selectorCache, SDFTransformation transformation) 
            where TDistanceSelector : IDistanceSelector<TDistanceSelector, TDistance>, new() 
            where TCombiner : IContourCombiner<TDistanceSelector, TDistance>, new()
            where TConverter : DistancePixelConversion<TDistanceSelector, TDistance>, new()
        {

            // 1. Create the converter 
            var converter = new TConverter() { Mapping = transformation.DistanceMapping };
            // 2. Create your combiner‐driven distance finder
            var distanceFinder = new ShapeDistanceFinder<TCombiner, TDistanceSelector, TDistance>(shape, cache, windingsCache, selectorCache);

            // 3. Parallel loop over rows
            bool rightToLeft = false;

            fixed (float* arrayFixed = output.Pixels)
            {
                // used to trick compiler into thinking this is not fixed when dealing with lambda expression
                float* pixels = arrayFixed; 

                for (int y = 0; y < output.SubHeight; y++)
                //Parallel.For(0, output.SubHeight, y =>
                {
                    int row = shape.InverseYAxis ? output.SubHeight - y - 1 : y;
                    for (int col = 0; col < output.SubWidth; col++)
                    {
                        int x = rightToLeft ? output.SubWidth - col - 1 : col;
                        // unproject into Shape‐space
                        var p = transformation.Projection.Unproject(new Vector2(x + .5f, y + .5f));
                        // get the signed‐distance
                        TDistance dist = distanceFinder.Distance(p);
                        // write into the pixel Buffer
                        float* pixel = pixels + output.GetIndex(x, row);
                        converter.Convert(pixel, dist, x, row);
                    }
                    rightToLeft = !rightToLeft; // flip for “staggered” ordering
                }
            }
        }

        /// <summary>
        /// Generates a conventional single-channel signed distance field.
        /// </summary>
        public static void GenerateSDF(BitmapRefSingle output, Shape shape, Span<EdgeCache> cache, SDFTransformation transformation, GeneratorConfig config = default)
        {

            if (config.OverlapSupport)
            {
                int* windings = stackalloc int[shape.Contours.Count];
                TrueDistanceSelector* selectors = stackalloc TrueDistanceSelector[shape.Contours.Count];
                GenerateDistanceField<OverlappingContourCombiner<TrueDistanceSelector, float>, DistancePixelConversionTrue, TrueDistanceSelector, float>
                    (output, shape, cache, windings, selectors, transformation);
            }
            else
            {
                int* windings = null;
                TrueDistanceSelector* selectors = null;
                GenerateDistanceField<SimpleContourCombiner<TrueDistanceSelector, float>, DistancePixelConversionTrue, TrueDistanceSelector, float>
                    (output, shape, cache, windings, selectors, transformation);
            }
        }

        /// <summary>
        /// Generates a single-channel signed perpendicular distance field.
        /// </summary>
        public static void GeneratePSDF(BitmapRefSingle output, Shape shape, Span<EdgeCache> cache, SDFTransformation transformation, GeneratorConfig config = default)
        {
            if (config.OverlapSupport)
            {
                int* windings = stackalloc int[shape.Contours.Count];
                PerpendicularDistanceSelector* selectors = stackalloc PerpendicularDistanceSelector[shape.Contours.Count];

                GenerateDistanceField<OverlappingContourCombiner<PerpendicularDistanceSelector, float>, DistancePixelConversionPerpendicular, PerpendicularDistanceSelector, float>
                    (output, shape, cache, windings, selectors, transformation);
            }
            else
            {
                int* windings = null;
                PerpendicularDistanceSelector* selectors = null;

                GenerateDistanceField<SimpleContourCombiner<PerpendicularDistanceSelector, float>, DistancePixelConversionPerpendicular, PerpendicularDistanceSelector, float>
                    (output, shape, cache, windings, selectors, transformation);
            }
        }

        /// <summary>
        /// Generates a multi-channel signed distance field. Edge colors must be assigned first! (See edgeColoringSimple)
        /// </summary>
        public static void GenerateMSDF(BitmapRefMulti output, Shape shape, Span<EdgeCache> cache, SDFTransformation transformation, MSDFGeneratorConfig config = default)
        {
            if (config.OverlapSupport)
            {
                int* windings = stackalloc int[shape.Contours.Count];
                MultiDistanceSelector* selectors = stackalloc MultiDistanceSelector[shape.Contours.Count];

                GenerateDistanceField<OverlappingContourCombiner<MultiDistanceSelector, MultiDistance>, DistancePixelConversionMulti, MultiDistanceSelector, MultiDistance>
                    (output, shape, cache, windings, selectors, transformation);
                MSDFErrorCorrection.ErrorCorrection<OverlappingContourCombiner<PerpendicularDistanceSelector, float>>(output, shape, cache, transformation, config);
            }
            else
            {
                int* windings = stackalloc int[shape.Contours.Count];
                MultiDistanceSelector* selectors = stackalloc MultiDistanceSelector[shape.Contours.Count];

                GenerateDistanceField<SimpleContourCombiner<MultiDistanceSelector, MultiDistance>, DistancePixelConversionMulti, MultiDistanceSelector, MultiDistance>
                    (output, shape, cache, windings, selectors, transformation);
                MSDFErrorCorrection.ErrorCorrection<SimpleContourCombiner<PerpendicularDistanceSelector, float>>(output, shape, cache, transformation, config);
            }
        }

        /// <summary>
        /// Generates a multi-channel signed distance field with true distance in the alpha channel. Edge colors must be assigned first.
        /// </summary>
        public static void GenerateMTSDF(BitmapRefMultiAndTrue output, Shape shape, Span<EdgeCache> cache, SDFTransformation transformation, MSDFGeneratorConfig config = default)
        {
            if (config.OverlapSupport)
            {
                int* windings = stackalloc int[shape.Contours.Count];
                MultiAndTrueDistanceSelector* selectors = stackalloc MultiAndTrueDistanceSelector[shape.Contours.Count];

                GenerateDistanceField<OverlappingContourCombiner<MultiAndTrueDistanceSelector, MultiAndTrueDistance>, DistancePixelConversionMultiAndTrue, MultiAndTrueDistanceSelector, MultiAndTrueDistance>
                    (output, shape, cache, windings, selectors, transformation);
                MSDFErrorCorrection.ErrorCorrection<OverlappingContourCombiner<PerpendicularDistanceSelector, float>>(output, shape, cache, transformation, config);
            }
            else
            {
                int* windings = stackalloc int[shape.Contours.Count];
                MultiAndTrueDistanceSelector* selectors = stackalloc MultiAndTrueDistanceSelector[shape.Contours.Count];

                GenerateDistanceField<SimpleContourCombiner<MultiAndTrueDistanceSelector, MultiAndTrueDistance>, DistancePixelConversionMultiAndTrue, MultiAndTrueDistanceSelector, MultiAndTrueDistance>
                    (output, shape, cache, windings, selectors, transformation);
                MSDFErrorCorrection.ErrorCorrection<SimpleContourCombiner<PerpendicularDistanceSelector, float>>(output, shape, cache, transformation, config);
            }
        }


    }
}
