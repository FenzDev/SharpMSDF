﻿using static System.Formats.Asn1.AsnWriter;

namespace SharpMSDF.Core
{
    public class Projection
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
        /// Converts the Shape coordinate to pixel coordinate.
        /// Converts the vector to pixel coordinate space.
        public Vector2 ProjectVector(Vector2 vector) => Scale * vector;
        /// Converts the vector from pixel coordinate space.
        public Vector2 UnprojectVector(Vector2 vector) => vector / Scale;
        /// Converts the X-coordinate from Shape to pixel coordinate space.
        public double ProjectX(double x) => Scale.X * (x + Translate.X);
        /// Converts the Y-coordinate from Shape to pixel coordinate space.
        public double ProjectY(double y) => Scale.Y * (y + Translate.Y);
        /// Converts the X-coordinate from pixel to Shape coordinate space.
        public double UnprojectX(double x) => x / Scale.X - Translate.X;
        /// Converts the Y-coordinate from pixel to Shape coordinate space.
        public double UnprojectY(double y) => y / Scale.Y - Translate.Y;

        public readonly Vector2 Scale;
        public readonly Vector2 Translate;

    }
}
