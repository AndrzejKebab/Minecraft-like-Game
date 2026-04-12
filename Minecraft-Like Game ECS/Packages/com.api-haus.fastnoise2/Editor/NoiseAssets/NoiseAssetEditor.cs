using System.Linq;
using FastNoise2.Authoring.NoiseAsset;
using UnityEditor;
using UnityEngine;

namespace FastNoise2.Editor.NoiseAssets
{
	[CustomEditor(typeof(BakedNoiseTextureAsset))]
	public class NoiseAssetEditor : UnityEditor.Editor
	{
		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			if (GUILayout.Button("Bake"))
			{
				System.Collections.Generic.IEnumerable<BakedNoiseTextureAsset> targetAssets =
					targets.OfType<BakedNoiseTextureAsset>();

				foreach (BakedNoiseTextureAsset noiseAsset in targetAssets)
				{
					string assetPath = AssetDatabase.GetAssetPath(noiseAsset);
					noiseAsset.BakeIntoAsset(assetPath);
				}

				AssetDatabase.Refresh();
			}
		}
	}
}
