using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem; 
public class Ninja : CharacterBase
{
    [Header("Stats Específicos de Ninja")]
    [SerializeField] private int shurikenCount = 10;
    [SerializeField] private float velocidadNinja = 8f; 

   
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            this.velocidad = velocidadNinja;
        }
    }
    public virtual void OnSpecialAttack(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;

        if (context.performed)
        {
            // El cliente pide al servidor que lance un shuriken
            SpecialAttackServerRpc();
        }
    }

    [ServerRpc]
    private void SpecialAttackServerRpc()
    {
        if (shurikenCount > 0)
        {
            shurikenCount--;
            Debug.Log("SERVIDOR: Ninja lanza Shuriken! Quedan: " + shurikenCount);

            // Lógica de Spawn del Shuriken
            // GameObject shuriken = Instantiate(shurikenPrefab, ...);
            // shuriken.GetComponent<NetworkObject>().Spawn(true);
        }
    }
    [ServerRpc]
    protected override void UltimateAttackServerRpc()
    {
        // 'base.UltimateAttackServerRpc();' // No llamamos al padre (está vacío)

        Debug.Log("SERVIDOR: ¡¡¡ULTI DE NINJA: 'Kage Bunshin no Jutsu'!!!");
        // Aquí iría la lógica de la ulti del ninja
        // (Crear clones, volverse invisible, etc.)
    }
}