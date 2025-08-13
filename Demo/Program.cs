using System;
using System.CodeDom.Compiler;
using System.IO;
using Typography.OpenFont;
using SharpMSDF.IO;
using SharpMSDF.Core;
using System.Runtime.CompilerServices;
using OpMode = SharpMSDF.Core.ErrorCorrectionConfig.OpMode;
using ConfigDistanceCheckMode = SharpMSDF.Core.ErrorCorrectionConfig.ConfigDistanceCheckMode;
using SharpMSDF.Atlas;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Drawing;
using System.Buffers;
using static System.Net.Mime.MediaTypeNames;
using SharpMSDF.Utilities;
using System.Threading;
using System.Runtime.Intrinsics.X86;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Reflection.Metadata;

namespace SharpMSDF.Demo
{

    internal static class Program
    {
        static void Main(string[] args)
        {

            var font = FontImporter.LoadFont("micross.ttf");
            //OneGlyphGen(font, ' ');
            ImediateAtlasGen(font);
            //OnDemandAtlasGen(font);
        }

        private unsafe static void OneGlyphGen(Typeface font, uint unicode)
        {
            // Set some generation parameters 
            int scale = 64;
            float pxrange = 6.0f;
            float angleThereshold = 3.0f;

            // Load the glyph
            var glyphIndex = FontImporter.PreEstimateGlyph(font, unicode, out int maxContours, out int maxSegments);

            Contour* contoursPtr = stackalloc Contour[maxContours];
            PtrPool<Contour> contoursPool = new(contoursPtr, maxContours);
            EdgeSegment* segmentsPtr = stackalloc EdgeSegment[maxSegments];
            PtrPool<EdgeSegment> segmentsPool = new(segmentsPtr, maxSegments);

            float advance = 0.0f;
            var shape = FontImporter.LoadGlyph(font, glyphIndex, FontCoordinateScaling.EmNormalized, ref contoursPool, ref segmentsPool, ref advance);
            var msdf_ = ArrayPool<float>.Shared.Rent(scale * scale * 3);
            var msdf = new Bitmap<float>(msdf_, scale, scale, 3);

            shape.OrientContours(); // This will fix orientation of the windings
            shape.Normalize(); // Normalize the Shape geometry for distance field generation.
            EdgeColorings.Simple(ref shape, angleThereshold); // Assign colors to the edges of the shape, we use InkTrap technique here.

            // range = pxrange / scale
            var distMap = new DistanceMapping(new(pxrange / scale));
            var transformation = new SDFTransformation(new Projection(new(scale), new(0)), distMap);
            //    ^ Scale    ^ Translation  
            // Generate msdf
            Span<EdgeCache> cache = stackalloc EdgeCache[shape.EdgeCount()];
            MSDFGen.GenerateMSDF(
                msdf,
                shape,
                cache,
                transformation
            );

            // Save msdf output
            Png.SavePng(msdf, "output.png");

            // Save a rendering preview
            var rast_ = ArrayPool<float>.Shared.Rent(1024 * 1024);
            var rast = new Bitmap<float>(rast_, 1024, 1024);
            Render.RenderSdf(rast, msdf, pxrange);
            Png.SavePng(rast, "render.png");

            ArrayPool<float>.Shared.Return(rast_);
            ArrayPool<float>.Shared.Return(msdf_);
        }

