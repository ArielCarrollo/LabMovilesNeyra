using UnityEngine;
using Unity.Netcode;

public class AttackBuff : NetworkBehaviour
{
    [Header("Buff Settings")]
    [SerializeField] private int minAttackBonus = 1;
    [SerializeField] private int maxAttackBonus = 3;
    [SerializeField] private float duration = 10f; // Opcional: si quieres que el buff dure un tiempo

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        if (other.TryGetComponent<SimplePlayerController>(out var player))
        {
            int attackBonus = Random.Range(minAttackBonus, maxAttackBonus + 1);
            player.ApplyAttackBuff(attackBonus, duration);

            // Despawneamos el buff para que no se pueda coger dos veces
            GetComponent<NetworkObject>().Despawn();
        }
    }
}