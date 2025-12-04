using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MapToggle: Wird auf jedem Toggle für eine Map-Auswahl verwendet
/// Speichert nur die mapReference, die vom SelectMapButton ausgelesen wird
/// </summary>
public class LoadMapButton : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private string mapReference = "";
    [SerializeField] private bool debugLogs = true;

    /// <summary>
    /// Gibt die Map-Referenz zurück
    /// </summary>
    public string GetMapReference()
    {
        return mapReference;
    }

    /// <summary>
    /// Setzt die Map-Referenz zur Laufzeit
    /// </summary>
    public void SetMapReference(string reference)
    {
        mapReference = reference;
        if (debugLogs) Debug.Log($"[LoadMapButton] Map reference set to: {mapReference}");
    }
}
