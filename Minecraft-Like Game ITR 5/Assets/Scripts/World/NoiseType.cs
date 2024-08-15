using System;
using Unity.Collections;
using UnityEngine;

namespace PatataGames
{
	[CreateAssetMenu(menuName = "Minecraft/Noise Type", fileName = "New Noise Type")]
	public class NoiseType : ScriptableObject
	{
		public string EncodedNoiseNodeTree;
		public AnimationCurve NoiseCurve;
		[Range(0f, 5000f)]
		public float Scale = 1000f;
		public NoiseData NoiseData;

		private void AssignData()
		{
			NoiseData = new NoiseData(NoiseCurve, Scale);
		}
	}

	[Serializable]
	public struct NoiseData
	{
		public NativeCurve NoiseCurve { get; private set; }
		public float Scale { get; private set; }

		public NoiseData(AnimationCurve curve, float scale)
		{
			NoiseCurve = curve.ToNative(Allocator.Persistent);
			Scale = scale;
		}

		public void Dispose()
		{
			if(NoiseCurve.IsCreated) NoiseCurve.Dispose();
		}
	}
}