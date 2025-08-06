
using SharpMSDF.Core;
using System.Runtime.InteropServices;

namespace SharpMSDF.Atlas
{
	public class ImmediateAtlasGenerator<T, TStorage> : AtlasGenerator
		where T : struct
		where TStorage : AtlasStorage, new()
	{
		public readonly int N;
		public GeneratorFunction<T> GEN_FN { get; }
		public TStorage Storage { get; private set; } = new();
		public List<GlyphBox> Layout { get; } = [];

		private List<T> _GlyphBuffer = [];
		private List<byte> _ErrorCorrectionBuffer = [];
		private GeneratorAttributes _Attributes = new GeneratorAttributes { Config = MSDFGeneratorConfig.Default };
		private int _ThreadCount = 1;

		public ImmediateAtlasGenerator(int n, GeneratorFunction<T> genFn)
		{
			N = n;
			GEN_FN = genFn;
			Storage.Init(0, 0, n); // just to store 'n'
		}

		public ImmediateAtlasGenerator(int width, int height, int n, GeneratorFunction<T> genFn)
		{
			N = n;
			GEN_FN = genFn;
			Storage.Init(width, height, n);
		}

		public ImmediateAtlasGenerator(int n, GeneratorFunction<T> genFn, TStorage storage)
		{
			N = n;
			GEN_FN = genFn;
			Storage = storage;
		}

		public override void Generate(List<GlyphGeometry> glyphs)
		{
			ReadOnlySpan<GlyphGeometry> glyphSpan = CollectionsMarshal.AsSpan(glyphs);

			int maxBoxArea = 0;
			for (int i = 0; i < glyphs.Count; ++i)
			{
				GlyphBox box = glyphs[i];
				maxBoxArea = Math.Max(maxBoxArea, box.Rect.Width * box.Rect.Height);
				Layout.Add(box);
			}

			int threadBufferSize = N * maxBoxArea;

			// Ensure buffers are large enough
			int requiredGlyphBufferSize = _ThreadCount * threadBufferSize;
			if (requiredGlyphBufferSize > _GlyphBuffer.Count)
			{
				_GlyphBuffer.Clear();
				_GlyphBuffer.AddRange(new T[requiredGlyphBufferSize]);
			}

			int requiredErrorBufferSize = _ThreadCount * maxBoxArea;
			if (requiredErrorBufferSize > _ErrorCorrectionBuffer.Count)
			{
				_ErrorCorrectionBuffer.Clear();
				_ErrorCorrectionBuffer.AddRange(new byte[requiredErrorBufferSize]);
			}

			GeneratorAttributes[] threadAttributes = new GeneratorAttributes[_ThreadCount];

			// Get spans for the buffers to avoid repeated List access
			Span<T> glyphBufferSpan = CollectionsMarshal.AsSpan(_GlyphBuffer);
			Span<byte> errorBufferSpan = CollectionsMarshal.AsSpan(_ErrorCorrectionBuffer);

			for (int i = 0; i < _ThreadCount; ++i)
			{
				threadAttributes[i] = _Attributes;

				// Create a span for this thread's error correction buffer
				Span<byte> threadErrorSpan = errorBufferSpan.Slice(i * maxBoxArea, maxBoxArea);

				// Convert span to array only when necessary for the API
				threadAttributes[i].Config.ErrorCorrection.Buffer = threadErrorSpan.ToArray();
			}

			Workload workload = new((i, threadNo) =>
			{
				GlyphGeometry glyph = glyphs[i];
				if (!glyph.IsWhitespace())
				{
					glyph.GetBoxRect(out int l, out int b, out int w, out int h);
					T[] span = _GlyphBuffer.GetRange(threadNo * threadBufferSize, threadBufferSize).ToArray();
					BitmapRef<T> glyphBitmap = new(span, w, h, N);
					GEN_FN(glyphBitmap, glyph, threadAttributes[threadNo]);
					BitmapConstRef<T> constRef = new(glyphBitmap);
					Storage.Put(l, b, constRef);
				}
				return true;
			}, glyphs.Count);

			_ = workload.Finish(_ThreadCount);
		}

        public override void Rearrange(int width, int height, List<Remap> remapping, int count)
		{
            for (int i = 0; i < count; ++i)
			{
                var glyphBox = Layout[remapping[i].Index] with { Rect = Layout[remapping[i].Index].Rect with { X = remapping[i].Target.X, Y = remapping[i].Target.Y } };

				Layout[remapping[i].Index] = glyphBox;
			}

            var oldStorage = Storage;
            Storage = new();
            Storage.Init(oldStorage, width, height, remapping.ToArray()[..count]);
		}

		public override void Resize(int width, int height)
		{
			TStorage oldStorage = Storage;
			Storage = new();
			Storage.Init(oldStorage, width, height);
		}

		public void SetAttributes(GeneratorAttributes attributes)
		{
			_Attributes = attributes;
		}

		public void SetThreadCount(int threadCount)
		{
			_ThreadCount = threadCount;
		}
	}

}