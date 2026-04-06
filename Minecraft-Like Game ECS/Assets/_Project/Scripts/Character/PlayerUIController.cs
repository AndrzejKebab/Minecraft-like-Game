using UnityEngine;
using UnityEngine.UIElements;

namespace _Project.UI
{
    [RequireComponent(typeof(PanelRenderer))]
    public class PlayerUIController : MonoBehaviour
    {
        public static PlayerUIController Instance { get; set; }
        
        private PanelRenderer panelRenderer;
        private Label selectedBlockLabel;
        
        // Cache the name so if the UI reloads mid-game, it restores the correct text
        private string currentBlockName = "None"; 

        private void Awake()
        {
            // Simple singleton for our ECS system to access easily
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            panelRenderer = GetComponent<PanelRenderer>();
        }

        private void OnEnable()
        {
            // Unity 6 Standard: Register the reload callback
            panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }

        private void OnDisable()
        {
            panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        }

        // Called automatically when the PanelRenderer fully builds the UI tree
        private void OnUIReload(PanelRenderer renderer, VisualElement rootElement)
        {
            // Query the UI tree for our specific label
            selectedBlockLabel = rootElement.Q<Label>("SelectedBlock");
            
            if (selectedBlockLabel == null)
            {
                Debug.LogError("Could not find a Label named 'SelectedBlock' in the PanelRenderer UXML!");
            }
            else
            {
                // Ensure the label displays the correct text upon loading
                selectedBlockLabel.text = $"Selected Block: {currentBlockName}";
            }
        }

        public void UpdateSelectedBlockText(string blockName)
        {
            currentBlockName = blockName;

            // Only update the label if the UI has actually finished loading
            if (selectedBlockLabel != null)
            {
                selectedBlockLabel.text = $"Selected Block: {currentBlockName}";
            }
        }
    }
}