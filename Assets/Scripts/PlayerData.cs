using Unity.Collections;
using Unity.Netcode;
using System;

public struct PlayerData : INetworkSerializable, IEquatable<PlayerData>
{
    public ulong ClientId;
    public FixedString64Bytes Username;
    public bool IsReady; // Añadido para el estado de "listo"

    public PlayerData(ulong clientId, string username, bool isReady = false)
    {
        ClientId = clientId;
        Username = new FixedString64Bytes(username);
        IsReady = isReady;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref Username);
        serializer.SerializeValue(ref IsReady); // Serializamos el nuevo dato
    }

    public bool Equals(PlayerData other)
    {
        return ClientId == other.ClientId && Username == other.Username && IsReady == other.IsReady;
    }
}