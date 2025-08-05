using SharpMSDF.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Typography.OpenFont;
using static SharpMSDF.Core.ErrorCorrectionConfig;

namespace SharpMSDF.Atlas
{
	public static class GlyphGenerators
	{
		public static void Scanline(BitmapRef<float> output, GlyphGeometry glyph, GeneratorAttributes attribs)
		{
			Rasterization.Rasterize(output, glyph.GetShape(), new(new(glyph.GetBoxScale()), glyph.GetBoxTranslate()), FillRule.FILL_NONZERO);
		}

		public static void Sdf(BitmapRef<float> output, GlyphGeometry glyph, GeneratorAttributes attribs)
		{
			MSDFGen.GenerateSDF(output, glyph.GetShape(), new(glyph.GetBoxProjection(), new(glyph.GetBoxRange())), attribs.Config);
			if (attribs.ScanlinePass)
				Rasterization.DistanceSignCorrection(output, glyph.GetShape(), glyph.GetBoxProjection(), FillRule.FILL_NONZERO);
		}

		public static void psdfGenerator(BitmapRef<float> output, GlyphGeometry glyph, GeneratorAttributes attribs)
		{
			MSDFGen.GeneratePSDF(output, glyph.GetShape(), new(glyph.GetBoxProjection(), new(glyph.GetBoxRange())), attribs.Config);
			if (attribs.ScanlinePass)
				Rasterization.DistanceSignCorrection(output, glyph.GetShape(), glyph.GetBoxProjection(), FillRule.FILL_NONZERO);
		}
		public static void Msdf(BitmapRef<float> output, GlyphGeometry glyph, GeneratorAttributes attribs)
		{
			MSDFGeneratorConfig config = attribs.Config;
			if (attribs.ScanlinePass)
				config.ErrorCorrection.Mode = ErrorCorrectionConfig.OpMode.DISABLED;
			MSDFGen.GenerateMSDF(output, glyph.GetShape(), new(glyph.GetBoxProjection(), new(glyph.GetBoxRange())), config);
			if (attribs.ScanlinePass)
			{
				Rasterization.DistanceSignCorrection(output, glyph.GetShape(), glyph.GetBoxProjection(), FillRule.FILL_NONZERO);
				if (attribs.Config.ErrorCorrection.Mode != ErrorCorrectionConfig.OpMode.DISABLED)
				{
					config.ErrorCorrection.Mode = attribs.Config.ErrorCorrection.Mode;
					config.ErrorCorrection.DistanceCheckMode = ErrorCorrectionConfig.ConfigDistanceCheckMode.DO_NOT_CHECK_DISTANCE;
					MSDFErrorCorrection.ErrorCorrection(output, glyph.GetShape(), new(glyph.GetBoxProjection(), new(glyph.GetBoxRange())), config);
				}
			}
		}

		public static void Mtsdf(BitmapRef<float> output, GlyphGeometry glyph, GeneratorAttributes attribs)
		{
			MSDFGeneratorConfig config = attribs.Config;
			if (attribs.ScanlinePass)
				config.ErrorCorrection.Mode = ErrorCorrectionConfig.OpMode.DISABLED;
			MSDFGen.GenerateMTSDF(output, glyph.GetShape(), new(glyph.GetBoxProjection(), new(glyph.GetBoxRange())), config);
			if (attribs.ScanlinePass)
			{
				Rasterization.DistanceSignCorrection(output, glyph.GetShape(), glyph.GetBoxProjection(), FillRule.FILL_NONZERO);
				if (attribs.Config.ErrorCorrection.Mode != ErrorCorrectionConfig.OpMode.DISABLED)
				{
					config.ErrorCorrection.Mode = attribs.Config.ErrorCorrection.Mode;
					config.ErrorCorrection.DistanceCheckMode = ErrorCorrectionConfig.ConfigDistanceCheckMode.DO_NOT_CHECK_DISTANCE;
					MSDFErrorCorrection.ErrorCorrection(output, glyph.GetShape(), new(glyph.GetBoxProjection(), new(glyph.GetBoxRange())), config);
				}
			}
		}
	}
}
