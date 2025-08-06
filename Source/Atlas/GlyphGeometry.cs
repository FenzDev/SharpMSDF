using SharpMSDF.Core;
using SharpMSDF.IO;
#if MSDFGEN_USE_SKIA
using SharpMSDF.SkiaSharp;
#endif
using Typography.OpenFont;
using SimpleTrueShapeDistanceFinder = SharpMSDF.Core.ShapeDistanceFinder<SharpMSDF.Core.SimpleContourCombiner<SharpMSDF.Core.TrueDistanceSelector, double>, SharpMSDF.Core.TrueDistanceSelector, double>;

namespace SharpMSDF.Atlas
{
	public struct GlyphGeometry
	{
		public struct GlyphAttributes
		{
			public double Scale;
			public DoubleRange Range;
			public Padding InnerPadding, OuterPadding;
			public double MiterLimit;
			public bool PxAlignOriginX, PxAlignOriginY;
		}

		private uint _codepoint;
		private double _geometryScale;
		private Shape.Bounds _bounds;
		private double _advance;
		private ushort _index;
		private Shape _shape;

		private struct BoxData
		{
            public AtlasRectangle Rect;
			public DoubleRange Range;
			public double Scale;
			public Vector2 Translate;
			public Padding OuterPadding;
		}

		private BoxData _box;

		public GlyphGeometry() { }

		public bool Load(Typeface font, double geometryScale, uint codepoint, bool preprocessGeometry = true)
		{
			if (font == null)
				return false;

			_shape = FontImporter.LoadGlyph(font, codepoint, FontCoordinateScaling.None, out _, out _, ref _advance);

			if (_shape.Validate())
			{
				_index = font.GetGlyphIndex((int)codepoint);
				_geometryScale = geometryScale;
				_codepoint = codepoint;
				_advance *= geometryScale;

#if MSDFGEN_USE_SKIA
		if (preprocessGeometry)
		{
			ResolveShapeGeometry.Resolve(_shape);
		}
#endif

				_shape.Normalize();
				_bounds = _shape.GetBounds();

#if MSDFGEN_USE_SKIA
		if (!preprocessGeometry)
#endif
				{
					var outerPoint = new Vector2(
						_bounds.l - (_bounds.r - _bounds.l) - 1,
						_bounds.b - (_bounds.t - _bounds.b) - 1
					);

					if (SimpleTrueShapeDistanceFinder.OneShotDistance(_shape, outerPoint) > 0)
					{
						foreach (var contour in _shape.Contours)
							contour.Reverse();
					}

					_shape.OrientContours();
				}

				return true;
			}
			return false;
		}

		public void EdgeColoring(Action<Shape, double, ulong> coloringFunc, double angleThreshold, ulong seed)
		{
			coloringFunc?.Invoke(_shape, angleThreshold, seed);
		}

		public void WrapBox(GlyphAttributes glyphAttributes)
		{
			double scale = glyphAttributes.Scale * _geometryScale;
			DoubleRange range = glyphAttributes.Range / _geometryScale;
			Padding fullPadding = (glyphAttributes.InnerPadding + glyphAttributes.OuterPadding) / _geometryScale;

			_box.Range = range;
			_box.Scale = scale;

			if (_bounds.l < _bounds.r && _bounds.b < _bounds.t)
			{
				double l = _bounds.l + range.Lower;
				double b = _bounds.b + range.Lower;
				double r = _bounds.r - range.Lower;
				double t = _bounds.t - range.Lower;

				if (glyphAttributes.MiterLimit > 0)
					_shape.BoundMiters(ref l, ref b, ref r, ref t, -range.Lower, glyphAttributes.MiterLimit, 1);

				l -= fullPadding.L; b -= fullPadding.B;
				r += fullPadding.R; t += fullPadding.T;

				if (glyphAttributes.PxAlignOriginX)
				{
					int sl = (int)Math.Floor(scale * l - 0.5);
					int sr = (int)Math.Ceiling(scale * r + 0.5);
					_box.Rect.Width = sr - sl;
					_box.Translate.X = -sl / scale;
				}
				else
				{
					double w = scale * (r - l);
					_box.Rect.Width = (int)Math.Ceiling(w) + 1;
					_box.Translate.X = -l + 0.5 * (_box.Rect.Width - w) / scale;
				}

				if (glyphAttributes.PxAlignOriginY)
				{
					int sb = (int)Math.Floor(scale * b - 0.5);
					int st = (int)Math.Ceiling(scale * t + 0.5);
					_box.Rect.Height = st - sb;
					_box.Translate.Y = -sb / scale;
				}
				else
				{
					double h = scale * (t - b);
					_box.Rect.Height = (int)Math.Ceiling(h) + 1;
					_box.Translate.Y = -b + 0.5 * (_box.Rect.Height - h) / scale;
				}

				_box.OuterPadding = glyphAttributes.Scale * glyphAttributes.OuterPadding;
			}
			else
			{
				_box.Rect.Width = 0;
				_box.Rect.Height = 0;
				_box.Translate = new Vector2();
			}
		}

