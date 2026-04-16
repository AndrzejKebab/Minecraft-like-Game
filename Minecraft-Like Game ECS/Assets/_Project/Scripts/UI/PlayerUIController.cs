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
		public static PlayerUIController Instance { get; private set; }

		private void Awake()
		{
			if (Instance == null) Instance = this;
			else Destroy(gameObject);

			panelRenderer = GetComponent<PanelRenderer>();
		}

		private void OnEnable()
		{
			panelRenderer.RegisterUIReloadCallback(OnUIReload);
		}

		private void OnDisable()
		{
			panelRenderer.UnregisterUIReloadCallback(OnUIReload);
		}

		private void OnUIReload(PanelRenderer renderer, VisualElement rootElement)
		{
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