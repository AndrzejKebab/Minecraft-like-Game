using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace _Project.UI
{
	[RequireComponent(typeof(PanelRenderer))]
	public class PlayerUIController : MonoBehaviour
	{
		private string currentBlockName = "None";

		private       PanelRenderer      panelRenderer;
		private       Label              selectedBlockLabel;
		private       int                lastReloadVersion = -1;
		public static PlayerUIController Instance { get; private set; }

		private void Awake()
		{
			if (Instance == null) Instance = this;
			else Destroy(gameObject);

			panelRenderer = GetComponent<PanelRenderer>();
		}

		// statics survive when domain reload is disabled — reset on play mode entry
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			Instance = null;
		}

		private void OnEnable()
		{
			panelRenderer.RegisterUIReloadCallback(OnUIReload);
		}

		private void OnDisable()
		{
			panelRenderer.UnregisterUIReloadCallback(OnUIReload);
		}

		private void OnUIReload(PanelRenderer renderer, VisualElement rootElement, int version)
		{
			if (version == lastReloadVersion) return; // UI didn't actually change
			lastReloadVersion = version;

			selectedBlockLabel = rootElement.Q<Label>("SelectedBlock");

			if (selectedBlockLabel == null)
				Debug.LogError("Could not find a Label named 'SelectedBlock' in the PanelRenderer UXML!");
			else
				selectedBlockLabel.text =
					new StringBuilder().Append("Selected Block: ").Append(currentBlockName).ToString();
		}

		public void UpdateSelectedBlockText(string blockName)
		{
			currentBlockName = blockName;

			if (selectedBlockLabel != null)
				selectedBlockLabel.text =
					new StringBuilder().Append("Selected Block: ").Append(currentBlockName).ToString();
		}
	}
}