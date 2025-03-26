using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

[Serializable, StructLayout(LayoutKind.Sequential)]
public struct Vertex
{
	public float3 Position;
	public float2 UV;

	public override string ToString()
	{
		return $"Vertex: {Position}, UV: {UV}";
	}
}