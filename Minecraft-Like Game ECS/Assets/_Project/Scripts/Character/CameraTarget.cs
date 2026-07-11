using System;
using Unity.Entities;

[Serializable]
public struct CameraTarget : IComponentData
{
	[NonSerialized] public Entity TargetEntity;
}