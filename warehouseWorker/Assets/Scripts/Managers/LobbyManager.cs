using Mirror;
using UnityEngine;

public class LobbyManager : NetworkRoomManager
{

}

// network message as class to inherit from as to update every client
public struct PosMessage : NetworkMessage
{
    public Vector2 vector2;
}