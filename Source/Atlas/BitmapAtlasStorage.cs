using SharpMSDF.Core;
using System.Buffers;
using System.Drawing;

namespace SharpMSDF.Atlas
{
    public struct BitmapAtlasStorage : IAtlasStorage<BitmapAtlasStorage>
    {
        public Bitmap<byte> Bitmap;

        public BitmapAtlasStorage() { }

        public void Init(int width, int height, int channels)
        {
            int size = width * height * channels;
            var bitmapArray = ArrayPool<byte>.Shared.Rent(size);
            Bitmap = new Bitmap<byte>(bitmapArray, width, height, channels);
        }

        public void Init(BitmapConstRef<byte> bitmap)
        {
            int size = bitmap.SubWidth * bitmap.SubHeight * bitmap.N;
            var bitmapArray = ArrayPool<byte>.Shared.Rent(size);
            if (Bitmap.Pixels != null)
                Destroy();
            Bitmap = new Bitmap<byte> (bitmapArray, bitmap.OriginalWidth, bitmap.OriginalHeight, bitmap.N);
            Array.Clear(Bitmap.Pixels, 0, Bitmap.Pixels.Length);
            Array.Copy(bitmap._Pixels, Bitmap.Pixels, size);
        }

        public void Init(Bitmap<byte> bitmap)
        {
            Bitmap = bitmap;
        }

        public void Init(BitmapAtlasStorage orig, int width, int height)
        {
            // TODO: if length of old array is sam no need to rerent
            if (Bitmap.Pixels != null)
                Destroy();
            var bitmapArray = ArrayPool<byte>.Shared.Rent(width*height*orig.Bitmap.N);
            Bitmap = new Bitmap<byte>(bitmapArray, width, height, orig.Bitmap.N);
            Array.Clear(Bitmap.Pixels, 0, Bitmap.Pixels.Length);
            Blitter.Blit(new BitmapRef<byte>(Bitmap), orig.Bitmap, 0, 0, 0, 0, Math.Min(width, orig.Bitmap.Width), Math.Min(height, orig.Bitmap.Height));
        }

        public void Init(BitmapAtlasStorage orig, int width, int height, Span<Remap> remapping)
        {
            int size = width * height * orig.Bitmap.N;
            if (Bitmap.Pixels != null)
                Destroy();
            var bitmapArray = ArrayPool<byte>.Shared.Rent(size);
            Bitmap = new Bitmap<byte>(bitmapArray, width, height, orig.Bitmap.N);
            Array.Clear(Bitmap.Pixels, 0, Bitmap.Pixels.Length);
            for (int i = 0; i < remapping.Length; ++i)
            {
                var remap = remapping[i];
                Blitter.Blit(new BitmapRef<byte>(Bitmap), orig.Bitmap, remap.Target.X, remap.Target.Y, remap.Source.X, remap.Source.Y, remap.Width, remap.Height);
            }
        }

        public static implicit operator BitmapConstRef<byte>(BitmapAtlasStorage storage) => new BitmapConstRef<byte>(storage.Bitmap);
        public static implicit operator BitmapRef<byte>(BitmapAtlasStorage storage) => new BitmapRef<byte>(storage.Bitmap);
        public Bitmap<byte> Move() => Bitmap;

        public void Put(int x, int y, BitmapConstRef<float> subBitmap)
        {
            Blitter.Blit(Bitmap, subBitmap, x, y, 0, 0, subBitmap.SubWidth, subBitmap.SubHeight);
        }
        public void Put(int x, int y, BitmapConstRef<byte> subBitmap)
        {
            Blitter.Blit(Bitmap, subBitmap, x, y, 0, 0, subBitmap.SubWidth, subBitmap.SubHeight);
        }

        public void Get(int x, int y, BitmapRef<byte> subBitmap)
        {
            Blitter.Blit(subBitmap, Bitmap, 0, 0, x, y, subBitmap.SubWidth, subBitmap.SubHeight);
        }
        
        public void Destroy()
        {
            ArrayPool<byte>.Shared.Return(Bitmap.Pixels);
        }
    }
}
