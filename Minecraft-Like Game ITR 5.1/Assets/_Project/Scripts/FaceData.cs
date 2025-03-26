using System;
using System.Text;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct FaceData
{
	public   Vertex[]   Vertices;
	[field:SerializeField] public float3 Normal;

	public override string ToString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (var vertex in Vertices)
		{
			stringBuilder.AppendLine(vertex.ToString());
		}

		stringBuilder.AppendLine("Normal: " + Normal.ToString());

		return stringBuilder.ToString();
	}
}