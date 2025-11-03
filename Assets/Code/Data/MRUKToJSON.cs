using System.IO;
using UnityEngine;
using Meta.XR.MRUtilityKit;
using System.Collections.Generic;
using System;

public class MRUKToJSON : MonoBehaviour
{
    public string fileName = "scene_export_full.json";

    void Start()
    {
        var mruk = MRUK.Instance;
        // If MRUK is already ready, export immediately; otherwise, wait for the event.
        if (mruk.IsInitialized)
        {
            OnSceneReady();
            Debug.Log("MRUK is already initialized, exporting immediately.");
        }    
        else
        {
            Debug.Log("Waiting for MRUK to initialize before exporting.");
            mruk.SceneLoadedEvent.AddListener(OnSceneReady);
        }
    }

    public void OnSceneReady()
    {
        var mruk = MRUK.Instance;
        // Exclude global mesh if you only need anchors/semantics.
        List<MRUKRoom> rooms = mruk.Rooms;
        string json = mruk.SaveSceneToJsonString(includeGlobalMesh: false, rooms);
        fileName += "_" +DateTime.UtcNow.AddHours(1.0).ToString("yyyyMMdd_HHmmss");
        var path = Path.Combine(Application.persistentDataPath, fileName);

        File.WriteAllText(path, json);
        Debug.Log($"Exported MRUK scene JSON to: {path}");
    }
}
