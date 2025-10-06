using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Unity.Collections;
using Unity.Cinemachine;
using System.Threading.Tasks;

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

    [Header("Leveling System")]
    [SerializeField] private int baseXpToLevelUp = 100;
    [SerializeField] private float xpMultiplierPerLevel = 1.2f;
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
            // Construcción explícita de ClientRpcParams para evitar errores
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId }
                }
            };
            NotifyClientOfFailureClientRpc("El lobby está lleno.", clientRpcParams);
            return;
        }

        foreach (var player in PlayersInLobby)
        {
            if (player.ClientId == clientId) return;
        }

        PlayerData loadedData = CloudAuthManager.Instance.LocalPlayerData;
        loadedData.ClientId = clientId;
        loadedData.Username = username;

        PlayersInLobby.Add(loadedData);

        var successRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId }
            }
        };
        LoginSuccessClientRpc(successRpcParams);
    }

    public int GetXpForLevel(int level)
    {
        return Mathf.FloorToInt(baseXpToLevelUp * Mathf.Pow(level, xpMultiplierPerLevel));
    }

    [Rpc(SendTo.Server)]
    public void AwardExperienceServerRpc(ulong playerId, int amount)
    {
        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            if (PlayersInLobby[i].ClientId == playerId)
            {
                PlayerData updatedData = PlayersInLobby[i];
                bool leveledUp = false;

                updatedData.CurrentXP += amount;
                int xpNeeded = GetXpForLevel(updatedData.Level);

                while (updatedData.CurrentXP >= xpNeeded)
                {
                    updatedData.Level++;
                    updatedData.CurrentXP -= xpNeeded;
                    xpNeeded = GetXpForLevel(updatedData.Level);
                    leveledUp = true;
                }

                PlayersInLobby[i] = updatedData;

                if (leveledUp)
                {
                    // Iteramos a través de TODOS los objetos que existen en la red.
                    foreach (NetworkObject spawnedObject in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
                    {
                        // Buscamos el objeto que le pertenece al jugador que subió de nivel.
                        if (spawnedObject.OwnerClientId == playerId)
                        {
                            // Nos aseguramos de que sea un personaje jugador (y no otro objeto).
                            if (spawnedObject.TryGetComponent<SimplePlayerController>(out _))
                            {
                                // Obtenemos su UI y actualizamos el nivel.
                                PlayerNicknameUI nicknameUI = spawnedObject.GetComponentInChildren<PlayerNicknameUI>();
                                if (nicknameUI != null)

                                {
                                    nicknameUI.Level.Value = updatedData.Level;
                                }
                                // Encontramos al jugador, rompemos el bucle para no seguir buscando.
                                break;
                            }
                        }
                    }
                }

                ClientRpcSendParams sendParams = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { playerId }
                };
                SaveChangesToCloudClientRpc(updatedData, new ClientRpcParams { Send = sendParams });

                return;
            }
        }
    }

    [ClientRpc]
    private void SaveChangesToCloudClientRpc(PlayerData data, ClientRpcParams clientRpcParams = default)
    {
        // Este método ya no es 'async'.
        // Solo se ejecuta en el cliente dueño de los datos.
        if (!IsOwner) return;

        // Llamamos a una nueva función asíncrona que manejará el guardado.
        SaveChangesToCloudAsync(data);
    }
    private async void SaveChangesToCloudAsync(PlayerData data)
    {
        // Esta función sí es 'async', pero no es un RPC.
        CloudAuthManager.Instance.UpdateLocalData(data);
        await CloudAuthManager.Instance.SavePlayerProgress();
    }

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
            SpawnPlayer(playerInfo); // --- Pasamos toda la PlayerData ---
        }

        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= SpawnPlayersAfterSceneLoad;
    }

    private void SpawnPlayer(PlayerData playerDataToSpawn) // --- Recibimos toda la PlayerData ---
    {
        Transform playerInstance = Instantiate(playerPrefab);
        NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();
        networkObject.SpawnWithOwnership(playerDataToSpawn.ClientId, true);

        // Ya no necesitamos buscar al jugador, lo recibimos directamente
        PlayerAppearance appearance = playerInstance.GetComponent<PlayerAppearance>();
        if (appearance != null)
        {
            appearance.PlayerCustomData.Value = playerDataToSpawn;
        }

        // --- LA CORRECCIÓN CLAVE ---
        PlayerNicknameUI nicknameUI = playerInstance.GetComponentInChildren<PlayerNicknameUI>();
        if (nicknameUI != null)
        {
            nicknameUI.Nickname.Value = playerDataToSpawn.Username;
            nicknameUI.Level.Value = playerDataToSpawn.Level; // <-- ASIGNAMOS EL NIVEL
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

    [Rpc(SendTo.Server)]
    public void UpdatePlayerNameServerRpc(string newName, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        for (int i = 0; i < PlayersInLobby.Count; i++)
        {
            if (PlayersInLobby[i].ClientId == clientId)
            {
                // Como PlayerData es un struct, debemos reemplazarlo, no modificarlo directamente
                PlayerData updatedPlayer = PlayersInLobby[i];
                updatedPlayer.Username = newName;
                PlayersInLobby[i] = updatedPlayer;
                break;
            }
        }
    }
}