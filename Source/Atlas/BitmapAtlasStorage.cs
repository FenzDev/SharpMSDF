using SharpMSDF.Core;

namespace SharpMSDF.Atlas
{
    public class BitmapAtlasStorage<T> : AtlasStorage where T : struct
    {
        public Bitmap<T> Bitmap;

        public BitmapAtlasStorage() { }

        public override void Init(int width, int height, int channels)
        {
            Bitmap = new Bitmap<T>(width, height, channels);
            Array.Clear(Bitmap.Pixels, 0, Bitmap.Pixels.Length);
        }

        public void Init(BitmapConstRef<T> bitmap)
        {
            Bitmap = new Bitmap<T>(bitmap.OriginalWidth, bitmap.OriginalHeight, bitmap.N);
            Array.Copy(bitmap._Pixels, Bitmap.Pixels, Bitmap.Pixels.Length);
        }

        public void Init(Bitmap<T> bitmap)
        {
            Bitmap = bitmap;
        }

        public override void Init<TStorage>(TStorage orig, int width, int height)
        {
            if (orig is BitmapAtlasStorage<T> origStorage)
            {
                Bitmap = new Bitmap<T>(width, height, origStorage.Bitmap.N);
                Blitter.Blit(new BitmapRef<T>(Bitmap), origStorage.Bitmap, 0, 0, 0, 0, Math.Min(width, origStorage.Bitmap.Width), Math.Min(height, origStorage.Bitmap.Height));
            }

        }

        public override void Init<TStorage>(TStorage orig, int width, int height, Span<Remap> remapping)
        {
            if (orig is BitmapAtlasStorage<T> origStorage)
            {
                Bitmap = new Bitmap<T>(width, height, origStorage.Bitmap.N);
                for (int i = 0; i < remapping.Length; ++i)
                {
                    var remap = remapping[i];
                    Blitter.Blit(new BitmapRef<T>(Bitmap), origStorage.Bitmap, remap.Target.X, remap.Target.Y, remap.Source.X, remap.Source.Y, remap.Width, remap.Height);
                }
            }
        }

        public static implicit operator BitmapConstRef<T>(BitmapAtlasStorage<T> storage) => new BitmapConstRef<T>(storage.Bitmap);
        public static implicit operator BitmapRef<T>(BitmapAtlasStorage<T> storage) => new BitmapRef<T>(storage.Bitmap);
        public Bitmap<T> Move() => Bitmap;
        
        public override void Put<S>(int x, int y, BitmapConstRef<S> subBitmap) where S : struct
        {
            if (subBitmap is BitmapConstRef<T> sameTypeSubBitmap)
            {
                Blitter.Blit((BitmapRef<T>)Bitmap, sameTypeSubBitmap, x, y, 0, 0, subBitmap.SubWidth, subBitmap.SubHeight);
            }
            else if (subBitmap is BitmapConstRef<float> subBitmapFloat && Bitmap is Bitmap<byte> bytemap)
            {
                Blitter.Blit(new BitmapRef<byte>(bytemap), subBitmapFloat, x, y, 0, 0, subBitmap.SubWidth, subBitmap.SubHeight);
            }
            else
            {
                throw new NotSupportedException($"Blit from {typeof(S).Name} to {typeof(T).Name} is not supported.");
            }
        }

        public override void Get<S>(int x, int y, BitmapRef<S> subBitmap)
            where S : struct
        {
            throw new NotImplementedException();
        }
    }
}