		public void FrameBox(in GlyphAttributes glyphAttributes, int width, int height, double? fixedX, double? fixedY)
		{
			double scale = glyphAttributes.Scale * _geometryScale;
			DoubleRange range = glyphAttributes.Range / _geometryScale;
			Padding fullPadding = (glyphAttributes.InnerPadding + glyphAttributes.OuterPadding) / _geometryScale;

			_box.Range = range;
			_box.Scale = scale;
			_box.Rect.Width = width;
			_box.Rect.Height = height;

			if (fixedX.HasValue && fixedY.HasValue)
			{
				_box.Translate.X = fixedX.Value / _geometryScale;
				_box.Translate.Y = fixedY.Value / _geometryScale;
			}
			else
			{
				double l = _bounds.l + range.Lower;
				double b = _bounds.b + range.Lower;
				double r = _bounds.r - range.Lower;
				double t = _bounds.t - range.Lower;

				if (glyphAttributes.MiterLimit > 0)
					_shape.BoundMiters(ref l, ref b, ref r, ref t, -range.Lower, glyphAttributes.MiterLimit, 1);

				l -= fullPadding.L; b -= fullPadding.B;
				r += fullPadding.R; t += fullPadding.T;

				if (fixedX.HasValue)
					_box.Translate.X = fixedX.Value / _geometryScale;
				else if (glyphAttributes.PxAlignOriginX)
				{
					int sl = (int)Math.Floor(scale * l - 0.5);
					int sr = (int)Math.Ceiling(scale * r + 0.5);
					_box.Translate.X = (-sl + (_box.Rect.Width - (sr - sl)) / 2.0) / scale;
				}
				else
				{
					double w = scale * (r - l);
					_box.Translate.X = -l + 0.5 * (_box.Rect.Width - w) / scale;
				}

				if (fixedY.HasValue)
					_box.Translate.Y = fixedY.Value / _geometryScale;
				else if (glyphAttributes.PxAlignOriginY)
				{
					int sb = (int)Math.Floor(scale * b - 0.5);
					int st = (int)Math.Ceiling(scale * t + 0.5);
					_box.Translate.Y = (-sb + (_box.Rect.Height - (st - sb)) / 2.0) / scale;
				}
				else
				{
					double h = scale * (t - b);
					_box.Translate.Y = -b + 0.5 * (_box.Rect.Height - h) / scale;
				}
			}

			_box.OuterPadding = glyphAttributes.Scale * glyphAttributes.OuterPadding;
		}

		public void FrameBox(double scale, double range, double miterLimit, int width, int height, double? fixedX, double? fixedY, bool pxAlignOrigin)
		{
			FrameBox(new GlyphAttributes
			{
				Scale = scale,
				Range = new DoubleRange(range),
				MiterLimit = miterLimit,
				PxAlignOriginX = pxAlignOrigin,
				PxAlignOriginY = pxAlignOrigin
			}, width, height, fixedX, fixedY);
		}

