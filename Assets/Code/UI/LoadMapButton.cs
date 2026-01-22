using UnityEngine;

public class LoadMapButton : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private string mapReference = "";
    [SerializeField] private bool debugLogs = true;

    public string GetMapReference()
    {
        return mapReference;
    }

    public void SetMapReference(string reference)
    {
        mapReference = reference;
        if (debugLogs) Debug.Log($"[LoadMapButton] Map reference set to: {mapReference}");
    }
}
