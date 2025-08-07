
using System;

namespace SharpMSDF.Core
{
    public struct DistanceMapping
    {
        // Explicitly designates value as distance delta rather than an absolute distance.
        public class Delta
        {
            public double Value { get; }

            public Delta(double distanceDelta)
            {
                Value = distanceDelta;
            }

            public static implicit operator double(Delta d) => d.Value;
        }

        private readonly double scale;
        private readonly double translate;

        public DistanceMapping()
        {
            scale = 1.0;
            translate = 0.0;
        }

        public DistanceMapping(DoubleRange range)
        {
            //double extent = range.Upper - range.Lower;
            scale = 1 / (range.Upper - range.Lower);
            translate = -range.Lower;
        }
        //scale(1/(range.upper-range.lower)), translate(-range.lower)

        private DistanceMapping(double scale, double translate)
        {
            this.scale = scale;
            this.translate = translate;
        }

        public double this[double d] => scale * ( d + translate);

        public double this[Delta d] => d.Value * scale;

        public DistanceMapping Inverse()
        {
            return new DistanceMapping(1.0 / scale, -scale * translate);
        }

        public static DistanceMapping Inverse(DoubleRange range)
        {
            double rangeWidth = range.Upper - range.Lower;
            return new DistanceMapping(rangeWidth, range.Lower / (rangeWidth!=0 ? rangeWidth : 1.0));

        }
    }

}