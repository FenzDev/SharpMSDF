using SharpMSDF.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpMSDF.Atlas
{

    /// <summary>
    /// Prototype of an atlas generator class.
    /// An atlas generator maintains the atlas bitmap (AtlasStorage) and its _Layout and facilitates
    /// generation of bitmap representation of glyphs. The _Layout of the atlas is given by the caller.
    /// </summary>
    public abstract class AtlasGenerator
    {
        /// <summary>
        /// Generates bitmap representation for the supplied array of glyphs.
        /// </summary>
        public abstract void Generate(List<GlyphGeometry> glyphs);

        /// <summary>
        /// Resizes the atlas and rearranges the generated pixels according to the remapping array.
        /// </summary>
        public abstract void Rearrange(int width, int height, List<Remap> remapping, int count);

        /// <summary>
        /// Resizes the atlas and keeps the generated pixels in place.
        /// </summary>
        public abstract void Resize(int width, int height);
    }

    /// <summary>
    /// Configuration of signed distance field generator.
    /// </summary>
    public struct GeneratorAttributes
    {
        public MSDFGeneratorConfig Config;
        public bool ScanlinePass;
    }

    /// <summary>
    /// A delegate that generates the bitmap for a single glyph.
    /// </summary>
    public delegate void GeneratorFunction<T>(
        BitmapRef<T> bitmap,
        GlyphGeometry glyph,
        GeneratorAttributes attributes
    ) where T : struct;

}
