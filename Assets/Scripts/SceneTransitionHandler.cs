// Archivo: SceneTransitionHandler.cs (CON DEBUG LOGS)
using Unity.Netcode;
using UnityEngine;

public class SceneTransitionHandler : NetworkBehaviour
{
    //public override void OnNetworkSpawn()
    //{
    //    // --- ¡LOG DE VERIFICACIÓN 3! ---
    //    if (GameManager.Instance != null)
    //    {
    //        Debug.Log($"[Cliente] SceneTransitionHandler.OnNetworkSpawn se ha ejecutado. Enviando petición de spawn para el cliente {NetworkManager.Singleton.LocalClientId}.");
    //        GameManager.Instance.RequestPlayerSpawnServerRpc(NetworkManager.Singleton.LocalClientId);
    //    }
    //    else
    //    {
    //        Debug.LogError("[Cliente] SceneTransitionHandler no pudo encontrar el GameManager.Instance. El spawn fallará.");
    //    }
    //}
}