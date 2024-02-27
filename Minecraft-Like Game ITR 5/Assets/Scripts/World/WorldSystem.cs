using Unity.Collections;
using Unity.Entities;
using UnityEditor;

namespace PatataStudio
{
	public partial struct WorldSystem : ISystem
	{
		private NativeList<NoiseData> noiseDatas;

		private void GetHeightMapDataSO()
		{
			var assets = AssetDatabase.FindAssets("t:" + nameof(HeightMapData), new[] { "Assets/Scripts/World/TerrainGeneration/NoiseData" });
			foreach (var asset in assets)
			{
				var SOpath = AssetDatabase.GUIDToAssetPath(asset);
				var mapData = AssetDatabase.LoadAssetAtPath<HeightMapData>(SOpath);
				noiseDatas.Add(mapData.Noise);
			}
		}
	}
}