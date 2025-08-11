using SharpMSDF.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpMSDF.Atlas
{
    public interface IAtlasStorage<TSelf> where TSelf : IAtlasStorage<TSelf>
    {

        public abstract void Init(int width, int height, int channels);

        /// <summary>
        /// Creates a copy with different dimensions
        /// </summary>
        public abstract void Init(TSelf orig, int width, int height);
        /// <summary>
        /// /Creates a copy with different dimensions and rearranges the pixels according to the remapping array
        /// </summary>
        public abstract void Init(TSelf orig, int width, int height, Span<Remap> remapping);
        /// <summary>
        /// Destroy the storage, and free it from memory
        /// </summary>
        public abstract void Destroy();
        /// <summary>
        /// Stores a subsection at x, y into the atlas _Storage. May be implemented for only some TRect, N
        /// </summary>
        public abstract void Put(int x, int y, BitmapConstRef<float> subBitmap);
        public abstract void Put(int x, int y, BitmapConstRef<byte> subBitmap);
        /// <summary>
        /// Retrieves a subsection at x, y from the atlas _Storage. May be implemented for only some TRect, N
        /// </summary>
        public abstract void Get(int x, int y, BitmapRef<byte> subBitmap);

    };

}

