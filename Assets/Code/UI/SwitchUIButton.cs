using UnityEngine;

public class SwitchUIButton : MonoBehaviour, IButtonBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject uiToEnable;
    [SerializeField] private GameObject uiToDisable;

    [Header("Settings")]
    [SerializeField] private bool debugLogs = true;

    public void Execute()
    {
        if (uiToEnable != null)
        {
            uiToEnable.SetActive(true);
            if (debugLogs) Debug.Log($"[SwitchUIButton] Enabled UI: {uiToEnable.name}");
        }
        else
        {
            Debug.LogError("[SwitchUIButton] uiToEnable is not assigned.");
        }

        if (uiToDisable != null)
        {
            uiToDisable.SetActive(false);
            if (debugLogs) Debug.Log($"[SwitchUIButton] Disabled UI: {uiToDisable.name}");
        }
        else
        {
            Debug.LogError("[SwitchUIButton] uiToDisable is not assigned.");
        }
    }
}