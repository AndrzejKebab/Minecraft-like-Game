using FastNoise2.Authoring.NoiseGraph;
using FastNoise2.Bindings;
using UnityEngine;
using static Unity.Mathematics.math;

namespace FastNoise2.Samples
{
	/// <summary>
	/// Continuously scrolls and renders a noise graph to a texture on a quad mesh.
	/// </summary>
	public class NoiseGraphQuad : MonoBehaviour
	{
		[Header("Noise Source")]
		[SerializeField] FastNoiseGraph noiseGraph;

		[Header("Display")]
		[SerializeField] int resolution = 256;
		[SerializeField] float frequency = 0.02f;
		[SerializeField] int seed = 1337;

		[Header("Scroll")]
		[SerializeField] Vector2 scrollSpeed = new(0.5f, 0.3f);

		FastNoise m_Noise;
		Texture2D m_Texture;
		float[] m_Buffer;
		Color32[] m_Pixels;
		Renderer m_Renderer;

		void OnEnable()
		{
			if (noiseGraph == null)
				return;

			m_Noise = noiseGraph.Instantiate();
			if (!m_Noise.IsCreated)
				return;

			m_Buffer = new float[resolution * resolution];
			m_Pixels = new Color32[resolution * resolution];

			m_Texture = new Texture2D(resolution, resolution, TextureFormat.R8, false)
			{
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Repeat
			};

			m_Renderer = GetComponent<Renderer>();
			if (m_Renderer != null)
				m_Renderer.material.mainTexture = m_Texture;
		}

		void OnDisable()
		{
			if (m_Noise.IsCreated)
				m_Noise.Dispose();

			if (m_Texture != null)
				Destroy(m_Texture);

			m_Noise = default;
			m_Texture = null;
		}

		void Update()
		{
			if (!m_Noise.IsCreated || m_Texture == null)
				return;

			float t = Time.time;
			float xOff = t * scrollSpeed.x;
			float yOff = t * scrollSpeed.y;

			m_Noise.GenUniformGrid2D(m_Buffer, xOff, yOff,
				resolution, resolution, frequency, frequency, seed);

			for (int i = 0; i < m_Buffer.Length; i++)
			{
				byte v = (byte)(saturate(m_Buffer[i] * 0.5f + 0.5f) * 255f);
				m_Pixels[i] = new Color32(v, v, v, 255);
			}

			m_Texture.SetPixels32(m_Pixels);
			m_Texture.Apply(false);
		}
	}
}
