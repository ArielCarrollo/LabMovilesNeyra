using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Unity.Collections;
using Unity.Cinemachine;
using System.Threading.Tasks;
using System.Linq;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Prefabs & Scenes")]
    [SerializeField] private Transform playerPrefab;
    private const string GameSceneName = "Game";

    [Header("Game Settings")]
    private const int MaxPlayers = 5;

    public NetworkList<PlayerData> PlayersInLobby = new NetworkList<PlayerData>();

    private CinemachineImpulseSource impulseSource;

    [Header("Leveling System")]
    [SerializeField] private int baseXpToLevelUp = 100;
    [SerializeField] private float xpMultiplierPerLevel = 1.2f;

    private Dictionary<ulong, bool> clientLoadedLobbyUI = new Dictionary<ulong, bool>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        impulseSource = GetComponent<CinemachineImpulseSource>();
    }
    private void OnDestroy()
    {
        // Muy importante: soltar la referencia estática
        if (Instance == this)
        {
            Instance = null;
        }

        // Por si quedó algo en memoria
        if (PlayersInLobby != null)
            PlayersInLobby.Clear();

        if (clientLoadedLobbyUI != null)
            clientLoadedLobbyUI.Clear();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
    }

    private void OnClientDisconnect(ulong clientId)
    {
        if (!IsServer) return;

        if (clientLoadedLobbyUI.ContainsKey(clientId))
        {
            clientLoadedLobbyUI.Remove(clientId);
        }

        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            if (PlayersInLobby[i].ClientId == clientId)
            {
                Debug.Log($"Servidor: Eliminando jugador {PlayersInLobby[i].Username.ToString()} (ID: {clientId}) de la lista.");
                PlayersInLobby.RemoveAt(i);
                break;
            }
        }
    }

    // --- Lógica de Autenticación y Carga de Lobby ---

    [Rpc(SendTo.Server)]
    public void OnPlayerAuthenticatedServerRpc(PlayerData playerData, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        // 👇 SANITIZAR AQUÍ TAMBIÉN
        string name = playerData.Username.ToString();
        if (!string.IsNullOrEmpty(name))
            name = name.Replace("\0", string.Empty);
        if (string.IsNullOrWhiteSpace(name))
            name = $"Player_{clientId}";
        playerData.Username = new Unity.Collections.FixedString64Bytes(name);

        bool isAlreadyConnected = false;
        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            if (PlayersInLobby[i].ClientId == clientId)
            {
                isAlreadyConnected = true;
                break;
            }
        }

        if (isAlreadyConnected)
        {
            Debug.LogWarning($"El cliente {clientId} ya está en la lista. Sincronizando UI.");
        }
        else
        {
            if (PlayersInLobby.Count >= 5)
            {
                Debug.LogWarning($"El cliente {clientId} intentó unirse pero el lobby está lleno.");
                return;
            }

            playerData.ClientId = clientId;
            playerData.IsReady = false;
            PlayersInLobby.Add(playerData);
            clientLoadedLobbyUI[clientId] = false;
            Debug.Log($"Servidor: Jugador {playerData.Username.ToString()} (ID: {clientId}) añadido a la lista.");
        }

        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId }
            }
        };
        LoginSuccessClientRpc(clientRpcParams);
    }


    [ClientRpc]
    public void LoginSuccessClientRpc(ClientRpcParams clientRpcParams = default)
    {
        Debug.Log("Cliente: Recibida señal LoginSuccessClientRpc. Cargando UI de Lobby...");
        if (UiGameManager.Instance != null)
        {
            UiGameManager.Instance.GoToLobby();
        }
        else
        {
            Debug.LogError("Error: UiGameManager.Instance es nulo en el cliente.");
        }

        ConfirmLobbyUILoadedServerRpc();
    }

    [Rpc(SendTo.Server)]
    private void ConfirmLobbyUILoadedServerRpc(RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (clientLoadedLobbyUI.ContainsKey(clientId))
        {
            clientLoadedLobbyUI[clientId] = true;
            Debug.Log($"Servidor: Cliente {clientId} ha confirmado la carga de la UI del Lobby.");
        }

        ForceSyncPlayerToClient(clientId);
    }

    private void ForceSyncPlayerToClient(ulong targetClientId)
    {
        PlayerData playerData = new PlayerData();
        bool found = false;
        foreach (var p in PlayersInLobby)
        {
            if (p.ClientId == targetClientId)
            {
                playerData = p;
                found = true;
                break;
            }
        }

        if (found)
        {
            
        }
    }


    // --- Lógica de la Sala de Espera (Ready, Customization) ---

    [Rpc(SendTo.Server)]
    public void ToggleReadyServerRpc(RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            if (PlayersInLobby[i].ClientId == clientId)
            {
                PlayerData updatedPlayer = PlayersInLobby[i];
                updatedPlayer.IsReady = !updatedPlayer.IsReady;
                PlayersInLobby[i] = updatedPlayer;
                break;
            }
        }
    }

    [Rpc(SendTo.Server)]
    public void UpdatePlayerAppearanceServerRpc(PlayerData customData, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            if (PlayersInLobby[i].ClientId == clientId)
            {
                PlayerData updatedPlayer = PlayersInLobby[i];
                updatedPlayer.BodyIndex = customData.BodyIndex;
                updatedPlayer.EyesIndex = customData.EyesIndex;
                updatedPlayer.GlovesIndex = customData.GlovesIndex;
                PlayersInLobby[i] = updatedPlayer;
                break;
            }
        }
    }

    [Rpc(SendTo.Server)]
    public void UpdatePlayerNameServerRpc(string newName, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            if (PlayersInLobby[i].ClientId == clientId)
            {
                PlayerData updatedPlayer = PlayersInLobby[i];
                updatedPlayer.Username = new FixedString64Bytes(newName);
                PlayersInLobby[i] = updatedPlayer;
                break;
            }
        }
    }

    // --- Expulsar jugador (solo host) ---
    [Rpc(SendTo.Server)]
    public void KickPlayerServerRpc(ulong targetClientId, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId)
            return;

        if (targetClientId == NetworkManager.ServerClientId)
            return;

        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            if (PlayersInLobby[i].ClientId == targetClientId)
            {
                Debug.Log($"Servidor: expulsando al jugador {PlayersInLobby[i].Username.ToString()} ({targetClientId})");
                PlayersInLobby.RemoveAt(i);
                break;
            }
        }

        if (NetworkManager.Singleton.ConnectedClients.ContainsKey(targetClientId))
        {
            NetworkManager.Singleton.DisconnectClient(targetClientId);
        }
    }

    [Rpc(SendTo.Server)]
    public void CloseLobbyServerRpc(RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId)
            return;

        var connectedIds = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        foreach (var id in connectedIds)
        {
            if (id == NetworkManager.ServerClientId)
                continue;

            if (NetworkManager.Singleton.ConnectedClients.ContainsKey(id))
            {
                NetworkManager.Singleton.DisconnectClient(id);
            }
        }

        PlayersInLobby.Clear();

        CloseLobbyOnClientRpc();

        NetworkManager.Singleton.Shutdown();
        Destroy(gameObject);
    }

    [ClientRpc]
    private void CloseLobbyOnClientRpc(ClientRpcParams clientRpcParams = default)
    {
        var relay = FindObjectOfType<RelayLobbyConnector>();
        if (relay != null)
        {
            // vuelve al panel de selección
            relay.ClearCurrentLobby();
            relay.ShowJoiningPanel();
        }

        if (UiGameManager.Instance != null)
        {
            //UiGameManager.Instance.ShowLobbySelection();
        }
    }

    // --- Lógica de Inicio de Juego ---

    [Rpc(SendTo.Server)]
    public void StartGameServerRpc(RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;
        if (!AllPlayersReady()) return;

        NetworkManager.Singleton.SceneManager.LoadScene(GameSceneName, LoadSceneMode.Single);
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnClientSceneLoaded;
    }

    private bool AllPlayersReady()
    {
        if (PlayersInLobby.Count == 0) return false;
        foreach (var player in PlayersInLobby)
        {
            if (!player.IsReady) return false;
        }
        return true;
    }

    private void OnClientSceneLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (sceneName != GameSceneName) return;

        var allConnectedClientIds = NetworkManager.Singleton.ConnectedClientsIds.ToList();

        if (NetworkManager.Singleton.IsHost)
        {
            allConnectedClientIds.Remove(NetworkManager.Singleton.LocalClientId);
        }

        bool allClientsLoaded = true;
        foreach (var clientId in allConnectedClientIds)
        {
            if (!clientsCompleted.Contains(clientId))
            {
                allClientsLoaded = false;
                break;
            }
        }

        if (NetworkManager.Singleton.IsHost && !clientsCompleted.Contains(NetworkManager.Singleton.LocalClientId))
        {
            allClientsLoaded = false;
        }


        if (allClientsLoaded)
        {
            Debug.Log("Servidor: Todos los clientes han cargado la escena 'Game'. Spawneando jugadores...");
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnClientSceneLoaded;

            foreach (var player in PlayersInLobby)
            {
                SpawnPlayerForClient(player);
            }
        }
    }

    private void SpawnPlayerForClient(PlayerData playerData)
    {
        Transform playerInstance = Instantiate(playerPrefab);
        playerInstance.GetComponent<NetworkObject>().SpawnAsPlayerObject(playerData.ClientId, true);

        PlayerAppearance appearance = playerInstance.GetComponent<PlayerAppearance>();
        if (appearance != null)
        {
            appearance.PlayerCustomData.Value = playerData;
        }

        PlayerNicknameUI nicknameUI = playerInstance.GetComponentInChildren<PlayerNicknameUI>();
        if (nicknameUI != null)
        {
            nicknameUI.Nickname.Value = playerData.Username;
            nicknameUI.Level.Value = playerData.Level;
        }
    }


    // --- RPCs y Misceláneos ---

    [ClientRpc]
    private void NotifyClientOfFailureClientRpc(string message, ClientRpcParams clientRpcParams = default)
    {
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

    // --- Sistema de Experiencia ---

    public int GetXpForLevel(int level)
    {
        return Mathf.FloorToInt(baseXpToLevelUp * Mathf.Pow(xpMultiplierPerLevel, level));
    }

    [Rpc(SendTo.Server)]
    public void AddXpToPlayerServerRpc(int amount, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            if (PlayersInLobby[i].ClientId == clientId)
            {
                PlayerData updatedPlayer = PlayersInLobby[i];
                updatedPlayer.CurrentXP += amount;

                int xpNeeded = GetXpForLevel(updatedPlayer.Level);
                while (updatedPlayer.CurrentXP >= xpNeeded)
                {
                    updatedPlayer.CurrentXP -= xpNeeded;
                    updatedPlayer.Level++;
                    xpNeeded = GetXpForLevel(updatedPlayer.Level);
                }

                PlayersInLobby[i] = updatedPlayer;
                SavePlayerProgress(updatedPlayer, clientId);

                break;
            }
        }
    }

    private void SavePlayerProgress(PlayerData data, ulong clientId)
    {
        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId }
            }
        };
        RequestClientSaveProgressClientRpc(data, clientRpcParams);
    }

    [ClientRpc]
    private void RequestClientSaveProgressClientRpc(PlayerData data, ClientRpcParams clientRpcParams)
    {
        Debug.Log($"Cliente: Recibida solicitud para guardar progreso. Nivel: {data.Level}, XP: {data.CurrentXP}");
        if (CloudAuthManager.Instance != null)
        {
            CloudAuthManager.Instance.UpdateLocalData(data);
            _ = CloudAuthManager.Instance.SavePlayerProgress();
        }
    }
}