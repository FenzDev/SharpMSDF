using System.Numerics;
using static System.Formats.Asn1.AsnWriter;

namespace SharpMSDF.Core
{
    public struct Projection
    {
        public Projection() 
        {
            Scale = new Vector2(1);
            Translate = new Vector2(0);
        }
        public Projection(Vector2 scale, Vector2 translate)
        {
            Scale = scale;
            Translate = translate;
        }

        /// Converts the shape coordinate to pixel coordinate.
        public Vector2 Project(Vector2 coord) => Scale * (coord + Translate);
        /// Converts the pixel coordinate to shape coordinate.
        public Vector2 Unproject(Vector2 coord) => coord / Scale - Translate;
        /// Converts the vector to pixel coordinate space.
        public Vector2 ProjectVector(Vector2 vector) => Scale * vector;
        /// Converts the vector from pixel coordinate space.
        public Vector2 UnprojectVector(Vector2 vector) => vector / Scale;
        /// Converts the X-coordinate from Shape to pixel coordinate space.
        public float ProjectX(float x) => Scale.X * (x + Translate.X);
        /// Converts the Y-coordinate from Shape to pixel coordinate space.
        public float ProjectY(float y) => Scale.Y * (y + Translate.Y);
        /// Converts the X-coordinate from pixel to Shape coordinate space.
        public float UnprojectX(float x) => x / Scale.X - Translate.X;
        /// Converts the Y-coordinate from pixel to Shape coordinate space.
        public float UnprojectY(float y) => y / Scale.Y - Translate.Y;

        public readonly Vector2 Scale;
        public readonly Vector2 Translate;

    }
}
