using Unity.Collections;
using Unity.Netcode;

public struct PlayerData : INetworkSerializable, System.IEquatable<PlayerData>
{
    public ulong ClientId;
    public FixedString64Bytes Username;

    public PlayerData(ulong clientId, string username)
    {
        ClientId = clientId;
        Username = new FixedString64Bytes(username);
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref Username);
    }

    public bool Equals(PlayerData other)
    {
        return ClientId == other.ClientId && Username == other.Username;
    }
}