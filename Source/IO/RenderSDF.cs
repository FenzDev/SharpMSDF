using System;
using System.ComponentModel;
using System.Numerics;
using SharpMSDF.Core;
using FloatRGB = (float R, float G, float B);

namespace SharpMSDF.IO
{
    public static class Render
    {
        private static FloatRGB Mix(FloatRGB distA, FloatRGB distB, float weight)
        {
            return new ()
            {
                R = Arithmetic.Mix(distA.R, distB.R, weight),
                G = Arithmetic.Mix(distA.G, distB.G, weight),
                B = Arithmetic.Mix(distA.B, distB.B, weight),
            };
        }

        private static FloatRGB Sample3(BitmapConstRef<float> bitmap, Vector2 pos)
        {
            int w = bitmap.SubWidth, h = bitmap.SubHeight;
            var x = pos.X * w - .5f;
            var y = pos.Y * h - .5f;
            var l = (int) MathF.Floor(x);
            var b = (int) MathF.Floor(y);
            var r = l + 1;
            var t = b + 1;
            var lr = x - l;
            var bt = y - b;
            l = Math.Clamp(l, 0, w - 1);
            r = Math.Clamp(r, 0, w - 1);
            b = Math.Clamp(b, 0, h - 1);
            t = Math.Clamp(t, 0, h - 1);
            return Mix(Mix(ToMulti(bitmap, l, b), ToMulti(bitmap, r, b), lr), Mix(ToMulti(bitmap, l, t), ToMulti(bitmap, r, t), lr), bt);
        }

        private static FloatRGB ToMulti(BitmapConstRef<float> bitmap, int x, int y) => new()
        {
            R = bitmap[x,y,0],
            G = bitmap[x,y,1],
            B = bitmap[x,y,2],
        };
        private static float Sample1(BitmapConstRef<float> bitmap, Vector2 pos)
        {
            int w = bitmap.SubWidth, h = bitmap.SubHeight;
            var x = pos.X * w - .5f;
            var y = pos.Y * h - .5f;
            var l = (int)MathF.Floor(x);
            var b = (int)MathF.Floor(y);
            var r = l + 1;
            var t = b + 1;
            var lr = x - l;
            var bt = y - b;
            l = Math.Clamp(l, 0, w - 1);
            r = Math.Clamp(r, 0, w - 1);
            b = Math.Clamp(b, 0, h - 1);
            t = Math.Clamp(t, 0, h - 1);
            return Arithmetic.Mix(Arithmetic.Mix(bitmap[l, b], bitmap[r, b], lr),
                Arithmetic.Mix(bitmap[l, t], bitmap[r, t], lr), bt);

        }

        private static float DistVal(float dist, double pxRange)
        {
            if (pxRange == 0)
                return dist > .5 ? 1 : 0;
            return (float) Math.Clamp((dist - .5) * pxRange + .5, 0, 1);
        }


        /// <summary>
        /// Reconstructs the shape's appearance into output from the distance field sdf.
        /// </summary>
        public static void RenderSdf(BitmapRef<float> output, BitmapConstRef<float> sdf, double pxRange)
        {
            int w = output.SubWidth, h = output.SubHeight;
            pxRange *= (double) (w + h) / (sdf.SubWidth + sdf.SubHeight);
            switch (sdf.N)
            {
                case 1:
                    for (var y = 0; y < h; ++y)
                    for (var x = 0; x < w; ++x)
                    {
                        var s = Sample1(sdf, new Vector2((x + .5f) / w, (y + .5f) / h));
                        output[x, y] = DistVal(s, pxRange);
                    }
                    break;
                case 3:
                    for (var y = 0; y < h; ++y)
                        for (var x = 0; x < w; ++x)
                        {
                            var s = Sample3(sdf, new Vector2((x + .5f) / w, (y + .5f) / h));
                            output[x, y] = DistVal(Arithmetic.Median(s.R, s.G, s.B), pxRange);
                        }

                    break;
                case 4:
                    for (var y = 0; y < h; ++y)
                        for (var x = 0; x < w; ++x)
                        {
                            var s = Sample3(sdf, new Vector2((x + .5f) / w, (y + .5f) / h));
                            output[x, y] = DistVal(Arithmetic.Median(s.R, s.G, s.B), pxRange);
                        }

                    break;
            }
        }

        public static void Simulate8Bit(BitmapRef<float> bitmap)
        {
            for (var y = 0; y < bitmap.SubHeight; ++y)
            for (var x = 0; x < bitmap.SubWidth; ++x)
            {
                switch (bitmap.N)
                    {
                        case 1:
                            var v = (byte) Math.Clamp(bitmap[x, y] * 0x100, 0, 0xff);
                            bitmap[x, y] = v / 255.0f;
                            break;
                        case 3:
                            var r = (byte)Math.Clamp(bitmap[x, y, 0] * 0x100, 0, 0xff);
                            var g = (byte)Math.Clamp(bitmap[x, y, 1] * 0x100, 0, 0xff);
                            var b = (byte)Math.Clamp(bitmap[x, y, 2] * 0x100, 0, 0xff);
                            bitmap[x, y, 0] = r / 255.0f;
                            bitmap[x, y, 1] = g / 255.0f;
                            bitmap[x, y, 2] = b / 255.0f;
                            break;
                    }

                }
        }

    }
}