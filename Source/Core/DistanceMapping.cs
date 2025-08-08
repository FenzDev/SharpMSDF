
using System;

namespace SharpMSDF.Core
{
    public struct DistanceMapping
    {
        // Explicitly designates value as distance delta rather than an absolute distance.
        public class Delta
        {
            public float Value { get; }

            public Delta(float distanceDelta)
            {
                Value = distanceDelta;
            }

            public static implicit operator float(Delta d) => d.Value;
        }

        private readonly float scale;
        private readonly float translate;

        public DistanceMapping()
        {
            scale = 1.0f;
            translate = 0.0f;
        }

        public DistanceMapping(DoubleRange range)
        {
            //float extent = range.Upper - range.Lower;
            scale = 1 / (range.Upper - range.Lower);
            translate = -range.Lower;
        }
        //scale(1/(range.upper-range.lower)), translate(-range.lower)

        private DistanceMapping(float scale, float translate)
        {
            this.scale = scale;
            this.translate = translate;
        }

        public float this[float d] => scale * ( d + translate);

        public float this[Delta d] => d.Value * scale;

        public DistanceMapping Inverse()
        {
            return new DistanceMapping(1.0f / scale, -scale * translate);
        }

        public static DistanceMapping Inverse(DoubleRange range)
        {
            float rangeWidth = range.Upper - range.Lower;
            return new DistanceMapping(rangeWidth, range.Lower / (rangeWidth!=0 ? rangeWidth : 1.0f));

        }
    }

}