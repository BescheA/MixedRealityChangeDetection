using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BackToMainMenuButton: destroys the loaded map GameObject and resets the toggles
/// Switches UI back: hides the map viewer and shows the selection menu
/// </summary>
public class BackToMainMenuButton : MonoBehaviour, IButtonBehaviour
{
    [Header("References")]
    [SerializeField] private ToggleGroup mapToggleGroup;
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
            if (debugLogs) Debug.Log($"[BackToMainMenuButton] {gameObject.name} registered toggle listener");
        }
        else
        {
            Debug.LogError($"[BackToMainMenuButton] No Toggle component found on {gameObject.name}");
        }

        // Auto-find ToggleGroup if not assigned
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
    /// Called when the back button activates: destroys the scans container, resets toggles, and switches UI back.
    /// </summary>
    public void Execute()
    {
        if (debugLogs) Debug.Log("[BackToMainMenuButton] Execute called");

        // Destroy the loaded map GameObject (scans container)
        DestroyScansContainer();

        // Reset all toggles in the ToggleGroup
        ResetToggles();

        // Switch UI back
        SwitchUIBack();
    }

    /// <summary>
    /// Switches the UI back: hides the map viewer and shows the selection menu
    /// </summary>
    private void SwitchUIBack()
    {
        if (debugLogs) Debug.Log("[BackToMainMenuButton] Switching UI back");

        // Hide map viewer menu
        if (mapViewerMenu != null)
        {
            mapViewerMenu.SetActive(false);
            if (debugLogs) Debug.Log("[BackToMainMenuButton] Hidden Map Viewer Menu");
        }
        else
        {
            Debug.LogWarning("[BackToMainMenuButton] mapViewerMenu is null");
        }

        // Show map selection menu
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
    /// Destroys the scans container if present
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
    /// Resets all toggles in the ToggleGroup
    /// </summary>
    private void ResetToggles()
    {
        if (mapToggleGroup == null)
        {
            Debug.LogWarning("[BackToMainMenuButton] mapToggleGroup is null, cannot reset toggles");
            return;
        }

        // Temporarily set allowSwitchOff to true to disable all toggles
        bool originalAllowSwitchOff = mapToggleGroup.allowSwitchOff;
        mapToggleGroup.allowSwitchOff = true;

        foreach (var toggle in mapToggleGroup.GetComponentsInChildren<Toggle>())
        {
            toggle.isOn = false;
            if (debugLogs) Debug.Log($"[BackToMainMenuButton] Reset toggle: {toggle.name}");
        }

        // Restore original value
        mapToggleGroup.allowSwitchOff = originalAllowSwitchOff;

        if (debugLogs) Debug.Log("[BackToMainMenuButton] All toggles reset");
    }

    /// <summary>
    /// Auto-finds missing references
    /// </summary>
    private void AutoFindReferences()
    {
        // Find ToggleGroup
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
            Debug.Log($"[BackToMainMenuButton] Auto-found: ToggleGroup={mapToggleGroup?.name}, SelectionMenu={mapSelectionMenu?.name}, ViewerMenu={mapViewerMenu?.name}");
        }
    }
}
