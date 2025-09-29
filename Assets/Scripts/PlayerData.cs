using Unity.Collections;
using Unity.Netcode;
using System;

public struct PlayerData : INetworkSerializable, IEquatable<PlayerData>
{
    public ulong ClientId;
    public FixedString64Bytes Username;
    public bool IsReady;

    public int BodyIndex;
    public int EyesIndex;
    public int GlovesIndex;
    public PlayerData(ulong clientId, string username, bool isReady = false)
    {
        ClientId = clientId;
        Username = new FixedString64Bytes(username);
        IsReady = isReady;

        BodyIndex = 0;
        EyesIndex = 0;
        GlovesIndex = 0;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref Username);
        serializer.SerializeValue(ref IsReady);

        // ▼▼▼ SERIALIZAR NUEVOS DATOS ▼▼▼
        serializer.SerializeValue(ref BodyIndex);
        serializer.SerializeValue(ref EyesIndex);
        serializer.SerializeValue(ref GlovesIndex);
        // ... serializa las demás ...
    }

    public bool Equals(PlayerData other)
    {
        return ClientId == other.ClientId &&
               Username == other.Username &&
               IsReady == other.IsReady &&
               BodyIndex == other.BodyIndex &&
               EyesIndex == other.EyesIndex &&
               GlovesIndex == other.GlovesIndex; 
    }
}