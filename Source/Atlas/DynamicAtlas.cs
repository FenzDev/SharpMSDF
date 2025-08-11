namespace SharpMSDF.Atlas
{

    [Flags]
    public enum ChangeFlag : int
    {
        NoChange = 0x00,
        Resized = 0x01,
        Rearranged = 0x02,
    }

    /// <summary>
    /// This class can be used to produce a dynamic atlas to which more glyphs are added over time.
    /// It takes care of laying out and enlarging the atlas as necessary and delegates the actual work
    /// to the specified AtlasGenerator, which may e.g. do the work asynchronously.
    /// </summary>
    public struct DynamicAtlas<TAtlasGen>
        where TAtlasGen : IAtlasGenerator
    {
        private int _Side;
        private int _Spacing;
        private int _GlyphCount;
        private int _TotalArea;
        private List<AtlasRectangle> _Rectangles = [];
        private List<Remap> _RemapBuffer = [];
        public RectanglePacker Packer;
        public TAtlasGen Generator;


        public DynamicAtlas() { }
        public DynamicAtlas(TAtlasGen generator, RectanglePacker packer, int minSide, int maxSide)
        {
            _Side = CeilPOT(minSide);
            _Spacing = 0;
            _GlyphCount = 0;
            _TotalArea = 0;
            Packer = packer;
            Generator = generator;
        }

        private static int CeilPOT(int x)
        {
            if (x > 0)
            {
                int y = 1;
                while (y < x)
                    y <<= 1;
                return y;
            }
            return 0;
        }

        /// <summary>
        /// Adds a batch of glyphs. Adding more than one glyph at a time may improve packing efficiency
        /// </summary>
        public ChangeFlag Add(List<GlyphGeometry> glyphs, bool allowRearrange = false)
        {
            // TODO : Fix
            ChangeFlag changeFlags = 0;
            int start = _Rectangles.Count;

            for (int i = 0; i < glyphs.Count; ++i)
            {
                if (!glyphs[i].IsWhitespace())
                {
                    glyphs[i].GetBoxSize(out int w, out int h);
                    _Rectangles.Add(new AtlasRectangle(0, 0, w + _Spacing, h + _Spacing));

                    _RemapBuffer.Add(new Remap
                    {
                        Index = _GlyphCount + i,
                        Width = w,
                        Height = h
                    });

                    _TotalArea += (w + _Spacing) * (h + _Spacing);
                }
            }

            if (_Rectangles.Count > start)
            {
                int packerStart = start;
                int remaining;
                while ((remaining = Packer.Pack(_Rectangles, packerStart)) > 0)
                {
                    _Side = (_Side | (_Side == 0 ? 1 : 0)) << 1;
                    while (_Side * _Side < _TotalArea)
                        _Side <<= 1;

                    if (allowRearrange)
                    {
                        Packer = new RectanglePacker(_Side + _Spacing, _Side + _Spacing);
                        packerStart = 0;
                    }
                    else
                    {
                        Packer.Expand(_Side + _Spacing, _Side + _Spacing);
                        packerStart = _Rectangles.Count - remaining; // (- remaining) was removed for bug reason
                    }

                    changeFlags |= ChangeFlag.Resized;
                }

                if (packerStart < start)
                {
                    for (int i = packerStart; i < start; ++i)
                    {
                        var remap = _RemapBuffer[i];
                        remap.Source = remap.Target;
                        remap.Target = new (_Rectangles[i].X, _Rectangles[i].Y);

                        _RemapBuffer[i] = remap;
                    }

                    Generator.Rearrange(_Side, _Side, _RemapBuffer, start);

                    changeFlags |= ChangeFlag.Rearranged;
                }
                else if ((changeFlags & ChangeFlag.Resized) != 0)
                {
                    Generator.Resize(_Side, _Side);
                }

                for (int i = start; i < _Rectangles.Count; ++i)
                {
                    _RemapBuffer[i] = _RemapBuffer[i] with { Target = new(_Rectangles[i].X, _Rectangles[i].Y) };
                    glyphs[_RemapBuffer[i].Index - _GlyphCount] =
                        glyphs[_RemapBuffer[i].Index - _GlyphCount].PlaceBox(_Rectangles[i].X, _Rectangles[i].Y);
                }
            }

            Generator.Generate(glyphs);

            _GlyphCount += glyphs.Count;
            return changeFlags;
        }

    }
}
