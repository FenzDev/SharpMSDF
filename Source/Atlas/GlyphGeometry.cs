using SharpMSDF.Core;
using SharpMSDF.IO;
using SharpMSDF.Utilities;
using System.Numerics;

#if MSDFGEN_USE_SKIA
using SharpMSDF.SkiaSharp;
#endif
using Typography.OpenFont;
using SimpleTrueShapeDistanceFinder = SharpMSDF.Core.ShapeDistanceFinder<SharpMSDF.Core.SimpleContourCombiner<SharpMSDF.Core.TrueDistanceSelector, float>, SharpMSDF.Core.TrueDistanceSelector, float>;

namespace SharpMSDF.Atlas
{
	public struct GlyphGeometry
	{
		public struct GlyphAttributes
		{
			public float Scale;
			public DoubleRange Range;
			public Padding InnerPadding, OuterPadding;
			public float MiterLimit;
			public bool PxAlignOriginX, PxAlignOriginY;
		}

		private uint _codepoint;
		private float _geometryScale;
		private Shape.Bounds _bounds;
		private float _advance;
		private ushort _index;
		public Shape Shape;

		private struct BoxData
		{
            public AtlasRectangle Rect;
			public DoubleRange Range;
			public float Scale;
			public Vector2 Translate;
			public Padding OuterPadding;
		}

		private BoxData _box;

		public GlyphGeometry() { }

		public unsafe bool Load(Typeface font, /*int glyphIndex, */ref PtrPool<Contour> contoursPool, ref PtrPool<EdgeSegment> segmentsPool, float geometryScale, uint codepoint, bool preprocessGeometry = true)
		{
			if (font == null)
				return false;

			ushort glyphIndex = font.GetGlyphIndex((int)codepoint);
			Shape = FontImporter.LoadGlyph(font, glyphIndex, FontCoordinateScaling.None, ref contoursPool, ref segmentsPool, ref _advance);

			if (Shape.Validate())
			{
				_index = glyphIndex;
                _geometryScale = geometryScale;
				_codepoint = codepoint;
				_advance *= geometryScale;

#if USE_SKIA
		if (preprocessGeometry)
		{
			ResolveShapeGeometry.Resolve(_shape);
		}
#endif

                Shape.Normalize();
				_bounds = Shape.GetBounds();

#if USE_SKIA
		if (!preprocessGeometry)
#endif
				{
					var outerPoint = new Vector2(
						_bounds.l - (_bounds.r - _bounds.l) - 1,
						_bounds.b - (_bounds.t - _bounds.b) - 1
					);

					int* windings = stackalloc int[Shape.Contours.Count];
					TrueDistanceSelector* selectors = stackalloc TrueDistanceSelector[Shape.Contours.Count];

					if (SimpleTrueShapeDistanceFinder.OneShotDistance(Shape, windings, selectors, outerPoint) > 0)
					{
						for (int c = 0; c < Shape.Contours.Count; c++)
                            Shape.Contours[c].Reverse();
					}
				}

				return true;
			}
			return false;
		}

		public void EdgeColoring(EdgeColoringDelegate coloringFunc, float angleThreshold, ulong seed)
		{
			coloringFunc?.Invoke(ref Shape, angleThreshold, seed);
		}

