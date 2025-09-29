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

    [Header("Game Settings")]
    private const int MaxPlayers = 5;
    public NetworkList<PlayerData> PlayersInLobby;

    // Se elimina el authDataStore
    // private Dictionary<string, string> authDataStore = new Dictionary<string, string>();
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

    // Nuevo método para manejar jugadores después de la autenticación de Unity Cloud
    [Rpc(SendTo.Server)]
    public void OnPlayerAuthenticatedServerRpc(string username, ulong clientId)
    {
        if (PlayersInLobby.Count >= MaxPlayers)
        {
            NotifyClientOfFailureClientRpc("El lobby está lleno.", new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
            return;
        }

        // Opcional: Verificar si el jugador ya está en el lobby para evitar duplicados
        foreach (var player in PlayersInLobby)
        {
            if (player.ClientId == clientId)
            {
                // El jugador ya está, no hacer nada o manejar reconexión.
                return;
            }
        }

        PlayersInLobby.Add(new PlayerData(clientId, username));
        LoginSuccessClientRpc(new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
    }

    // Los métodos RegisterPlayerServerRpc y LoginPlayerServerRpc han sido eliminados.

    [Rpc(SendTo.Server)]
    public void ToggleReadyServerRpc(ulong clientId)
    {
        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            if (PlayersInLobby[i].ClientId == clientId)
            {
                PlayerData updatedPlayerData = PlayersInLobby[i];
                updatedPlayerData.IsReady = !updatedPlayerData.IsReady;
                PlayersInLobby[i] = updatedPlayerData;
                break;
            }
        }
    }

    [Rpc(SendTo.Server)]
    public void ChangeAppearanceServerRpc(PlayerData newPlayerData, ulong clientId)
    {
        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            if (PlayersInLobby[i].ClientId == clientId)
            {
                PlayersInLobby[i] = newPlayerData;
                break;
            }
        }
    }

    [Rpc(SendTo.Server)]
    public void StartGameServerRpc()
    {
        if (!IsHost) return;

        bool allPlayersReady = true;
        if (PlayersInLobby.Count == 0)
        {
            allPlayersReady = false;
        }
        else
        {
            foreach (var player in PlayersInLobby)
            {
                if (!player.IsReady)
                {
                    allPlayersReady = false;
                    break;
                }
            }
        }

        if (!allPlayersReady) return;

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

        PlayerData playerDataToSpawn = new PlayerData();
        bool foundPlayer = false;
        foreach (var p in PlayersInLobby)
        {
            if (p.ClientId == clientId)
            {
                playerDataToSpawn = p;
                foundPlayer = true;
                break;
            }
        }

        if (foundPlayer)
        {
            PlayerAppearance appearance = playerInstance.GetComponent<PlayerAppearance>();
            if (appearance != null)
            {
                appearance.PlayerCustomData.Value = playerDataToSpawn;
            }

            PlayerNicknameUI nicknameUI = playerInstance.GetComponentInChildren<PlayerNicknameUI>();
            if (nicknameUI != null)
            {
                nicknameUI.Nickname.Value = playerDataToSpawn.Username;
            }
        }
    }

    [ClientRpc]
    public void LoginSuccessClientRpc(ClientRpcParams clientRpcParams = default)
    {
        UiGameManager.Instance.GoToLobby();
    }

    [ClientRpc]
    private void NotifyClientOfFailureClientRpc(string message, ClientRpcParams clientRpcParams = default)
    {
        // Este error ahora sería manejado por el sistema de autenticación, pero lo dejamos por si se usa para otros fines.
        if (UiGameManager.Instance != null)
        {
            UiGameManager.Instance.ShowError(message);
        }
    }

    public void TriggerCameraShake()
    {
        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse();
        }
    }
}