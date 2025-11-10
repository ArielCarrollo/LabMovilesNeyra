using Unity.Netcode;
using UnityEngine;

// Hereda de CharacterBase, no de NetworkBehaviour directamente
public class Ninja : CharacterBase
{
    [Header("Stats Específicos de Ninja")]
    [SerializeField] private int shurikenCount = 10;
    [SerializeField] private float velocidadNinja = 8f; // Stat personalizado

    /// <summary>
    /// Usamos OnNetworkSpawn para aplicar las personalizaciones de stats
    /// </summary>
    public override void OnNetworkSpawn()
    {
        // ¡MUY IMPORTANTE! Llama a la función base para que inicialice todo
        base.OnNetworkSpawn();

        // El servidor puede sobre-escribir los stats base
        if (IsServer)
        {
            // El 'velocidad' es de la clase base, lo modificamos
            this.velocidad = velocidadNinja;
        }
    }

    /// <summary>
    /// Sobreescribimos Update para añadir nuestro ataque especial
    /// </summary>
    protected override void Update()
    {
        // ¡MUY IMPORTANTE! Llama al Update base para que maneje el movimiento
        base.Update();

        // Solo el dueño puede disparar
        if (!IsOwner) return;

        // Lógica de ataque especial del Ninja
        if (Input.GetKeyDown(KeyCode.F))
        {
            // El cliente pide al servidor que lance un shuriken
            ThrowShurikenServerRpc();
        }
    }

    /// <summary>
    /// [ServerRpc]
    /// El cliente Ninja llama a esto, se ejecuta EN EL SERVIDOR.
    /// </summary>
    [ServerRpc]
    private void ThrowShurikenServerRpc()
    {
        if (shurikenCount > 0)
        {
            shurikenCount--;
            Debug.Log("SERVIDOR: Ninja lanza Shuriken! Quedan: " + shurikenCount);

            // Aquí iría la lógica de instanciar el prefab del shuriken
            // y hacerle NetworkObject.Spawn()
        }
        else
        {
            Debug.Log("SERVIDOR: Ninja sin Shurikens!");
        }
    }
}