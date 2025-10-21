using UnityEngine;
public class ScanRoomButton : MonoBehaviour
{
    public void OnScanRoomClicked()
    {
        // System UI takes over; app pauses; resumes afterward.
        // TODO: Setup appears but no Model is generated. Why?
        OVRScene.RequestSpaceSetup();   // or OVRSceneManager.RequestSceneCapture() in older APIs
    }
}
