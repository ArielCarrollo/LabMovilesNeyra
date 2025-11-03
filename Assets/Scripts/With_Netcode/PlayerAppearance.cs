using UnityEngine;
using Unity.Netcode;

public class PlayerAppearance : NetworkBehaviour
{
    [SerializeField] private Transform bodiesParent;
    [SerializeField] private Transform eyesParent;
    [SerializeField] private Transform glovesParent;

    public readonly NetworkVariable<PlayerData> PlayerCustomData = new NetworkVariable<PlayerData>();

    // --- CORRECCIÓN: Métodos públicos para obtener los contadores ---
    public int GetBodyCount() => bodiesParent != null ? bodiesParent.childCount : 0;
    public int GetEyesCount() => eyesParent != null ? eyesParent.childCount : 0;
    public int GetGlovesCount() => glovesParent != null ? glovesParent.childCount : 0;
    // --- Fin de la Corrección ---

    public override void OnNetworkSpawn()
    {
        PlayerCustomData.OnValueChanged += OnDataChanged;
        // Al aparecer, aplicamos la apariencia con los datos que ya tiene la variable de red.
        if (PlayerCustomData.Value.Username.Length > 0) // Comprobar si el struct tiene datos válidos
        {
            ApplyAppearance(PlayerCustomData.Value);
        }
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

        // Asegurarse de que el índice esté dentro de los límites
        if (index < 0 || index >= parent.childCount)
        {
            // Si el índice es inválido, desactivar todo o activar el primero (default)
            index = 0;
            if (parent.childCount == 0) return; // No hay hijos, no hacer nada
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            parent.GetChild(i).gameObject.SetActive(i == index);
        }
    }
}
