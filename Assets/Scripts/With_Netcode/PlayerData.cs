using Unity.Collections;
using Unity.Netcode;
using System;

[System.Serializable] 
public struct PlayerData : INetworkSerializable, IEquatable<PlayerData>
{
    public ulong ClientId;
    public FixedString64Bytes Username;
    public bool IsReady;

    public int Level;
    public int CurrentXP;

    public int BodyIndex;
    public int EyesIndex;
    public int GlovesIndex;

    public PlayerData(ulong clientId, string username, bool isReady = false)
    {
        ClientId = clientId;
        Username = new FixedString64Bytes(username);
        IsReady = isReady;

        Level = 1;
        CurrentXP = 0;

        BodyIndex = 0;
        EyesIndex = 0;
        GlovesIndex = 0;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref Username);
        serializer.SerializeValue(ref IsReady);

        // --- Serializar nuevos datos ---
        serializer.SerializeValue(ref Level);
        serializer.SerializeValue(ref CurrentXP);

        serializer.SerializeValue(ref BodyIndex);
        serializer.SerializeValue(ref EyesIndex);
        serializer.SerializeValue(ref GlovesIndex);
    }

    public bool Equals(PlayerData other)
    {
        return ClientId == other.ClientId &&
               Username == other.Username &&
               IsReady == other.IsReady &&
               Level == other.Level &&           // --- Añadido a la comparación ---
               CurrentXP == other.CurrentXP &&   // --- Añadido a la comparación ---
               BodyIndex == other.BodyIndex &&
               EyesIndex == other.EyesIndex &&
               GlovesIndex == other.GlovesIndex;
    }
}