        private unsafe static void ImediateAtlasGen(Typeface font)
        {
            List<GlyphGeometry> glyphs = new (font.GlyphCount);
            // FontGeometry is a helper class that loads a set of glyphs from a single font.
            // It can also be used to get additional font metrics, kerning information, etc.
            FontGeometry fontGeometry = new(glyphs);

            
            fontGeometry.PreEstimateGlyphCharset(font, Charset.ASCII, out var maxContours, out var maxSegments);
            
            Contour* contoursPtr = stackalloc Contour[maxContours];
            PtrPool<Contour> contoursPool = new(contoursPtr, maxContours);
            //bool isSegmentsPoolBig = maxSegments > 256;
            PtrPool<EdgeSegment> segmentsPool;
            EdgeSegment[] segmentArr = null; GCHandle handle = new GCHandle();
            //if (!isSegmentsPoolBig)
            //{
            //    EdgeSegment* segmentsPtr = stackalloc EdgeSegment[maxSegments];
            //    segmentsPool = new(segmentsPtr, maxSegments);
            //}
            /*else */if (OperatingSystem.IsBrowser())
            {
                segmentArr = ArrayPool<EdgeSegment>.Shared.Rent(maxSegments);
                handle = GCHandle.Alloc(segmentArr, GCHandleType.Pinned);
                EdgeSegment* segmentsPtr = (EdgeSegment*)handle.AddrOfPinnedObject();
                segmentsPool = new(segmentsPtr, maxSegments);
            }
            else
            {
                nuint size = (nuint)(sizeof(EdgeSegment) * maxSegments);
                EdgeSegment* segmentsPtr = (EdgeSegment*)NativeMemory.Alloc(size);
                segmentsPool = new(segmentsPtr, maxSegments);
            }

            // Load a set of character glyphs:
            // In the last argument, you can specify a charset other than ASCII.
            // To load specific glyph indices, use loadGlyphs instead.
            fontGeometry.LoadCharset(font, ref contoursPool, ref segmentsPool, 1.0f, Charset.ASCII);
            // Apply MSDF edge coloring. See EdgeColorings for other coloring strategies.
            const float maxCornerAngle = 3.0f;
            for (var g = 0; g < glyphs.Count; g++)
            {
                var glyph = glyphs[g];
                glyph.Shape.OrientContours();
                glyph.EdgeColoring(EdgeColorings.InkTrap, maxCornerAngle, 0);
                glyphs[g] = glyph;
            }
            // TightAtlasPacker class computes the layout of the atlas.
            TightAtlasPacker packer = new();
            // Set atlas parameters:
            // setDimensions or setDimensionsConstraint to find the best value
            packer.SetDimensionsConstraint(DimensionsConstraint.Square);
            // setScale for a fixed scale or setMinimumScale to use the largest that fits
            packer.SetMinimumScale(64.0f);
            // setPixelRange or setUnitRange
            packer.SetPixelRange(new DoubleRange(6.0f));
            packer.SetMiterLimit(1.0f);
            packer.SetOriginPixelAlignment(false, true);
            // Compute atlas layout - pack glyphs
            packer.Pack(ref glyphs);
            // Get final atlas dimensions
            packer.GetDimensions(out int width, out int height);

            //Gen function
            //GeneratorFunction<float> msdfGen = GlyphGenerators.Msdf;

            // The ImmediateAtlasGenerator class facilitates the generation of the atlas bitmap.
            ImmediateAtlasGenerator<
                    BitmapAtlasStorage // class that stores the atlas bitmap
                                             // For example, a custom atlas storage class that stores it in VRAM can be used.
                > generator = new(width, height, 3, GenType.MSDF);
            // GeneratorAttributes can be modified to change the generator's default settings.
            GeneratorAttributes attributes = new();
            generator.SetAttributes(attributes);
            //generator.SetThreadCount(4);
            // Generate atlas bitmap
            generator.Generate(glyphs);
            // The atlas bitmap can now be retrieved via atlasStorage as a BitmapConstRef.
            // The glyphs array (or fontGeometry) contains positioning data for typesetting text.

            //if (isSegmentsPoolBig)
            {
                if (OperatingSystem.IsBrowser())
                {
                    handle.Free();
                    ArrayPool<EdgeSegment>.Shared.Return(segmentArr);
                }
                else
                {
                    NativeMemory.Free(segmentsPool.Data);
                }
            }


            Bmp.SaveBmp(generator.Storage.Bitmap, "atlas.bmp");

        }

        //    private static void OnDemandAtlasGen(Typeface font)
        //    {
        //        // Atlas parameters
        //        const double pixelRange = 6.0;
        //        const double glyphScale = 64.0;
        //        const double miterLimit = 2.0;
        //        const double maxCornerAngle = 3.0;

        //        // Initialize
        //        List<GlyphGeometry> glyphs = new(font.GlyphCount);
        //        FontGeometry fontGeometry = new(glyphs);
        //        DynamicAtlas<ImmediateAtlasGenerator<float, BitmapAtlasStorage<byte>>> myDynamicAtlas = new();
        //        myDynamicAtlas.Generator = new(3, GlyphGenerators.Msdf);
        //        myDynamicAtlas.Packer = new();

        //        Console.WriteLine("Dynamic Atlas Generator Demo");
        //        while (true)  // Ctrl+C to exit
        //        {
        //            Console.WriteLine("Enter char(s) to be added to Atlas (Make sure no char is repeated from previous prompts):");
        //            ReadOnlySpan<char> chars = Console.ReadLine();

        //            Charset charset = new();
        //            int prevEndMark = glyphs.Count;
        //            for (int c = 0; c < chars.Length; ++c)
        //                charset.Add(chars[c]);
        //            fontGeometry.LoadCharset(font, 1.0, charset);

        //            for (int g = prevEndMark; g <  glyphs.Count; ++g)
        //            { 
        //                var glyph = glyphs[g];
        //                // Preprocess windings
        //                glyph.GetShape().OrientContours();
        //                // Apply MSDF edge coloring. See EdgeColorings for other coloring strategies.
        //                glyph.EdgeColoring(EdgeColorings.InkTrap, maxCornerAngle, 0);
        //                // Finalize glyph box scale based on the parameters
        //                glyph.WrapBox(new() { Scale = glyphScale, Range = new( pixelRange / glyphScale), MiterLimit = miterLimit });

        //                glyphs[g] = glyph;
        //            }

        //var newGlyphs = glyphs[prevEndMark..];
        //var changeFlags = myDynamicAtlas.Add(newGlyphs);
        //for (int i = 0; i < newGlyphs.Count; ++i)
        //{
        //	glyphs[prevEndMark + i] = newGlyphs[i];
        //}

        //            var bitmap = myDynamicAtlas.Generator.Storage.Bitmap;

        //            Png.SavePng(bitmap, "dynamic-atlas.png");
        //        }
        //    }



    }
}