		public void WrapBox(GlyphAttributes glyphAttributes)
		{
			float scale = glyphAttributes.Scale * _geometryScale;
			DoubleRange range = glyphAttributes.Range / _geometryScale;
			Padding fullPadding = (glyphAttributes.InnerPadding + glyphAttributes.OuterPadding) / _geometryScale;

			_box.Range = range;
			_box.Scale = scale;

			if (_bounds.l < _bounds.r && _bounds.b < _bounds.t)
			{
				float l = _bounds.l + range.Lower;
				float b = _bounds.b + range.Lower;
				float r = _bounds.r - range.Lower;
				float t = _bounds.t - range.Lower;

				if (glyphAttributes.MiterLimit > 0)
					Shape.BoundMiters(ref l, ref b, ref r, ref t, -range.Lower, glyphAttributes.MiterLimit, 1);

				l -= fullPadding.L; b -= fullPadding.B;
				r += fullPadding.R; t += fullPadding.T;

				if (glyphAttributes.PxAlignOriginX)
				{
					int sl = (int)Math.Floor(scale * l - 0.5f);
					int sr = (int)Math.Ceiling(scale * r + 0.5f);
					_box.Rect.Width = sr - sl;
					_box.Translate.X = -sl / scale;
				}
				else
				{
					float w = scale * (r - l);
					_box.Rect.Width = (int)Math.Ceiling(w) + 1;
					_box.Translate.X = -l + 0.5f * (_box.Rect.Width - w) / scale;
				}

				if (glyphAttributes.PxAlignOriginY)
				{
					int sb = (int)Math.Floor(scale * b - 0.5f);
					int st = (int)Math.Ceiling(scale * t + 0.5f);
					_box.Rect.Height = st - sb;
					_box.Translate.Y = -sb / scale;
				}
				else
				{
					float h = scale * (t - b);
					_box.Rect.Height = (int)Math.Ceiling(h) + 1;
					_box.Translate.Y = -b + 0.5f * (_box.Rect.Height - h) / scale;
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

		public void FrameBox(in GlyphAttributes glyphAttributes, int width, int height, float? fixedX, float? fixedY)
		{
			float scale = glyphAttributes.Scale * _geometryScale;
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
				float l = _bounds.l + range.Lower;
				float b = _bounds.b + range.Lower;
				float r = _bounds.r - range.Lower;
				float t = _bounds.t - range.Lower;

				if (glyphAttributes.MiterLimit > 0)
					Shape.BoundMiters(ref l, ref b, ref r, ref t, -range.Lower, glyphAttributes.MiterLimit, 1);

				l -= fullPadding.L; b -= fullPadding.B;
				r += fullPadding.R; t += fullPadding.T;

				if (fixedX.HasValue)
					_box.Translate.X = fixedX.Value / _geometryScale;
				else if (glyphAttributes.PxAlignOriginX)
				{
					int sl = (int)Math.Floor(scale * l - 0.5f);
					int sr = (int)Math.Ceiling(scale * r + 0.5f);
					_box.Translate.X = (-sl + (_box.Rect.Width - (sr - sl)) / 2.0f) / scale;
				}
				else
				{
					float w = scale * (r - l);
					_box.Translate.X = -l + 0.5f * (_box.Rect.Width - w) / scale;
				}

				if (fixedY.HasValue)
					_box.Translate.Y = fixedY.Value / _geometryScale;
				else if (glyphAttributes.PxAlignOriginY)
				{
					int sb = (int)Math.Floor(scale * b - 0.5f);
					int st = (int)Math.Ceiling(scale * t + 0.5f);
					_box.Translate.Y = (-sb + (_box.Rect.Height - (st - sb)) / 2.0f) / scale;
				}
				else
				{
					float h = scale * (t - b);
					_box.Translate.Y = -b + 0.5f * (_box.Rect.Height - h) / scale;
				}
			}

			_box.OuterPadding = glyphAttributes.Scale * glyphAttributes.OuterPadding;
		}

		public void FrameBox(float scale, float range, float miterLimit, int width, int height, float? fixedX, float? fixedY, bool pxAlignOrigin)
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

		public void FrameBox(float scale, float range, float miterLimit, int width, int height, float? fixedX, float? fixedY, bool pxAlignOriginX, bool pxAlignOriginY)
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

		public float GetGeometryScale() => _geometryScale;
		//public ref _shape GetShape() => _shape;
		//public ref _shape.Bounds GetShapeBounds() => ref _bounds;
		public float GetAdvance() => _advance;
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
		public float GetBoxScale() => _box.Scale;
		public Vector2 GetBoxTranslate() => _box.Translate;

		public void GetQuadPlaneBounds(out float l, out float b, out float r, out float t)
		{
			if (_box.Rect.Width > 0 && _box.Rect.Height > 0)
			{
				float invScale = 1 / _box.Scale;
				l = _geometryScale * (-_box.Translate.X + (_box.OuterPadding.L + 0.5f) * invScale);
				b = _geometryScale * (-_box.Translate.Y + (_box.OuterPadding.B + 0.5f) * invScale);
				r = _geometryScale * (-_box.Translate.X + (-_box.OuterPadding.R + _box.Rect.Width - 0.5f) * invScale);
				t = _geometryScale * (-_box.Translate.Y + (-_box.OuterPadding.T + _box.Rect.Height - 0.5f) * invScale);
			}
			else
				l = b = r = t = 0;
		}

		public void GetQuadAtlasBounds(out float l, out float b, out float r, out float t)
		{
			if (_box.Rect.Width > 0 && _box.Rect.Height > 0)
			{
				l = _box.Rect.X + _box.OuterPadding.L + 0.5f;
				b = _box.Rect.Y + _box.OuterPadding.B + 0.5f;
				r = _box.Rect.X - _box.OuterPadding.R + _box.Rect.Width - 0.5f;
				t = _box.Rect.Y - _box.OuterPadding.T + _box.Rect.Height - 0.5f;
			}
			else
				l = b = r = t = 0;
		}

		public bool IsWhitespace() => Shape.Contours.Count == 0;

		public static implicit operator GlyphBox(GlyphGeometry geometry)
		{
			geometry.GetQuadPlaneBounds(out float l, out float b, out float r, out float t);
			return new GlyphBox
			{
				//Index = geometry._index,
				Advance = geometry._advance,
				Bounds = new Bounds { L = l, B = b, R = r, T = t },
				Rect = geometry._box.Rect
			};
		}
	}

	public delegate void EdgeColoringDelegate(ref Shape shape, float angleThereshold, ulong seed);
}
