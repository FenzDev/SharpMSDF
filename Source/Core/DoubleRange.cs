
namespace SharpMSDF.Core
{
    /// <summary>
    /// Represents the range between two real values.
    /// For example, the range of representable signed distances.
    /// </summary>
    public struct DoubleRange
    {
        public float Lower, Upper;

        public DoubleRange(float symmetricalWidth = 0)
        {
            Lower = -0.5f * symmetricalWidth;
            Upper = 0.5f * symmetricalWidth;
        }

        public DoubleRange(float lowerBound, float upperBound)
        {
            Lower = lowerBound;
            Upper = upperBound;
        }

        public static DoubleRange operator *(DoubleRange range, float factor)
        {
            return new DoubleRange(range.Lower * factor, range.Upper * factor);
        }

        public static DoubleRange operator *(float factor, DoubleRange range)
        {
            return new DoubleRange(factor * range.Lower, factor * range.Upper);
        }

        public static DoubleRange operator /(DoubleRange range, float divisor)
        {
            return new DoubleRange(range.Lower / divisor, range.Upper / divisor);
        }

        public void MultiplyInPlace(float factor)
        {
            Lower *= factor;
            Upper *= factor;
        }

        public void DivideInPlace(float divisor)
        {
            Lower /= divisor;
            Upper /= divisor;
        }

        public DoubleRange Multiply(float factor)
        {
            return new DoubleRange(Lower * factor, Upper * factor);
        }

        public DoubleRange Divide(float divisor)
        {
            return new DoubleRange(Lower / divisor, Upper / divisor);
        }

        public static DoubleRange operator +(DoubleRange a, DoubleRange b) => new DoubleRange(a.Lower + b.Lower, a.Upper + b.Upper);

    }
}
