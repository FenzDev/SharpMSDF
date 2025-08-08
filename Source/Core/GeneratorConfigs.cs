namespace SharpMSDF.Core
{

	/// <summary>
	/// The configuration of the MSDF error correction pass.
	/// </summary>
	public struct ErrorCorrectionConfig
	{
		public readonly static ErrorCorrectionConfig Default = new ErrorCorrectionConfig(

			OpMode.EDGE_PRIORITY,
			ConfigDistanceCheckMode.CHECK_DISTANCE_AT_EDGE,
			DefaultMinDeviationRatio,
			DefaultMinImproveRatio
		);

		/// <summary>The default value of MinDeviationRatio.</summary>
		public const float DefaultMinDeviationRatio = 1.1111111111111112f;
		/// <summary>The default value of MinImproveRatio.</summary>
		public const float DefaultMinImproveRatio = 1.1111111111111112f;

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
		public float MinDeviationRatio = DefaultMinDeviationRatio;
		public float MinImproveRatio = DefaultMinImproveRatio;

		/// <summary>
		/// An optional Buffer to avoid dynamic allocation. Must have at least as many bytes as the MSDF has pixels.
		/// </summary>
		public byte[] Buffer;

		public ErrorCorrectionConfig(
			OpMode mode = OpMode.EDGE_PRIORITY,
			ConfigDistanceCheckMode distanceCheckMode = ConfigDistanceCheckMode.CHECK_DISTANCE_AT_EDGE,
			float minDeviationRatio = DefaultMinDeviationRatio,
			float minImproveRatio = DefaultMinImproveRatio,
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
		public static MSDFGeneratorConfig Default => new MSDFGeneratorConfig(true);

		/// <inheritdoc/>
		public bool OverlapSupport { get; set; }
		/// <summary>
		/// Configuration of the error correction pass.
		/// </summary>
		public ErrorCorrectionConfig ErrorCorrection;

		public MSDFGeneratorConfig()
		{
			OverlapSupport = true;
			ErrorCorrection = new ErrorCorrectionConfig();
		}

		public unsafe MSDFGeneratorConfig(bool overlapSupport = true, ErrorCorrectionConfig? errorCorrection = null)
		{
			OverlapSupport = overlapSupport;
			ErrorCorrection = errorCorrection ?? ErrorCorrectionConfig.Default;
		}
	}
}
