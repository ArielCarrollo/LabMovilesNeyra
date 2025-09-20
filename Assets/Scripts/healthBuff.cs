using System;
using Unity.Netcode;
using UnityEngine;

public class healthBuff : NetworkBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        if (other.TryGetComponent<SimplePlayerController>(out var player))
        {
            player.TakeDamage(-1);
            Debug.Log($"Player {player.OwnerClientId} picked up a health buff.");
            GetComponent<NetworkObject>().Despawn();
        }
    }
}
