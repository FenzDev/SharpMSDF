
using SharpMSDF.Core;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using System.Threading;

namespace SharpMSDF.Atlas
{
	public enum GenType
	{
		Scanline = 1,
		SDF = 2,
		PSDF = 3,
		MSDF = 4,
		MTSDF = 5,
	}

	public struct ImmediateAtlasGenerator<TStorage> : IAtlasGenerator
		where TStorage : IAtlasStorage<TStorage>, new()
	{
		public readonly int N;
		public GenType GenType { get; }
		public TStorage Storage = new();
		public List<GlyphBox> Layout { get; } = [];

		private int _Width, _Height;
		private float[]? _GlyphBuffer;
		private byte[]? _ErrorCorrectionBuffer;
		private GeneratorAttributes _Attributes = new GeneratorAttributes { Config = MSDFGeneratorConfig.Default };
		//private int _ThreadCount = 1;

		public ImmediateAtlasGenerator(int n, GenType genType)
		{
			N = n;
            GenType = genType;
		}

		public ImmediateAtlasGenerator(int width, int height, int n, GenType genType)
		{
			N = n;
			_Width = width;
			_Height = height;
            GenType = genType;
		}

		public ImmediateAtlasGenerator(int n, GenType genType, TStorage storage)
		{
			N = n;
            GenType = genType;
			Storage = storage;
		}
		public void Generate(List<GlyphGeometry> glyphs)
		{
			ReadOnlySpan<GlyphGeometry> glyphSpan = CollectionsMarshal.AsSpan(glyphs);

			int maxBoxArea = 0;
			for (int i = 0; i < glyphs.Count; ++i)
			{
				GlyphBox box = glyphs[i];
				maxBoxArea = Math.Max(maxBoxArea, box.Rect.Width * box.Rect.Height);
				Layout.Add(box);
			}

			int bufferSize = N * maxBoxArea;

            // Allocate/reallocate only when bigger buffer needed
            //if (_GlyphBuffer == null || _GlyphBuffer.Length < bufferSize)
                _GlyphBuffer = ArrayPool<float>.Shared.Rent(bufferSize);


            //if (_ErrorCorrectionBuffer == null || _ErrorCorrectionBuffer.Length < maxBoxArea)
                _ErrorCorrectionBuffer = ArrayPool<byte>.Shared.Rent(maxBoxArea);

            GeneratorAttributes threadAttributes = new GeneratorAttributes();

			// Get spans for the buffers to avoid repeated List access
			Span<float> glyphBufferSpan = _GlyphBuffer;
			Span<byte> errorBufferSpan = _ErrorCorrectionBuffer;

			//for (int i = 0; i < _ThreadCount; ++i)
			//{
			//	threadAttributes[i] = _Attributes;

			//	// Create a span for this thread's error correction buffer
			//	Span<byte> threadErrorSpan = errorBufferSpan.Slice(i * maxBoxArea, maxBoxArea);

			//	// Convert span to array only when necessary for the API
			//	threadAttributes[i].Config.ErrorCorrection.Buffer = threadErrorSpan.ToArray();
			//}

			Storage.Init(_Width, _Height, N);

			GenerateEach(glyphSpan);

			ArrayPool<float>.Shared.Return(_GlyphBuffer);
			ArrayPool<byte>.Shared.Return(_ErrorCorrectionBuffer);
			_GlyphBuffer = null;
			_ErrorCorrectionBuffer = null;

			Storage.Destroy();

            //_ = workload.Finish(_ThreadCount);
        }

		public void GenerateEach(ReadOnlySpan<GlyphGeometry> glyphs)
		{
			for (int i = 0; i < glyphs.Length; i++)
			{
				GlyphGeometry glyph = glyphs[i];
				if (!glyph.IsWhitespace())
				{
					glyph.GetBoxRect(out int l, out int b, out int w, out int h);
					BitmapRef<float> glyphBitmap = new(_GlyphBuffer, w, h, N);
                    GenerateGlyph(glyphBitmap, glyph, _Attributes);
					BitmapConstRef<float> constRef = new(glyphBitmap);
                    Storage.Put(l, b, constRef); 
				}
			}
        }

		public void GenerateGlyph(BitmapRef<float> bitmap, GlyphGeometry glyph, GeneratorAttributes att)
		{
            switch (GenType)
            {
                case GenType.Scanline: GlyphGenerators.Scanline(bitmap, glyph, att);
                    break;
                case GenType.SDF: GlyphGenerators.Sdf(bitmap, glyph, att);
                    break;
                case GenType.PSDF: GlyphGenerators.Psdf(bitmap, glyph, att);
                    break;
                case GenType.MSDF: GlyphGenerators.Msdf(bitmap, glyph, att);
                    break;
                case GenType.MTSDF: GlyphGenerators.Mtsdf(bitmap, glyph, att);
                    break;
            }
        }

        public void Rearrange(int width, int height, List<Remap> remapping, int count)
		{
            for (int i = 0; i < count; ++i)
			{
                var glyphBox = Layout[remapping[i].Index] with { Rect = Layout[remapping[i].Index].Rect with { X = remapping[i].Target.X, Y = remapping[i].Target.Y } };

				Layout[remapping[i].Index] = glyphBox;
			}

            var oldStorage = Storage;
            Storage = new();
            Storage.Init(oldStorage, width, height, CollectionsMarshal.AsSpan(remapping)[..count]);
		}

		public void Resize(int width, int height)
		{
			TStorage oldStorage = Storage;
			Storage = new();
			Storage.Init(oldStorage, width, height);
		}

		public void SetAttributes(GeneratorAttributes attributes)
		{
			_Attributes = attributes;
		}

        //	public void SetThreadCount(int threadCount)
        //	{
        //		_ThreadCount = threadCount;
        //	}
    }

}