		public void FrameBox(double scale, double range, double miterLimit, int width, int height, double? fixedX, double? fixedY, bool pxAlignOriginX, bool pxAlignOriginY)
		{
			FrameBox(new GlyphAttributes
			{
				Scale = scale,
				Range = new DoubleRange(range),
				MiterLimit = miterLimit,
				PxAlignOriginX = pxAlignOriginX,
				PxAlignOriginY = pxAlignOriginY
			}, width, height, fixedX, fixedY);
		}

		public GlyphGeometry PlaceBox(int x, int y)
		{
			_box.Rect.X = x;
			_box.Rect.Y = y;
			return this;
		}

        public GlyphGeometry SetBoxRect(AtlasRectangle rect)
		{
			_box.Rect = rect;
			return this;
		}

		public ushort GetIndex() => _index;
		public ushort GetGlyphIndex() => _index;
		public uint GetCodepoint() => _codepoint;

		//public int GetIdentifier(GlyphIdentifierType type)
		//{
		//    return type switch
		//    {
		//        GlyphIdentifierType.GlyphIndex => _index,
		//        GlyphIdentifierType.UnicodeCodepoint => (int)_codepoint,
		//        _ => 0
		//    };
		//}

		public double GetGeometryScale() => _geometryScale;
		//public ref _shape GetShape() => _shape;
		//public ref _shape.Bounds GetShapeBounds() => ref _bounds;
		public double GetAdvance() => _advance;
        public AtlasRectangle GetBoxRect() => _box.Rect;
		public void GetBoxRect(out int x, out int y, out int w, out int h)
		{
			x = _box.Rect.X;
			y = _box.Rect.Y;
			w = _box.Rect.Width;
			h = _box.Rect.Height;
		}

		public void GetBoxSize(out int w, out int h)
		{
			w = _box.Rect.Width;
			h = _box.Rect.Height;
		}

		public DoubleRange GetBoxRange() => _box.Range;
		public Projection GetBoxProjection() => new Projection(new Vector2(_box.Scale), _box.Translate);
		public double GetBoxScale() => _box.Scale;
		public Vector2 GetBoxTranslate() => _box.Translate;

		public void GetQuadPlaneBounds(out double l, out double b, out double r, out double t)
		{
			if (_box.Rect.Width > 0 && _box.Rect.Height > 0)
			{
				double invScale = 1 / _box.Scale;
				l = _geometryScale * (-_box.Translate.X + (_box.OuterPadding.L + 0.5) * invScale);
				b = _geometryScale * (-_box.Translate.Y + (_box.OuterPadding.B + 0.5) * invScale);
				r = _geometryScale * (-_box.Translate.X + (-_box.OuterPadding.R + _box.Rect.Width - 0.5) * invScale);
				t = _geometryScale * (-_box.Translate.Y + (-_box.OuterPadding.T + _box.Rect.Height - 0.5) * invScale);
			}
			else
				l = b = r = t = 0;
		}

		public void GetQuadAtlasBounds(out double l, out double b, out double r, out double t)
		{
			if (_box.Rect.Width > 0 && _box.Rect.Height > 0)
			{
				l = _box.Rect.X + _box.OuterPadding.L + 0.5;
				b = _box.Rect.Y + _box.OuterPadding.B + 0.5;
				r = _box.Rect.X - _box.OuterPadding.R + _box.Rect.Width - 0.5;
				t = _box.Rect.Y - _box.OuterPadding.T + _box.Rect.Height - 0.5;
			}
			else
				l = b = r = t = 0;
		}

		public bool IsWhitespace() => _shape.Contours.Count == 0;

		public Shape GetShape() => _shape;

		public static implicit operator GlyphBox(GlyphGeometry geometry)
		{
			geometry.GetQuadPlaneBounds(out double l, out double b, out double r, out double t);
			return new GlyphBox
			{
				//Index = geometry._index,
				Advance = geometry._advance,
				Bounds = new Bounds { L = l, B = b, R = r, T = t },
				Rect = geometry._box.Rect
			};
		}
	}
}
