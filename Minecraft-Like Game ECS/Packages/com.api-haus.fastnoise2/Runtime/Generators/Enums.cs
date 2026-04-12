namespace FastNoise2.Generators
{
	public enum DistanceFunction
	{
		Euclidean,
		EuclideanSquared,
		Manhattan,
		Hybrid,
		MaxAxis,
		Minkowski,
	}

	public enum CellularReturnType
	{
		Index0,
		Index0Add1,
		Index0Sub1,
		Index0Mul1,
		Index0Div1,
	}

	public enum FadeInterpolation
	{
		Linear,
		Hermite,
		Quintic,
	}

	public enum Dimension
	{
		X,
		Y,
		Z,
		W,
	}

	public enum PlaneRotationType
	{
		ImproveXYPlanes,
		ImproveXZPlanes,
	}

	public enum VectorizationScheme
	{
		OrthogonalGradientMatrix,
		GradientOuterProduct,
	}

	internal static class EnumStrings
	{
		internal static string ToMetadataString(this DistanceFunction value) => value switch
		{
			DistanceFunction.EuclideanSquared => "EuclideanSquared",
			_ => value.ToString(),
		};

		internal static string ToMetadataString(this CellularReturnType value) => value.ToString();

		internal static string ToMetadataString(this FadeInterpolation value) => value.ToString();

		internal static string ToMetadataString(this Dimension value) => value.ToString();

		internal static string ToMetadataString(this PlaneRotationType value) => value switch
		{
			PlaneRotationType.ImproveXYPlanes => "ImproveXYPlanes",
			PlaneRotationType.ImproveXZPlanes => "ImproveXZPlanes",
			_ => value.ToString(),
		};

		internal static string ToMetadataString(this VectorizationScheme value) => value switch
		{
			VectorizationScheme.OrthogonalGradientMatrix => "OrthogonalGradientMatrix",
			VectorizationScheme.GradientOuterProduct => "GradientOuterProduct",
			_ => value.ToString(),
		};
	}
}
