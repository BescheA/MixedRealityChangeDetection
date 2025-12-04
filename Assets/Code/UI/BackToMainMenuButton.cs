using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BackToMainMenuButton: Zerstört das geladene Map-GameObject und resettet die Toggles
/// Schaltet UI zurück: versteckt Map-Viewer, zeigt Selection-Menü
/// </summary>
public class BackToMainMenuButton : MonoBehaviour, IButtonBehaviour
{
    [Header("References")]
    [SerializeField] private ToggleGroup mapToggleGroup;
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
            if (debugLogs) Debug.Log($"[BackToMainMenuButton] {gameObject.name} registered toggle listener");
        }
        else
        {
            Debug.LogError($"[BackToMainMenuButton] No Toggle component found on {gameObject.name}");
        }

        // Auto-find ToggleGroup wenn nicht zugewiesen
        if (mapToggleGroup == null || mapSelectionMenu == null || mapViewerMenu == null)
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
    /// Wird aufgerufen wenn der BackButton aktiviert wird
    /// Zerstört ScansContainer, resettet Toggles, und schaltet UI zurück
    /// </summary>
    public void Execute()
    {
        if (debugLogs) Debug.Log("[BackToMainMenuButton] Execute called");

        // Zerstöre das geladene Map-GameObject (ScansContainer)
        DestroyScansContainer();

        // Resettte alle Toggles in der ToggleGroup
        ResetToggles();

        // Schalte UI zurück
        SwitchUIBack();
    }

    /// <summary>
    /// Schaltet die UI zurück: versteckt Map-Viewer, zeigt Selection-Menü
    /// </summary>
    private void SwitchUIBack()
    {
        if (debugLogs) Debug.Log("[BackToMainMenuButton] Switching UI back");

        // Verstecke Map-Viewer Menü
        if (mapViewerMenu != null)
        {
            mapViewerMenu.SetActive(false);
            if (debugLogs) Debug.Log("[BackToMainMenuButton] Hidden Map Viewer Menu");
        }
        else
        {
            Debug.LogWarning("[BackToMainMenuButton] mapViewerMenu is null");
        }

        // Zeige Map-Auswahl Menü
        if (mapSelectionMenu != null)
        {
            mapSelectionMenu.SetActive(true);
            if (debugLogs) Debug.Log("[BackToMainMenuButton] Showed Map Selection Menu");
        }
        else
        {
            Debug.LogWarning("[BackToMainMenuButton] mapSelectionMenu is null");
        }
    }

    /// <summary>
    /// Zerstört den ScansContainer wenn vorhanden
    /// </summary>
    private void DestroyScansContainer()
    {
        var allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var obj in allObjects)
        {
            if (obj.name.StartsWith("ScansContainer_"))
            {
                if (debugLogs) Debug.Log($"[BackToMainMenuButton] Destroying {obj.name}");
                Destroy(obj);
            }
        }
    }

    /// <summary>
    /// Resettet alle Toggles in der ToggleGroup
    /// </summary>
    private void ResetToggles()
    {
        if (mapToggleGroup == null)
        {
            Debug.LogWarning("[BackToMainMenuButton] mapToggleGroup is null, cannot reset toggles");
            return;
        }

        // Setze allowSwitchOff temporär auf true um alle Toggles zu deaktivieren
        bool originalAllowSwitchOff = mapToggleGroup.allowSwitchOff;
        mapToggleGroup.allowSwitchOff = true;

        foreach (var toggle in mapToggleGroup.GetComponentsInChildren<Toggle>())
        {
            toggle.isOn = false;
            if (debugLogs) Debug.Log($"[BackToMainMenuButton] Reset toggle: {toggle.name}");
        }

        // Stelle ursprünglichen Wert wieder her
        mapToggleGroup.allowSwitchOff = originalAllowSwitchOff;

        if (debugLogs) Debug.Log("[BackToMainMenuButton] All toggles reset");
    }

    /// <summary>
    /// Auto-Finder für Referenzen
    /// </summary>
    private void AutoFindReferences()
    {
        // Finde ToggleGroup
        if (mapToggleGroup == null)
        {
            var allGroups = FindObjectsByType<ToggleGroup>(FindObjectsSortMode.None);
            foreach (var group in allGroups)
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
            Debug.Log($"[BackToMainMenuButton] Auto-found: ToggleGroup={mapToggleGroup?.name}, SelectionMenu={mapSelectionMenu?.name}, ViewerMenu={mapViewerMenu?.name}");
        }
    }
}
