using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SelectButton: checks which toggle in the ToggleGroup is active and loads the corresponding map
/// Then switches the UI: hides the selection menu and shows the map viewer menu
/// </summary>
public class SelectMapButton : MonoBehaviour, IButtonBehaviour
{
    [Header("References")]
    [SerializeField] private ToggleGroup mapToggleGroup;
    [SerializeField] private loadMap mapLoader;
    [SerializeField] private GameObject mapSelectionMenu; // Map selection menu
    [SerializeField] private GameObject mapViewerMenu; // Map viewer menu

    [Header("Settings")]
    [SerializeField] private bool debugLogs = true;

    private Toggle toggleComponent;

    private void OnEnable()
    {
        if (toggleComponent == null)
        {
            toggleComponent = GetComponent<Toggle>();
        }

        if (toggleComponent != null)
        {
            toggleComponent.onValueChanged.AddListener(OnToggleChanged);
            if (debugLogs) Debug.Log($"[SelectMapButton] {gameObject.name} registered toggle listener");
        }
        else
        {
            Debug.LogError($"[SelectMapButton] No Toggle component found on {gameObject.name}");
        }

        // Auto-find if not assigned
        if (mapToggleGroup == null || mapLoader == null || mapSelectionMenu == null || mapViewerMenu == null)
        {
            AutoFindReferences();
        }
    }

    private void OnDisable()
    {
        if (toggleComponent != null)
        {
            toggleComponent.onValueChanged.RemoveListener(OnToggleChanged);
        }
    }

    /// <summary>
    /// Invoked when the toggle state changes
    /// </summary>
    private void OnToggleChanged(bool isOn)
    {
        if (isOn)
        {
            Execute();
        }
    }

    /// <summary>
    /// Called when the select button activates.
    /// Finds the active toggle in the ToggleGroup, loads its map, then switches UI (hide selection, show viewer).
    /// </summary>
    public void Execute()
    {
        if (debugLogs) Debug.Log("[SelectMapButton] Execute called");

        if (mapToggleGroup == null)
        {
            Debug.LogError("[SelectMapButton] mapToggleGroup is null");
            return;
        }

        if (mapLoader == null)
        {
            Debug.LogError("[SelectMapButton] mapLoader is null");
            return;
        }

        // Find the active toggle
        Toggle activeToggle = GetActiveToggle();
        if (activeToggle == null)
        {
            Debug.LogWarning("[SelectMapButton] No active toggle found in ToggleGroup");
            return;
        }

        // Get the mapReference from the active toggle
        LoadMapButton mapToggle = activeToggle.GetComponent<LoadMapButton>();
        if (mapToggle == null)
        {
            Debug.LogError($"[SelectMapButton] Active toggle {activeToggle.name} has no LoadMapButton component");
            return;
        }

        string mapReference = mapToggle.GetMapReference();
        if (string.IsNullOrEmpty(mapReference))
        {
            Debug.LogError($"[SelectMapButton] Active toggle {activeToggle.name} has empty mapReference");
            return;
        }

        // Load the map
        if (debugLogs) Debug.Log($"[SelectMapButton] Loading map: {mapReference} from toggle: {activeToggle.name}");
        
        mapLoader.LoadSelectedMap(mapReference);

        // Switch UI
        SwitchUI();
    }

    /// <summary>
    /// Switches the UI: hides the selection menu and shows the map viewer menu
    /// </summary>
    private void SwitchUI()
    {
        if (debugLogs) Debug.Log("[SelectMapButton] Switching UI");

        // Hide map selection menu
        if (mapSelectionMenu != null)
        {
            mapSelectionMenu.SetActive(false);
            if (debugLogs) Debug.Log("[SelectMapButton] Hidden Map Selection Menu");
        }
        else
        {
            Debug.LogWarning("[SelectMapButton] mapSelectionMenu is null");
        }

        // Show map viewer menu
        if (mapViewerMenu != null)
        {
            mapViewerMenu.SetActive(true);
            if (debugLogs) Debug.Log("[SelectMapButton] Showed Map Viewer Menu");
        }
        else
        {
            Debug.LogWarning("[SelectMapButton] mapViewerMenu is null");
        }
    }

    /// <summary>
    /// Finds the active toggle in the ToggleGroup
    /// </summary>
    private Toggle GetActiveToggle()
    {
        if (mapToggleGroup == null)
            return null;

        // Iterate through all toggles in the ToggleGroup
        foreach (var toggle in mapToggleGroup.ActiveToggles())
        {
            if (toggle.isOn)
            {
                return toggle;
            }
        }

        return null;
    }

    /// <summary>
    /// Auto-finds missing references
    /// </summary>
    private void AutoFindReferences()
    {
        // Find mapLoader
        if (mapLoader == null)
        {
            mapLoader = FindFirstObjectByType<loadMap>();
        }

        // Find ToggleGroup
        if (mapToggleGroup == null)
        {
            var allObjects = FindObjectsByType<ToggleGroup>(FindObjectsSortMode.None);
            foreach (var group in allObjects)
            {
                if (group.name.Contains("MapToggle") || group.name.Contains("MapSelection"))
                {
                    mapToggleGroup = group;
                    break;
                }
            }
        }

        // Find selection and viewer menus
        if (mapSelectionMenu == null || mapViewerMenu == null)
        {
            var allGameObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var obj in allGameObjects)
            {
                if ((obj.name == "MapSelectionMenu" || obj.name.Contains("Selection")) && mapSelectionMenu == null)
                {
                    mapSelectionMenu = obj;
                }
                else if ((obj.name == "MapViewerMenu" || obj.name == "MapDisplayMenu") && mapViewerMenu == null)
                {
                    mapViewerMenu = obj;
                }
            }
        }

        if (debugLogs)
        {
            Debug.Log($"[SelectMapButton] Auto-found: ToggleGroup={mapToggleGroup?.name}, MapLoader={mapLoader?.name}, SelectionMenu={mapSelectionMenu?.name}, ViewerMenu={mapViewerMenu?.name}");
        }
    }
}
