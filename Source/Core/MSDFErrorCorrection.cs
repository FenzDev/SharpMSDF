
using System;
using System.Numerics;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SharpMSDF.Core
{

    public unsafe ref struct ArtifactClassifier<TCombiner>
        where TCombiner : IContourCombiner<PerpendicularDistanceSelector, float>, new()
    {
        public const byte CLASSIFIER_FLAG_CANDIDATE = 0x01;
        public const byte CLASSIFIER_FLAG_ARTIFACT = 0x02;
        ShapeDistanceChecker<TCombiner> Parent;
        Vector2 Direction;

        public readonly int N;

        public readonly float Span;
        public readonly bool ProtectedFlag;


        public ArtifactClassifier(float span, bool protectedFlag)
        {
            Span = span;
            ProtectedFlag = protectedFlag;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArtifactClassifier(ShapeDistanceChecker<TCombiner> parent, Vector2 direction, float span, int channels) : this(span, parent.ProtectedFlag)
        {
            Parent = parent; Direction = direction;
            N = channels;
        }

        /// <summary>
        /// Evaluates if the median value xm interpolated at xt in the range between am at at and bm at bt indicates an artifact.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int RangeTest(float at, float bt, float xt, float am, float bm, float xm)
        {
            // For protected texels, only consider inversion artifacts (interpolated median has different sign than boundaries). For the rest, it is sufficient that the interpolated median is outside its boundaries.
            if ((am > .5f && bm > .5f && xm <= .5f) || (am < .5f && bm < .5f && xm >= .5f) || (!ProtectedFlag && Arithmetic.Median(am, bm, xm) != xm))
            {
                float axSpan = (xt - at) * Span, bxSpan = (bt - xt) * Span;
                // Check if the interpolated median's value is in the expected range based on its distance (span) from boundaries a, b.
                if (!(xm >= am - axSpan && xm <= am + axSpan && xm >= bm - bxSpan && xm <= bm + bxSpan))
                    return CLASSIFIER_FLAG_CANDIDATE | CLASSIFIER_FLAG_ARTIFACT;
                return CLASSIFIER_FLAG_CANDIDATE;
            }
            return 0;
        }

        /// Returns true if the combined results of the tests performed on the median value m interpolated at t indicate an artifact.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Evaluate(float t, float m, int flags)
        {
            if ((flags & CLASSIFIER_FLAG_CANDIDATE) != 0)
            {
                // Skip expensive distance evaluation if the point has already been classified as an artifact by the base classifier.
                if ((flags & CLASSIFIER_FLAG_ARTIFACT) != 0)
                    return true;

                Vector2 tVector = t * Direction;
                Span<float> oldMSD = stackalloc float[N];
                Span<float> newMSD = stackalloc float[3];

                // Compute the color that would be currently interpolated at the artifact candidate's position.
                Vector2 sdfCoord = Parent.SdfCoord + tVector;
                Bitmap<float>.Interpolate(oldMSD, Parent.Sdf, sdfCoord);
                // Compute the color that would be interpolated at the artifact candidate's position if error correction was applied on the current texel.
                float aWeight = (1 - Math.Abs(tVector.X)) * (1 - Math.Abs(tVector.Y));
                float aPSD = Arithmetic.Median(Parent.Msd[0], Parent.Msd[1], Parent.Msd[2]);
                newMSD[0] = (float)(oldMSD[0] + aWeight * (aPSD - Parent.Msd[0]));
                newMSD[1] = (float)(oldMSD[1] + aWeight * (aPSD - Parent.Msd[1]));
                newMSD[2] = (float)(oldMSD[2] + aWeight * (aPSD - Parent.Msd[2]));
                // Compute the evaluated distance (interpolated median) before and after error correction, as well as the exact shape distance.
                float oldPSD = Arithmetic.Median(oldMSD[0], oldMSD[1], oldMSD[2]);
                float newPSD = Arithmetic.Median(newMSD[0], newMSD[1], newMSD[2]);
                float refPSD = (float)(Parent.DistanceMapping[Parent.DistanceFinder.Distance(Parent.ShapeCoord + tVector * Parent.TexelSize)]);
                // Compare the differences of the exact distance and the before and after distances.
                return Parent.MinImproveRatio * MathF.Abs(newPSD - refPSD) < (double)MathF.Abs(oldPSD - refPSD);
            }
            return false;
        }
    }

    public unsafe ref struct ShapeDistanceChecker<TCombiner>
        where TCombiner : IContourCombiner<PerpendicularDistanceSelector, float>, new()
    {

        public const byte CLASSIFIER_FLAG_CANDIDATE = 0x01;
        public const byte CLASSIFIER_FLAG_ARTIFACT = 0x02;
        
        internal float Span;
        internal bool ProtectedFlag;

        public Vector2 ShapeCoord, SdfCoord;
        public float* Msd;

        internal ShapeDistanceFinder<TCombiner, PerpendicularDistanceSelector, float> DistanceFinder;
        internal readonly BitmapConstRef<float> Sdf;
        internal readonly DistanceMapping DistanceMapping;
        internal readonly Vector2 TexelSize;
        internal readonly float MinImproveRatio;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ShapeDistanceChecker(BitmapConstRef<float> sdf, Shape shape, Projection projection, DistanceMapping distanceMapping, float minImproveRation)
        {
            Sdf = sdf;
            DistanceFinder = new(shape);
            DistanceMapping = distanceMapping;
            MinImproveRatio = minImproveRation;

            TexelSize = projection.UnprojectVector(new(1));
        }
        public ArtifactClassifier<TCombiner> Classifier(Vector2 direction, float span)
        {
            return new ArtifactClassifier<TCombiner>(this, direction, span, Sdf.N);
        }
    }


    /// <summary>
    /// Performs error correction on a computed MSDF to eliminate interpolation artifacts. This is a low-level class, you may want to use the API in msdf-error-correction.h instead.
    /// </summary>
    public class MSDFErrorCorrection
    {



        public const float ARTIFACT_T_EPSILON = 0.01f;
        public const float PROTECTION_RADIUS_TOLERANCE = 1.001f;


        /// Stencil flags.
        public enum Flags : byte
        {
            /// <summary>
            /// Texel marked as potentially causing interpolation errors.
            /// </summary>
            Error = 1,
            /// <summary>
            /// Texel marked as protected. Protected texels are only given the error flag if they cause inversion artifacts.
            /// </summary>
            Protected = 2
        }

        private BitmapRef<byte> Stencil;
        private SDFTransformation Transformation;
        private float MinDeviationRatio;
        private float MinImproveRatio;

        public MSDFErrorCorrection()
        {
        }

        public MSDFErrorCorrection(BitmapRef<byte> stencil, SDFTransformation transformation)
        {
            Stencil = stencil; 
            Transformation = transformation;
            MinDeviationRatio = ErrorCorrectionConfig.DefaultMinDeviationRatio;
            MinImproveRatio = ErrorCorrectionConfig.DefaultMinImproveRatio;
            Array.Clear(stencil.Pixels, 0, stencil.SubWidth * stencil.SubHeight);
        }

        /// <summary>
        /// Sets the minimum ratio between the actual and maximum expected distance delta to be considered an error.
        /// </summary>
        /// <param name="ratio"></param>
        public void SetMinDeviationRatio(float ratio)
        {
            MinDeviationRatio = ratio;
        }
        /// <summary>
        /// Sets the minimum ratio between the pre-correction distance error and the post-correction distance error.
        /// </summary>
        /// <param name="ratio"></param>
        public void SetMinImproveRatio(float ratio)
        {
            MinImproveRatio = ratio;
        }

        public void ProtectCorners(Shape shape)
        {
            for (int c=0; c < shape.Contours.Count; c++)
            {
                ref Contour contour = ref shape.Contours[c];
                if (contour.Edges.Count == 0)
                    continue;

                var prevEdge = contour.Edges[contour.Edges.Count - 1];
                for (int e = 0; e < contour.Edges.Count; e++)
                {
                    ref EdgeSegment edge = ref contour.Edges[e];
                    int commonColor = (int)(prevEdge.Color & edge.Color);

                    // If color changes -> it's a corner
                    if ((commonColor & (commonColor - 1)) == 0)
                    {
                        var p = Transformation.Projection.Project(edge.Point(0));
                        if (shape.InverseYAxis)
                            p.Y = Stencil.SubHeight - p.Y;

                        int l = (int)Math.Floor(p.X - 0.5);
                        int b = (int)Math.Floor(p.Y - 0.5);
                        int r = l + 1;
                        int t = b + 1;

                        // Bounds check and mark 2x2 texel region as PROTECTED
                        if (l < Stencil.SubWidth && b < Stencil.SubHeight && r >= 0 && t >= 0)
                        {
                            if (l >= 0 && b >= 0)
                                Stencil[l, b] |= (byte)Flags.Protected;
                            if (r < Stencil.SubWidth && b >= 0)
                                Stencil[r, b] |= (byte)Flags.Protected;
                            if (l >= 0 && t < Stencil.SubHeight)
                                Stencil[l, t] |= (byte)Flags.Protected;
                            if (r < Stencil.SubWidth && t < Stencil.SubHeight)
                                Stencil[r, t] |= (byte)Flags.Protected;
                        }
                    }

                    prevEdge = edge;
                }
            }
        }

        /// <summary>
        /// Determines if the channel contributes to an edge between the two texels a, b.
        /// </summary>
        public static unsafe byte EdgeBetweenTexelsChannel(float* a, float* b, int channel)
        {
            Span<float> c = stackalloc float[3];
            // Find interpolation ratio t (0 < t < 1) where an edge is expected (mix(a[channel], b[channel], t) == 0.5).
            float t = (a[channel] - .5f) / (a[channel] - b[channel]);
            if (t > 0 && t < 1)
            {
                // Interpolate channel values at t.
                c[0] = Arithmetic.Mix(a[0], b[0], t);
                c[1] = Arithmetic.Mix(a[1], b[1], t);
                c[2] = Arithmetic.Mix(a[2], b[2], t);
                // This is only an edge if the zero-distance channel is the median.
                return (Arithmetic.Median(c[0], c[1], c[2]) == c[channel]) ? (byte)1 : (byte)0;
            }
            return 0;
        }


        /// Returns a bit mask of which channels contribute to an edge between the two texels a, b.
        public static unsafe int EdgeBetweenTexels(float* a, float* b)
        {
            return
                (byte)EdgeColor.Red * EdgeBetweenTexelsChannel(a, b, 0) +
                (byte)EdgeColor.Green * EdgeBetweenTexelsChannel(a, b, 1) +
                (byte)EdgeColor.Blue * EdgeBetweenTexelsChannel(a, b, 2);
        }


        /// Marks texel as protected if one of its non-median channels is present in the channel mask.
        public static unsafe void ProtectExtremeChannels(byte* stencil, float* msd, float m, int mask)
        {
            if (
                ((mask & (byte)EdgeColor.Red) != 0 && (int)msd[0] != m) ||
                ((mask & (byte)EdgeColor.Green) != 0 && (int)msd[1] != m) ||
                ((mask & (byte)EdgeColor.Blue) != 0 && (int)msd[2] != m)
            )
                *stencil |= (byte)Flags.Protected;
        }

        public unsafe void ProtectEdges(BitmapConstRef<float> sdf)
        {
            fixed (byte* stencil = Stencil.Pixels)
            fixed (float* pixels = sdf._Pixels)
            {
                float radius = (float)(PROTECTION_RADIUS_TOLERANCE * Transformation.Projection.UnprojectVector(new(Transformation.DistanceMapping[new DistanceMapping.Delta(1)], 0)).Length());
                for (int y = 0; y < sdf.SubHeight; ++y)
                {
                    float* left = pixels + sdf.GetIndex(0, y);
                    float* right = pixels + sdf.GetIndex(1, y);
                    for (int x = 0; x < sdf.SubWidth - 1; ++x)
                    {
                        float lm = Arithmetic.Median(left[0], left[1], left[2]);
                        float rm = Arithmetic.Median(right[0], right[1], right[2]);
                        if (Math.Abs(lm - .5f) + Math.Abs(rm - .5f) < radius)
                        {
                            int mask = EdgeBetweenTexels(left, right);
                            ProtectExtremeChannels(stencil + Stencil.GetIndex(x, y), left, lm, mask);
                            ProtectExtremeChannels(stencil + Stencil.GetIndex(x + 1, y), right, rm, mask);
                        }
                        left += sdf.N;
                        right += sdf.N;
                    }
                }
                // Vertical texel pairs
                radius = (float)(PROTECTION_RADIUS_TOLERANCE * Transformation.Projection.UnprojectVector(new(0, Transformation.DistanceMapping[new(1)])).Length());
                for (int y = 0; y < sdf.SubHeight - 1; ++y)
                {
                    float* bottom = pixels + sdf.GetIndex(0, y);
                    float* top = pixels + sdf.GetIndex(0, y + 1);
                    for (int x = 0; x < sdf.SubWidth; ++x)
                    {
                        float bm = Arithmetic.Median(bottom[0], bottom[1], bottom[2]);
                        float tm = Arithmetic.Median(top[0], top[1], top[2]);
                        if (Math.Abs(bm - .5f) + Math.Abs(tm - .5f) < radius)
                        {
                            int mask = EdgeBetweenTexels(bottom, top);
                            ProtectExtremeChannels(stencil + Stencil.GetIndex(x, y), bottom, bm, mask);
                            ProtectExtremeChannels(stencil + Stencil.GetIndex(x, y + 1), top, tm, mask);
                        }
                        bottom += sdf.N; top += sdf.N;
                    }
                }
                // Diagonal texel pairs
                radius = (float)(PROTECTION_RADIUS_TOLERANCE * Transformation.Projection.UnprojectVector(new(Transformation.DistanceMapping[new(1)])).Length());
                for (int y = 0; y < sdf.SubHeight - 1; ++y)
                {
                    float* lb = pixels + sdf.GetIndex(0, y);
                    float* rb = pixels + sdf.GetIndex(1, y);
                    float* lt = pixels + sdf.GetIndex(0, y + 1);
                    float* rt = pixels + sdf.GetIndex(1, y + 1);
                    for (int x = 0; x < sdf.SubWidth - 1; ++x)
                    {
                        float mlb = Arithmetic.Median(lb[0], lb[1], lb[2]);
                        float mrb = Arithmetic.Median(rb[0], rb[1], rb[2]);
                        float mlt = Arithmetic.Median(lt[0], lt[1], lt[2]);
                        float mrt = Arithmetic.Median(rt[0], rt[1], rt[2]);
                        if (Math.Abs(mlb - .5f) + Math.Abs(mrt - .5f) < radius)
                        {
                            int mask = EdgeBetweenTexels(lb, rt);
                            ProtectExtremeChannels(stencil + Stencil.GetIndex(x, y), lb, mlb, mask);
                            ProtectExtremeChannels(stencil + Stencil.GetIndex(x + 1, y + 1), rt, mrt, mask);
                        }
                        if (Math.Abs(mrb - .5f) + Math.Abs(mlt - .5f) < radius)
                        {
                            int mask = EdgeBetweenTexels(rb, lt);
                            ProtectExtremeChannels(stencil + Stencil.GetIndex(x + 1, y), rb, mrb, mask);
                            ProtectExtremeChannels(stencil + Stencil.GetIndex(x, y + 1), lt, mlt, mask);
                        }
                        lb += sdf.N; rb += sdf.N; lt += sdf.N; rt += sdf.N;
                    }
                }
            }
        }
        public void ProtectEdges(Shape shape)
        {
            Span<Vector2> points = stackalloc Vector2[3];

            for (int c = 0; c < shape.Contours.Count; c++)
            {
                for (int e = 0; e < shape.Contours[c].Edges.Count; e++)
                {
                    ref EdgeSegment edge = ref shape.Contours[c].Edges[e];

                    Vector2 p0 = edge.Point(0);
                    Vector2 p1 = edge.Point(1);
                    Vector2 pMid = edge.Point(0.5f);

                    points[0] = Transformation.Projection.ProjectVector(p0);
                    points[1] = Transformation.Projection.ProjectVector(pMid);
                    points[2] = Transformation.Projection.ProjectVector(Transformation.Projection.ProjectVector(p1));

                    if (shape.InverseYAxis)
                    {
                        for (int i = 0; i < points.Length; ++i)
                            points[i].Y = Stencil.SubHeight - points[i].Y;
                    }

                    foreach (var pt in points)
                    {
                        int x = (int)Math.Round(pt.X);
                        int y = (int)Math.Round(pt.Y);
                        if (x >= 0 && y >= 0 && x < Stencil.SubWidth && y < Stencil.SubHeight)
                        {
                            Stencil[x, y] |= (byte)Flags.Protected;
                        }
                    }
                }
            }
        }

        public unsafe void ProtectAll()
        {
            fixed (byte* stencil = Stencil.Pixels)
            {
                byte* end = stencil + Stencil.SubWidth * Stencil.SubHeight;
                for (byte* mask = stencil; mask < end; ++mask)
                    *mask |= (byte)Flags.Protected;
            }
        }

        static float InterpolatedMedian(ReadOnlySpan<float> a, ReadOnlySpan<float> b, float t)
        {
            return Arithmetic.Median(
                Arithmetic.Mix(a[0], b[0], t),
                Arithmetic.Mix(a[1], b[1], t),
                Arithmetic.Mix(a[2], b[2], t)
            );
        }

        static float InterpolatedMedian(ReadOnlySpan<float> a, ReadOnlySpan<float> l, ReadOnlySpan<float> q, float t)
        {
            return (float)Arithmetic.Median(
                t * (t * q[0] + l[0]) + a[0],
                t * (t * q[1] + l[1]) + a[1],
                t * (t * q[2] + l[2]) + a[2]
            );
        }
        /// Determines if the interpolated median xm is an artifact.
        static bool IsArtifact(bool isProtected, float axSpan, float bxSpan, float am, float bm, float xm)
        {
            return (
                // For protected texels, only report an artifact if it would cause fill inversion (change between positive and negative distance).
                (!isProtected || (am > .5f && bm > .5f && xm <= .5f) || (am < .5f && bm < .5f && xm >= .5f)) &&
                // This is an artifact if the interpolated median is outside the range of possible values based on its distance from a, b.
                !(xm >= am - axSpan && xm <= am + axSpan && xm >= bm - bxSpan && xm <= bm + bxSpan)
            );
        }

        /// Checks if a linear interpolation artifact will occur at a point where two specific color channels are equal - such points have extreme median values.
        static bool HasLinearArtifactInner<TCombiner>(ArtifactClassifier<TCombiner> artifactClassifier, float am, float bm, ReadOnlySpan<float> a, ReadOnlySpan<float> b, float dA, float dB)
            where TCombiner : IContourCombiner<PerpendicularDistanceSelector, float>, new()
        {
            // Find interpolation ratio t (0 < t < 1) where two color channels are equal (mix(dA, dB, t) == 0).
            float t = (float)dA / (dA - dB);
            if (t > ARTIFACT_T_EPSILON && t < 1 - ARTIFACT_T_EPSILON)
            {
                // Interpolate median at t and let the classifier decide if its value indicates an artifact.
                float xm = InterpolatedMedian(a, b, t);
                return artifactClassifier.Evaluate(t, xm, artifactClassifier.RangeTest(0, 1, t, am, bm, xm));
            }
            return false;
        }

        static bool HasLinearArtifact<TCombiner>(ArtifactClassifier<TCombiner> artifactClassifier, float am, ReadOnlySpan<float> a, ReadOnlySpan<float> b)
            where TCombiner : IContourCombiner<PerpendicularDistanceSelector, float>, new()
        {
            float bm = Arithmetic.Median(b[0], b[1], b[2]);
            return (
                // Out of the pair, only report artifacts for the texel further from the edge to minimize side effects.
                MathF.Abs(am - .5f) >= MathF.Abs(bm - .5f) && (
                    // Check points where each pair of color channels meets.
                    HasLinearArtifactInner(artifactClassifier, am, bm, a, b, a[1] - a[0], b[1] - b[0]) ||
                    HasLinearArtifactInner(artifactClassifier, am, bm, a, b, a[2] - a[1], b[2] - b[1]) ||
                    HasLinearArtifactInner(artifactClassifier, am, bm, a, b, a[0] - a[2], b[0] - b[2])
                )
            );
        }

        /// Checks if a bilinear interpolation artifact will occur at a point where two specific color channels are equal - such points have extreme median values.
        static bool HasDiagonalArtifactInner<TCombiner>(ArtifactClassifier<TCombiner> artifactClassifier, float am, float dm, ReadOnlySpan<float> a, ReadOnlySpan<float> l, Span<float> q, float dA, float dBC, float dD, float tEx0, float tEx1)
            where TCombiner : IContourCombiner<PerpendicularDistanceSelector, float>, new()
        {
            // Find interpolation ratios t (0 < t[i] < 1) where two color channels are equal.
            Span<float> t = stackalloc float[2];

            Span<float> tEnd = stackalloc float[2];
            Span<float> em = stackalloc float[2];

            int solutions = EquationSolver.SolveQuadratic(t, dD - dBC + dA, dBC - dA - dA, dA);
            for (int i = 0; i < solutions; ++i)
            {
                // Solutions t[i] == 0 and t[i] == 1 are singularities and occur very often because two channels are usually equal at texels.
                if (t[i] > ARTIFACT_T_EPSILON && t[i] < 1 - ARTIFACT_T_EPSILON)
                {
                    // Interpolate median xm at t.
                    float xm = InterpolatedMedian(a, l, q, t[i]);
                    // Determine if xm deviates too much from medians of a, d.
                    int rangeFlags = artifactClassifier.RangeTest(0, 1, t[i], am, dm, xm);
                    // Additionally, check xm against the interpolated medians at the local extremes tEx0, tEx1.

                    // tEx0
                    if (tEx0 > 0 && tEx0 < 1)
                    {
                        tEnd[0] = 0; tEnd[1] = 1;
                        em[0] = am; em[1] = dm;
                        tEnd[tEx0 > t[i] ? 1 : 0] = tEx0;
                        em[tEx0 > t[i] ? 1 : 0] = InterpolatedMedian(a, l, q, tEx0);
                        rangeFlags |= artifactClassifier.RangeTest(tEnd[0], tEnd[1], t[i], em[0], em[1], xm);
                    }
                    // tEx1
                    if (tEx1 > 0 && tEx1 < 1)
                    {
                        tEnd[0] = 0; tEnd[1] = 1;
                        em[0] = am; em[1] = dm;
                        tEnd[tEx1 > t[i] ? 1 : 0] = tEx1;
                        em[tEx1 > t[i] ? 1 : 0] = InterpolatedMedian(a, l, q, tEx1);
                        rangeFlags |= artifactClassifier.RangeTest(tEnd[0], tEnd[1], t[i], em[0], em[1], xm);
                    }
                    if (artifactClassifier.Evaluate(t[i], xm, rangeFlags))
                        return true;
                }
            }
            return false;
        }

        /// Checks if a bilinear interpolation artifact will occur inbetween two diagonally adjacent texels a, d (with b, c forming the other diagonal).
        static bool HasDiagonalArtifact<TCombiner>(ArtifactClassifier<TCombiner> artifactClassifier, float am, ReadOnlySpan<float> a, ReadOnlySpan<float> b, ReadOnlySpan<float> c, ReadOnlySpan<float> d)
            where TCombiner : IContourCombiner<PerpendicularDistanceSelector, float>, new()
        {
            float dm = Arithmetic.Median(d[0], d[1], d[2]);
            // Out of the pair, only report artifacts for the texel further from the edge to minimize side effects.
            if (MathF.Abs(am - .5f) >= MathF.Abs(dm - .5f))
            {
                Span<float> abc = [
                    a[0]-b[0]-c[0],
                    a[1]-b[1]-c[1],
                    a[2]-b[2]-c[2]
                ];
                // Compute the linear terms for bilinear interpolation.
                Span<float> l = [
                    -a[0]-abc[0],
                    -a[1]-abc[1],
                    -a[2]-abc[2]
                ];
                // Compute the quadratic terms for bilinear interpolation.
                Span<float> q = [
                    d[0]+abc[0],
                    d[1]+abc[1],
                    d[2]+abc[2]
                ];
                // Compute interpolation ratios tEx (0 < tEx[i] < 1) for the local extremes of each color channel (the derivative 2*q[i]*tEx[i]+l[i] == 0).
                Span<float> tEx = [
                    -.5f*l[0]/q[0],
                    -.5f*l[1]/q[1],
                    -.5f*l[2]/q[2]
                ];
                // Check points where each pair of color channels meets.
                return (
                    HasDiagonalArtifactInner(artifactClassifier, am, dm, a, l, q, a[1] - a[0], b[1] - b[0] + c[1] - c[0], d[1] - d[0], tEx[0], tEx[1]) ||
                    HasDiagonalArtifactInner(artifactClassifier, am, dm, a, l, q, a[2] - a[1], b[2] - b[1] + c[2] - c[1], d[2] - d[1], tEx[1], tEx[2]) ||
                    HasDiagonalArtifactInner(artifactClassifier, am, dm, a, l, q, a[0] - a[2], b[0] - b[2] + c[0] - c[2], d[0] - d[2], tEx[2], tEx[0])
                );
            }
            return false;
        }
        public unsafe void FindErrors<TCombiner>(BitmapConstRef<float> sdf)
                where TCombiner : IContourCombiner<PerpendicularDistanceSelector, float>, new()
        {
            ReadOnlySpan<float> dummy = stackalloc float[4];

            // Compute the expected deltas between values of horizontally, vertically, and diagonally adjacent texels.
            float hSpan = MinDeviationRatio * Transformation.Projection.UnprojectVector(new(Transformation.DistanceMapping[new(1)], 0)).Length();
            float vSpan = MinDeviationRatio * Transformation.Projection.UnprojectVector(new(0, Transformation.DistanceMapping[new(1)])).Length();
            float dSpan = MinDeviationRatio * Transformation.Projection.UnprojectVector(new(Transformation.DistanceMapping[new(1)])).Length();

            fixed (float* pixels = sdf._Pixels)
            {
                // Inspect all texels.
                for (int y = 0; y < sdf.SubHeight; ++y)
                {
                    for (int x = 0; x < sdf.SubWidth; ++x)
                    {
                        ReadOnlySpan<float> c = sdf._Pixels.AsSpan(sdf.GetIndex(x, y));
                        float cm = Arithmetic.Median(sdf[x, y, 0], sdf[x, y, 1], sdf[x, y, 2]);
                        bool protectedFlag = (Stencil[x, y] & (byte)Flags.Protected) != 0;
                        ReadOnlySpan<float> l = dummy, b = dummy, r = dummy, t = dummy;

                        // Mark current texel c with the error flag if an artifact occurs when it's interpolated with any of its 8 neighbors.
                        bool artifact = false;
                        do
                        {
                            if (x > 0)
                            {
                                l = sdf.Slice(x - 1, y);
                                if (HasLinearArtifact<TCombiner>(new (hSpan, protectedFlag), cm, c, l))
                                {
                                    artifact = true;
                                    break;
                                }
                            }

                            if (y > 0)
                            {
                                b = sdf.Slice(x, y - 1);
                                if (HasLinearArtifact<TCombiner>(new (vSpan, protectedFlag), cm, c, b))
                                {
                                    artifact = true;
                                    break;
                                }
                            }

                            if (x < sdf.SubWidth - 1)
                            {
                                r = sdf.Slice(x + 1, y);
                                if (HasLinearArtifact<TCombiner>(new (hSpan, protectedFlag), cm, c, r))
                                {
                                    artifact = true;
                                    break;
                                }
                            }

                            if (y < sdf.SubHeight - 1)
                            {
                                t = sdf.Slice(x, y + 1);
                                if (HasLinearArtifact<TCombiner>(new (vSpan, protectedFlag), cm, c, t))
                                {
                                    artifact = true;
                                    break;
                                }
                            }

                            if (x > 0 && y > 0 && HasDiagonalArtifact<TCombiner>(new (dSpan, protectedFlag), cm, c, l, b, sdf.Slice(x - 1, y - 1)))
                            {
                                artifact = true;
                                break;
                            }

                            if (x < sdf.SubWidth - 1 && y > 0 && HasDiagonalArtifact<TCombiner>(new (dSpan, protectedFlag), cm, c, r, b, sdf.Slice(x + 1, y - 1)))
                            {
                                artifact = true;
                                break;
                            }

                            if (x > 0 && y < sdf.SubHeight - 1 && HasDiagonalArtifact<TCombiner>(new (dSpan, protectedFlag), cm, c, l, t, sdf.Slice(x - 1, y + 1)))
                            {
                                artifact = true;
                                break;
                            }

                            if (x < sdf.SubWidth - 1 && y < sdf.SubHeight - 1 && HasDiagonalArtifact<TCombiner>(new (dSpan, protectedFlag), cm, c, r, t, sdf.Slice(x + 1, y + 1)))
                            {
                                artifact = true;
                                break;
                            }

                        } while (false);

                        if (artifact)
                            Stencil[x, y] |= (byte)Flags.Error;
                    }
                }
            }
        }


        public unsafe void FindErrors<TCombiner>(BitmapConstRef<float> sdf, Shape shape)
            where TCombiner : IContourCombiner<PerpendicularDistanceSelector, float>, new()
        {
            ReadOnlySpan<float> dummy = stackalloc float[4];

            // Compute the expected deltas between values of horizontally, vertically, and diagonally adjacent texels.
            float hSpan = MinDeviationRatio * Transformation.Projection.UnprojectVector(new(Transformation.DistanceMapping[new(1)], 0)).Length();
            float vSpan = MinDeviationRatio * Transformation.Projection.UnprojectVector(new(0, Transformation.DistanceMapping[new(1)])).Length();
            float dSpan = MinDeviationRatio * Transformation.Projection.UnprojectVector(new(Transformation.DistanceMapping[new(1)])).Length();
            {
                var shapeDistanceChecker = new ShapeDistanceChecker<TCombiner>(sdf, shape, Transformation.Projection, Transformation.DistanceMapping, MinImproveRatio);
                bool rightToLeft = false;
                // Inspect all texels.
                // Parallel.For
                for (int y = 0; y < sdf.SubHeight; ++y)
                {
                    int row = shape.InverseYAxis ? sdf.SubHeight - y - 1 : y;
                    for (int col = 0; col < sdf.SubWidth; ++col)
                    {
                        int x = rightToLeft ? sdf.SubWidth - col - 1 : col;
                        if ((Stencil[x, row] & (byte)Flags.Error) != 0)
                            continue;
                        ReadOnlySpan<float> c = sdf.Slice(x, row);
                        shapeDistanceChecker.ShapeCoord = Transformation.Projection.Unproject(new(x + .5f, y + .5f));
                        shapeDistanceChecker.SdfCoord = new Vector2(x + .5f, row + .5f);
                        fixed (float* msd = c)
                        {
                            shapeDistanceChecker.ProtectedFlag = (Stencil[x, row] & (byte)Flags.Protected) != 0;
                            float cm = Arithmetic.Median(c[0], c[1], c[2]);
                            ReadOnlySpan<float> l = dummy, b = dummy, r = dummy, t = dummy;
                            // Mark current texel c with the error flag if an artifact occurs when it's interpolated with any of its 8 neighbors.
                            bool artifact = false;

                            do
                            {
                                if (x > 0)
                                {
                                    l = sdf.Slice(x - 1, row);
                                    if (HasLinearArtifact(shapeDistanceChecker.Classifier(new Vector2(-1, 0), hSpan), cm, c, l))
                                    {
                                        artifact = true;
                                        break;
                                    }
                                }

                                if (row > 0)
                                {
                                    b = sdf.Slice(x, row - 1);
                                    if (HasLinearArtifact(shapeDistanceChecker.Classifier(new Vector2(0, -1), vSpan), cm, c, b))
                                    {
                                        artifact = true;
                                        break;
                                    }
                                }

                                if (x < sdf.SubWidth - 1)
                                {
                                    r = sdf.Slice(x + 1, row);
                                    if (HasLinearArtifact(shapeDistanceChecker.Classifier(new Vector2(+1, 0), hSpan), cm, c, r))
                                    {
                                        artifact = true;
                                        break;
                                    }
                                }

                                if (row < sdf.SubHeight - 1)
                                {
                                    t = sdf.Slice(x, row + 1);
                                    if (HasLinearArtifact(shapeDistanceChecker.Classifier(new Vector2(0, +1), vSpan), cm, c, t))
                                    {
                                        artifact = true;
                                        break;
                                    }
                                }

                                if (x > 0 && row > 0 && HasDiagonalArtifact(shapeDistanceChecker.Classifier(new Vector2(-1, -1), dSpan), cm, c, l, b, sdf.Slice(x - 1, row - 1)))
                                {
                                    artifact = true;
                                    break;
                                }

                                if (x < sdf.SubWidth - 1 && row > 0 && HasDiagonalArtifact(shapeDistanceChecker.Classifier(new Vector2(+1, -1), dSpan), cm, c, r, b, sdf.Slice(x + 1, row - 1)))
                                {
                                    artifact = true;
                                    break;
                                }

                                if (x > 0 && row < sdf.SubHeight - 1 && HasDiagonalArtifact(shapeDistanceChecker.Classifier(new Vector2(-1, +1), dSpan), cm, c, l, t, sdf.Slice(x - 1, row + 1)))
                                {
                                    artifact = true;
                                    break;
                                }

                                if (x < sdf.SubWidth - 1 && row < sdf.SubHeight - 1 && HasDiagonalArtifact(shapeDistanceChecker.Classifier(new Vector2(+1, +1), dSpan), cm, c, r, t, sdf.Slice(x + 1, row + 1)))
                                {
                                    artifact = true;
                                    break;
                                }

                            } while (false);

                            if (artifact)
                                Stencil[x, row] |= (byte)Flags.Error;
                            
                        }
                    }
                }
            }
        }

        public unsafe void Apply(BitmapRef<float> sdf)
        {
            int texelCount = sdf.SubWidth * sdf.SubHeight;
            fixed (byte* maskFixed = Stencil.Pixels)
            fixed (float* texelFixed = sdf.Pixels)
            {
                byte* mask = maskFixed;
                float* texel = texelFixed;
                for (int i = 0; i < texelCount; ++i)
                {
                    if ((*mask & (byte)Flags.Error) != 0)
                    {
                        // Set all color channels to the median.
                        float m = Arithmetic.Median(texel[0], texel[1], texel[2]);
                        texel[0] = m; texel[1] = m; texel[2] = m;
                    }
                    ++mask;
                    texel += sdf.N;
                }
            }
        }

        /// <summary>
        /// Returns the stencil in its current state (see Flags).
        /// </summary>
        /// <returns></returns>
        public BitmapConstRef<byte> GetStencil() => Stencil;

        static void CorrectionInner<TCombiner>(BitmapRef<float> sdf, Shape shape, SDFTransformation transformation, MSDFGeneratorConfig config)
            where TCombiner : IContourCombiner<PerpendicularDistanceSelector, float>, new()
        {
            if (config.ErrorCorrection.Mode == ErrorCorrectionConfig.OpMode.DISABLED)
                return;
            Bitmap<byte> stencilBuffer;
            if (config.ErrorCorrection.Buffer == null)
                stencilBuffer = new Bitmap<byte>(sdf.SubWidth, sdf.SubHeight);
            else
                stencilBuffer = new();

            BitmapRef<byte> stencil;
            stencilBuffer.Pixels = config.ErrorCorrection.Buffer ?? stencilBuffer.Pixels;
            stencilBuffer._Width = sdf.SubWidth; stencilBuffer._Height = sdf.SubHeight;
            stencil = new(stencilBuffer);
            MSDFErrorCorrection ec = new(stencil, transformation);
            ec.SetMinDeviationRatio(config.ErrorCorrection.MinDeviationRatio);
            ec.SetMinImproveRatio(config.ErrorCorrection.MinImproveRatio);
            switch (config.ErrorCorrection.Mode)
            {
                case ErrorCorrectionConfig.OpMode.DISABLED:
                case ErrorCorrectionConfig.OpMode.INDISCRIMINATE:
                    break;
                case ErrorCorrectionConfig.OpMode.EDGE_PRIORITY:
                    ec.ProtectCorners(shape);
                    ec.ProtectEdges(sdf);
                    break;
                case ErrorCorrectionConfig.OpMode.EDGE_ONLY:
                    ec.ProtectAll();
                    break;
            }
            if (config.ErrorCorrection.DistanceCheckMode == ErrorCorrectionConfig.ConfigDistanceCheckMode.DO_NOT_CHECK_DISTANCE
                || (config.ErrorCorrection.DistanceCheckMode == ErrorCorrectionConfig.ConfigDistanceCheckMode.CHECK_DISTANCE_AT_EDGE
                    && config.ErrorCorrection.Mode != ErrorCorrectionConfig.OpMode.EDGE_ONLY))
            {
                ec.FindErrors<TCombiner>(sdf);
                if (config.ErrorCorrection.DistanceCheckMode == ErrorCorrectionConfig.ConfigDistanceCheckMode.CHECK_DISTANCE_AT_EDGE)
                    ec.ProtectAll();
            }
            if (config.ErrorCorrection.DistanceCheckMode == ErrorCorrectionConfig.ConfigDistanceCheckMode.ALWAYS_CHECK_DISTANCE || config.ErrorCorrection.DistanceCheckMode == ErrorCorrectionConfig.ConfigDistanceCheckMode.CHECK_DISTANCE_AT_EDGE)
            {
                if (config.OverlapSupport)
                    ec.FindErrors<OverlappingContourCombiner<PerpendicularDistanceSelector, float>>(sdf, shape);
                else
                    ec.FindErrors<SimpleContourCombiner<PerpendicularDistanceSelector, float>>(sdf, shape);
            }
            ec.Apply(sdf);
        }


        public static void ErrorCorrection<TCombiner>(BitmapRef<float> sdf, Shape shape, SDFTransformation transformation, MSDFGeneratorConfig config)
            where TCombiner : IContourCombiner<PerpendicularDistanceSelector, float>, new()
        {
            CorrectionInner<TCombiner>(sdf, shape, transformation, config);
        }

    }
}