using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SelectButton: Prüft welcher Toggle in der ToggleGroup aktiv ist und lädt die entsprechende Map
/// Schaltet dann die UI um: versteckt Selection-Menü, zeigt Map-Viewer-Menü
/// </summary>
public class SelectMapButton : MonoBehaviour, IButtonBehaviour
{
    [Header("References")]
    [SerializeField] private ToggleGroup mapToggleGroup;
    [SerializeField] private loadMap mapLoader;
    [SerializeField] private GameObject mapSelectionMenu; // Map-Auswahl Menü
    [SerializeField] private GameObject mapViewerMenu; // Map-Viewer Menü

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

        // Auto-find wenn nicht zugewiesen
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
    /// Wird aufgerufen wenn der Toggle geändert wird
    /// </summary>
    private void OnToggleChanged(bool isOn)
    {
        if (isOn)
        {
            Execute();
        }
    }

    /// <summary>
    /// Wird aufgerufen wenn der SelectButton aktiviert wird
    /// Findet den aktiven Toggle in der ToggleGroup und lädt dessen Map
    /// Schaltet dann die UI um (versteckt Selection, zeigt Map-Viewer)
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

        // Finde den aktiven Toggle
        Toggle activeToggle = GetActiveToggle();
        if (activeToggle == null)
        {
            Debug.LogWarning("[SelectMapButton] No active toggle found in ToggleGroup");
            return;
        }

        // Hole die mapReference vom aktiven Toggle
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

        // Lade die Map
        if (debugLogs) Debug.Log($"[SelectMapButton] Loading map: {mapReference} from toggle: {activeToggle.name}");
        
        mapLoader.LoadSelectedMap(mapReference);

        // Schalte UI um
        SwitchUI();
    }

    /// <summary>
    /// Schaltet die UI um: versteckt Selection-Menü, zeigt Map-Viewer-Menü
    /// </summary>
    private void SwitchUI()
    {
        if (debugLogs) Debug.Log("[SelectMapButton] Switching UI");

        // Verstecke Map-Auswahl Menü
        if (mapSelectionMenu != null)
        {
            mapSelectionMenu.SetActive(false);
            if (debugLogs) Debug.Log("[SelectMapButton] Hidden Map Selection Menu");
        }
        else
        {
            Debug.LogWarning("[SelectMapButton] mapSelectionMenu is null");
        }

        // Zeige Map-Viewer Menü
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
    /// Findet den aktiven Toggle in der ToggleGroup
    /// </summary>
    private Toggle GetActiveToggle()
    {
        if (mapToggleGroup == null)
            return null;

        // Iteriere durch alle Toggles in der ToggleGroup
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
    /// Auto-Finder für Referenzen
    /// </summary>
    private void AutoFindReferences()
    {
        // Finde mapLoader
        if (mapLoader == null)
        {
            mapLoader = FindFirstObjectByType<loadMap>();
        }

        // Finde ToggleGroup
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

        // Finde Selection und Viewer Menüs
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
