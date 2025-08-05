
using SharpMSDF.Core;

namespace SharpMSDF.Atlas
{
    public class ImmediateAtlasGenerator<T, TStorage>  : AtlasGenerator
        where T : struct
        where TStorage : AtlasStorage, new()
    {
        public readonly int N;
        public GeneratorFunction<T> GEN_FN { get; }
        public TStorage Storage => _Storage;
        public List<GlyphBox> Layout => _Layout;

        private TStorage _Storage = new();
        private readonly List<GlyphBox> _Layout = new();
        private List<T> _GlyphBuffer = new();
        private List<byte> _ErrorCorrectionBuffer = new();
        private GeneratorAttributes _Attributes;
        private int _ThreadCount = 1;

        public ImmediateAtlasGenerator(int n, GeneratorFunction<T> genFn)
        {
            N = n;
            GEN_FN = genFn;
            _Storage.Init(0, 0, n); // just to store 'n'
        }

        public ImmediateAtlasGenerator(int width, int height, int n, GeneratorFunction<T> genFn)
        {
            N = n;
            GEN_FN = genFn;
            _Storage.Init(width, height, n);
        }

        public override void Generate(List<GlyphGeometry> glyphs)
        {
            int maxBoxArea = 0;
            for (int i = 0; i < glyphs.Count; ++i)
            {
                GlyphBox box = glyphs[i];
                maxBoxArea = Math.Max(maxBoxArea, box.Rect.Width * box.Rect.Height);
                _Layout.Add(box);
            }

            int threadBufferSize = N * maxBoxArea;
            if (_ThreadCount * threadBufferSize > _GlyphBuffer.Count)
                _GlyphBuffer = new List<T>(new T[_ThreadCount * threadBufferSize]);
            if (_ThreadCount * maxBoxArea > _ErrorCorrectionBuffer.Count)
                _ErrorCorrectionBuffer = new List<byte>(new byte[_ThreadCount * maxBoxArea]);

            GeneratorAttributes[] threadAttributes = new GeneratorAttributes[_ThreadCount];
            for (int i = 0; i < _ThreadCount; ++i)
            {
                threadAttributes[i] = _Attributes;
                threadAttributes[i].Config.ErrorCorrection.Buffer = _ErrorCorrectionBuffer.GetRange(i * maxBoxArea, maxBoxArea).ToArray();
            }

            var workload = new Workload((i, threadNo) =>
            {
                var glyph = glyphs[i];
                if (!glyph.IsWhitespace())
                {
                    glyph.GetBoxRect(out int l, out int b, out int w, out int h);
                    var span = _GlyphBuffer.GetRange(threadNo * threadBufferSize, threadBufferSize).ToArray();
                    var glyphBitmap = new BitmapRef<T>(span, w, h, N);
                    GEN_FN(glyphBitmap, glyph, threadAttributes[threadNo]);
                    var constRef = new BitmapConstRef<T>(glyphBitmap);
                    _Storage.Put(l, b, constRef);
                }
                return true;
            }, glyphs.Count);

            workload.Finish(_ThreadCount);
        }

        public override void Rearrange(int width, int height, List<Remap> remapping, int count)
        {
            for (int i = 0; i < count; ++i)
            {
                var glyphBox = _Layout[remapping[i].Index] with { Rect = _Layout[remapping[i].Index].Rect with { X = remapping[i].Target.X, Y = remapping[i].Target.Y } };

                _Layout[remapping[i].Index] = glyphBox;
            }

            var oldStorage = _Storage;
            _Storage = new();
            _Storage.Init(oldStorage, width, height, remapping.ToArray()[..count]);
        }

        public override void Resize(int width, int height)
        {
            var oldStorage = _Storage;
            _Storage = new();
            _Storage.Init(oldStorage, width, height);
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