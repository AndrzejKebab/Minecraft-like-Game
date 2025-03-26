using UnityEngine;

[CreateAssetMenu(menuName = "VoxelGame/Block Type", fileName = "NewBlockType")]
public class BlockType : ScriptableObject
{
    public string Name;
    public byte   MeshID;
    public byte   TextureID;
    //TODO: Add support for textures somehow
}
