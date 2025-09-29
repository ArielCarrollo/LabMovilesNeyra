using UnityEngine;
using Unity.Netcode;
using TMPro;
using Unity.Collections;

public class PlayerNicknameUI : NetworkBehaviour
{
    [SerializeField]
    private TextMeshProUGUI nicknameText;

    public NetworkVariable<FixedString64Bytes> Nickname = new NetworkVariable<FixedString64Bytes>();

    public override void OnNetworkSpawn()
    {
        // Se suscribe a los cambios en la variable de red.
        // Cuando el servidor cambie el valor, se actualizará el texto en todos los clientes.
        Nickname.OnValueChanged += (previousValue, newValue) =>
        {
            nicknameText.text = newValue.ToString();
        };

        // Asigna el valor inicial que ya tiene la variable al aparecer.
        nicknameText.text = Nickname.Value.ToString();
    }
}