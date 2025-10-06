using UnityEngine;
using Unity.Netcode;
using TMPro;
using Unity.Collections;

public class PlayerNicknameUI : NetworkBehaviour
{
    [SerializeField]
    private TextMeshProUGUI nicknameText;

    // --- NUEVAS VARIABLES ---
    public NetworkVariable<FixedString64Bytes> Nickname = new NetworkVariable<FixedString64Bytes>();
    public NetworkVariable<int> Level = new NetworkVariable<int>(1); // Nivel por defecto 1

    public override void OnNetworkSpawn()
    {
        // Nos suscribimos a los cambios de AMBAS variables
        Nickname.OnValueChanged += HandleDisplayTextChanged;
        Level.OnValueChanged += HandleDisplayTextChanged;

        // Actualizamos el texto con los valores iniciales
        UpdateDisplayText();
    }

    // Un solo método para manejar el cambio de cualquiera de las dos variables
    private void HandleDisplayTextChanged(FixedString64Bytes previousValue, FixedString64Bytes newValue)
    {
        UpdateDisplayText();
    }

    private void HandleDisplayTextChanged(int previousValue, int newValue)
    {
        UpdateDisplayText();
    }

    // El método que construye el texto final
    private void UpdateDisplayText()
    {
        nicknameText.text = $"[Nvl {Level.Value}] {Nickname.Value}";
    }
}