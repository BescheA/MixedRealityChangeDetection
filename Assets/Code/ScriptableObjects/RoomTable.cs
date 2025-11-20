using UnityEngine;
[CreateAssetMenu(menuName = "ChangeDetection/RoomTable", fileName = "RoomTable")]
public class RoomTable : ScriptableObject
{
    public Room[] rooms;

    public Room GetRoomByReference(string reference)
    {
        foreach (var room in rooms)
        {
            if (room.reference == reference)
            {
                return room;
            }
        }
        return null;
    }
}