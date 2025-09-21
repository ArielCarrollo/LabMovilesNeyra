using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private Transform playerPrefab;
    private const string GameSceneName = "Game";

    private Dictionary<string, PlayerData> playerDataStore = new Dictionary<string, PlayerData>();
    private Dictionary<ulong, PlayerData> clientDataMap = new Dictionary<ulong, PlayerData>();

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    [Rpc(SendTo.Server)]
    public void RegisterPlayerServerRpc(string username, string password, ulong clientId)
    {
        if (playerDataStore.ContainsKey(username))
        {
            NotifyClientOfFailureClientRpc("El nombre de usuario ya está en uso.", new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
            return;
        }
        playerDataStore.Add(username, new PlayerData(username, password));
        LoginPlayerServerRpc(username, password, clientId);
    }

    [Rpc(SendTo.Server)]
    public void LoginPlayerServerRpc(string username, string password, ulong clientId)
    {
        if (!playerDataStore.TryGetValue(username, out PlayerData data) || data.password != password)
        {
            NotifyClientOfFailureClientRpc("Usuario o contraseña incorrectos.", new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
            return;
        }

        clientDataMap[clientId] = data;
        LoadGameSceneClientRpc(new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
        StartCoroutine(SpawnPlayerWithDelay(clientId, data));
    }

    [ClientRpc]
    private void LoadGameSceneClientRpc(ClientRpcParams clientRpcParams = default)
    {
        SceneManager.LoadScene(GameSceneName, LoadSceneMode.Single);
    }

    private IEnumerator SpawnPlayerWithDelay(ulong clientId, PlayerData data)
    {
        yield return new WaitForSeconds(1.0f);

        Transform playerInstance = Instantiate(playerPrefab);
        NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();
        networkObject.Spawn(true);
        networkObject.ChangeOwnership(clientId);

        SimplePlayerController playerController = playerInstance.GetComponent<SimplePlayerController>();
        if (playerController != null)
        {
            playerController.CurrentHealth.Value = data.health;
        }
    }

    [ClientRpc]
    private void NotifyClientOfFailureClientRpc(string message, ClientRpcParams clientRpcParams = default)
    {
        if (UiGameManager.Instance != null) { UiGameManager.Instance.ShowErrorAndReset(message); }
    }
}