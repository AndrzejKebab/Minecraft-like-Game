using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;
using _Project.Tags;

namespace _Project.UI
{
	/// <summary>
	///     Top-centre HUD (UI Toolkit) showing the player's world coordinates and the
	///     chunk they are standing in. It reads the Player entity's LocalTransform from
	///     the default ECS world each frame and writes it into a Label built in code.
	///
	///     Usage: drop this component on any GameObject in the scene. A UIDocument is
	///     added automatically. Assign a PanelSettings asset in the inspector for best
	///     results (Create ▸ UI Toolkit ▸ Panel Settings Asset); if none is assigned one
	///     is created at runtime using any theme already imported in the project.
	/// </summary>
	[RequireComponent(typeof(UIDocument))]
	public class CoordsHudController : MonoBehaviour
	{
		[Tooltip("Optional. If empty, a PanelSettings is created at runtime from any theme found in the project.")]
		public PanelSettings PanelSettings;

		private const int ChunkSize = 32;

		private Label         _label;
		private EntityManager _em;
		private EntityQuery   _playerQuery;
		private bool          _queryReady;

		private void Start()
		{
			var doc = GetComponent<UIDocument>();

			if (PanelSettings == null) PanelSettings = CreateRuntimePanelSettings();
			doc.panelSettings = PanelSettings;

			BuildUI(doc);
		}

		private void BuildUI(UIDocument doc)
		{
			VisualElement root = doc.rootVisualElement;
			if (root == null) return;
			root.Clear();
			root.style.flexGrow = 1f;
			root.pickingMode     = PickingMode.Ignore;

			// full-width bar pinned to the top, contents centred horizontally
			var bar = new VisualElement
			          {
				          pickingMode = PickingMode.Ignore
			          };
			bar.style.position    = Position.Absolute;
			bar.style.top         = 8f;
			bar.style.left        = 0f;
			bar.style.right       = 0f;
			bar.style.alignItems  = Align.Center;

			_label = new Label("XYZ: —   Chunk: —")
			         {
				         pickingMode = PickingMode.Ignore
			         };
			_label.style.color                    = Color.white;
			_label.style.fontSize                 = 16f;
			_label.style.unityFontStyleAndWeight  = FontStyle.Bold;
			_label.style.unityTextAlign           = TextAnchor.MiddleCenter;
			_label.style.paddingLeft              = 12f;
			_label.style.paddingRight             = 12f;
			_label.style.paddingTop               = 4f;
			_label.style.paddingBottom            = 4f;
			_label.style.backgroundColor          = new Color(0f, 0f, 0f, 0.55f);
			_label.style.borderTopLeftRadius      = 6f;
			_label.style.borderTopRightRadius     = 6f;
			_label.style.borderBottomLeftRadius   = 6f;
			_label.style.borderBottomRightRadius  = 6f;

			bar.Add(_label);
			root.Add(bar);
		}

		private void Update()
		{
			if (_label == null) return;

			if (!EnsureQuery())
			{
				_label.text = "XYZ: (world loading)";
				return;
			}

			if (_playerQuery.CalculateEntityCount() == 0)
			{
				_label.text = "XYZ: (waiting for player)";
				return;
			}

			var players = _playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
			var pos     = _em.GetComponentData<LocalTransform>(players[0]).Position;
			players.Dispose();

			int cx = FloorDiv((int)math.floor(pos.x), ChunkSize);
			int cy = FloorDiv((int)math.floor(pos.y), ChunkSize);
			int cz = FloorDiv((int)math.floor(pos.z), ChunkSize);

			_label.text = $"XYZ  {pos.x:0.0}, {pos.y:0.0}, {pos.z:0.0}      Chunk  {cx}, {cy}, {cz}";
		}

		private bool EnsureQuery()
		{
			if (_queryReady) return true;

			World world = World.DefaultGameObjectInjectionWorld;
			if (world == null || !world.IsCreated) return false;

			_em          = world.EntityManager;
			_playerQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<Player>(),
			                                     ComponentType.ReadOnly<LocalTransform>());
			_queryReady  = true;
			return true;
		}

		// floor division so chunk coords are correct for negative world positions
		private static int FloorDiv(int a, int b)
		{
			int q = a / b;
			if (a % b != 0 && (a < 0) != (b < 0)) q--;
			return q;
		}

		private static PanelSettings CreateRuntimePanelSettings()
		{
			var ps = ScriptableObject.CreateInstance<PanelSettings>();
			ps.name = "CoordsHud PanelSettings (runtime)";

			ThemeStyleSheet[] themes = Resources.FindObjectsOfTypeAll<ThemeStyleSheet>();
			if (themes != null && themes.Length > 0)
				ps.themeStyleSheet = themes[0];
			else
				Debug.LogWarning("[CoordsHud] No UI Toolkit theme found — assign a PanelSettings asset " +
				                 "(Create ▸ UI Toolkit ▸ Panel Settings Asset) on the CoordsHudController so the HUD renders.");

			return ps;
		}
	}
}
