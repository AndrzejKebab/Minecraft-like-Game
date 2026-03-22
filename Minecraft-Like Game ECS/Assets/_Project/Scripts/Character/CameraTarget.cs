using System;
using Unity.Entities;

[Serializable]
public struct CameraTarget : IComponentData
{
    public Entity TargetEntity;
}
