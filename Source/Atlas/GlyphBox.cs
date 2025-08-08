using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpMSDF.Atlas
{
    public struct Bounds
    {
        public float L, B, R, T;
    }

    /// <summary>
    /// The glyph box - its bounds in plane and atlas
    /// </summary>
    public struct GlyphBox
    {
        public ushort Index;
        public float Advance;
        public Bounds Bounds;   
        public AtlasRectangle Rect;

    }

}
