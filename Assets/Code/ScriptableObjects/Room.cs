using UnityEngine;
 
[CreateAssetMenu(menuName = "ChangeDetection/Room", fileName = "Room")]
public class Room : ScriptableObject
{
    public string reference;
    public GameObject roomMesh;
}