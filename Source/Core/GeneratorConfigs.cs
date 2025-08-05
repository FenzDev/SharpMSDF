namespace SharpMSDF.Core
{

    /// <summary>
    /// The configuration of the MSDF error correction pass.
    /// </summary>
    public struct ErrorCorrectionConfig
    {
        /// <summary>The default value of MinDeviationRatio.</summary>
        public const double DefaultMinDeviationRatio = 1.0; // Replace with actual default from .cpp if known
        /// <summary>The default value of MinImproveRatio.</summary>
        public const double DefaultMinImproveRatio = 1.0; // Replace with actual default from .cpp if known

        /// <summary>Mode of operation.</summary>
        public enum OpMode
        {
            /// Skips error correction pass.
            DISABLED,
            /// Corrects all discontinuities of the distance field regardless if edges are adversely affected.
            INDISCRIMINATE,
            /// Corrects artifacts at edges and other discontinuous distances only if it does not affect edges or corners.
            EDGE_PRIORITY,
            /// Only corrects artifacts at edges.
            EDGE_ONLY
        }

        /// <summary>
        /// Configuration of whether to use an algorithm that computes the exact _shape distance at the positions of suspected artifacts.
        /// </summary>
        public enum ConfigDistanceCheckMode
        {
            /// Never computes exact _shape distance.
            DO_NOT_CHECK_DISTANCE,
            /// Only computes exact _shape distance at edges.
            CHECK_DISTANCE_AT_EDGE,
            /// Computes and compares the exact _shape distance for each suspected artifact.
            ALWAYS_CHECK_DISTANCE
        }

        public OpMode Mode = OpMode.EDGE_PRIORITY;
        public ConfigDistanceCheckMode DistanceCheckMode = ConfigDistanceCheckMode.CHECK_DISTANCE_AT_EDGE;
        public double MinDeviationRatio = DefaultMinDeviationRatio;
        public double MinImproveRatio = DefaultMinImproveRatio;

        /// <summary>
        /// An optional Buffer to avoid dynamic allocation. Must have at least as many bytes as the MSDF has pixels.
        /// </summary>
        public byte[] Buffer;

        public ErrorCorrectionConfig(
            OpMode mode = OpMode.EDGE_PRIORITY,
            ConfigDistanceCheckMode distanceCheckMode = ConfigDistanceCheckMode.CHECK_DISTANCE_AT_EDGE,
            double minDeviationRatio = DefaultMinDeviationRatio,
            double minImproveRatio = DefaultMinImproveRatio,
            byte[] buffer = null)
        {
            Mode = mode;
            DistanceCheckMode = distanceCheckMode;
            MinDeviationRatio = minDeviationRatio;
            MinImproveRatio = minImproveRatio;
            Buffer = buffer;
        }
    }

    public interface IGeneratorConfig
    {
        /// <summary>
        /// Specifies whether to use the version of the algorithm that supports overlapping contours with the same winding.
        /// </summary>
        public bool OverlapSupport { get; set; }

    }


    /// <summary>
    /// The configuration of the distance field generator algorithm.
    /// </summary>
    public struct GeneratorConfig : IGeneratorConfig
    {
        /// <inheritdoc/>
        public bool OverlapSupport { get; set; }

        public GeneratorConfig(bool overlapSupport = true)
        {
            OverlapSupport = overlapSupport;
        }

        public static implicit operator GeneratorConfig(MSDFGeneratorConfig cfg) => new GeneratorConfig(cfg.OverlapSupport);
    }

    /// <summary>
    /// The configuration of the multi-channel distance field generator algorithm.
    /// </summary>
    public struct MSDFGeneratorConfig : IGeneratorConfig
    {
        /// <inheritdoc/>
        public bool OverlapSupport { get; set; }
        /// <summary>
        /// Configuration of the error correction pass.
        /// </summary>
        public ErrorCorrectionConfig ErrorCorrection;

        public MSDFGeneratorConfig()
        {
            ErrorCorrection = new ErrorCorrectionConfig();
        }

        public unsafe MSDFGeneratorConfig(bool overlapSupport, ErrorCorrectionConfig? errorCorrection = null)
        {
            OverlapSupport = overlapSupport;
            ErrorCorrection = errorCorrection ?? new ErrorCorrectionConfig();
        }
    }
}
