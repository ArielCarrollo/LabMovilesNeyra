using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Unity.Collections;
using Unity.Cinemachine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Prefabs & Scenes")]
    [SerializeField] private Transform playerPrefab;
    private const string GameSceneName = "Game";

    public NetworkList<PlayerData> PlayersInLobby;

    private Dictionary<string, string> authDataStore = new Dictionary<string, string>();
    private CinemachineImpulseSource impulseSource;
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            PlayersInLobby = new NetworkList<PlayerData>();
        }
        else
        {
            Destroy(gameObject);
        }
        impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
    }

    private void OnClientDisconnect(ulong clientId)
    {
        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            if (PlayersInLobby[i].ClientId == clientId)
            {
                PlayersInLobby.RemoveAt(i);
                break;
            }
        }
    }

    [Rpc(SendTo.Server)]
    public void RegisterPlayerServerRpc(string username, string password, ulong clientId)
    {
        if (authDataStore.ContainsKey(username))
        {
            NotifyClientOfFailureClientRpc("El nombre de usuario ya está en uso.", new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
            return;
        }
        authDataStore.Add(username, password);
        LoginPlayerServerRpc(username, password, clientId);
    }

    [Rpc(SendTo.Server)]
    public void LoginPlayerServerRpc(string username, string password, ulong clientId)
    {
        if (!authDataStore.TryGetValue(username, out string pass) || pass != password)
        {
            NotifyClientOfFailureClientRpc("Usuario o contraseña incorrectos.", new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
            return;
        }

        PlayersInLobby.Add(new PlayerData(clientId, username));

        LoginSuccessClientRpc(new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
    }

    [Rpc(SendTo.Server)]
    public void StartGameServerRpc()
    {
        if (!IsHost) return;

        NetworkManager.Singleton.SceneManager.LoadScene(GameSceneName, LoadSceneMode.Single);

        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += SpawnPlayersAfterSceneLoad;
    }

    private void SpawnPlayersAfterSceneLoad(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (sceneName != GameSceneName) return;

        foreach (var playerInfo in PlayersInLobby)
        {
            SpawnPlayer(playerInfo.ClientId);
        }

        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= SpawnPlayersAfterSceneLoad;
    }

    private void SpawnPlayer(ulong clientId)
    {
        Transform playerInstance = Instantiate(playerPrefab);
        NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();
        networkObject.SpawnWithOwnership(clientId, true);
    }

    [ClientRpc]
    public void LoginSuccessClientRpc(ClientRpcParams clientRpcParams = default)
    {
        UiGameManager.Instance.GoToLobby();
    }

    [ClientRpc]
    private void NotifyClientOfFailureClientRpc(string message, ClientRpcParams clientRpcParams = default)
    {
        if (UiGameManager.Instance != null) { UiGameManager.Instance.ShowErrorAndReset(message); }
    }
    public void TriggerCameraShake()
    {
        // Esta línea genera una sacudida con la configuración por defecto.
        // Puedes ajustar la fuerza y forma en el inspector del ImpulseSource.
        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse();
        }
    }
}