using UnityEngine;
using Unity.Netcode;

public class PlayerAppearance : NetworkBehaviour
{
    [SerializeField] private Transform bodiesParent;
    [SerializeField] private Transform eyesParent;
    [SerializeField] private Transform glovesParent;

    public readonly NetworkVariable<PlayerData> PlayerCustomData = new NetworkVariable<PlayerData>();

    public override void OnNetworkSpawn()
    {
        PlayerCustomData.OnValueChanged += OnDataChanged;
        ApplyAppearance(PlayerCustomData.Value);
    }

    public override void OnNetworkDespawn()
    {
        PlayerCustomData.OnValueChanged -= OnDataChanged;
    }

    private void OnDataChanged(PlayerData previousValue, PlayerData newValue)
    {
        ApplyAppearance(newValue);
    }
    public void ApplyAppearance(PlayerData data)
    {
        SetPartActive(bodiesParent, data.BodyIndex);
        SetPartActive(eyesParent, data.EyesIndex);
        SetPartActive(glovesParent, data.GlovesIndex);
    }

    private void SetPartActive(Transform parent, int index)
    {
        if (parent == null) return;

        for (int i = 0; i < parent.childCount; i++)
        {
            parent.GetChild(i).gameObject.SetActive(i == index);
        }
    }
}