// Archivo: GameManager.cs
using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic; // Necesario para usar Diccionarios

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private Transform playerPrefab;

    // Nuestra "base de datos" en memoria. La clave (string) es el nombre de usuario.
    private Dictionary<string, PlayerData> playerDataStore = new Dictionary<string, PlayerData>();

    // Un diccionario para saber qué datos de jugador corresponden a qué cliente conectado.
    private Dictionary<ulong, PlayerData> clientDataMap = new Dictionary<ulong, PlayerData>();

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    #region REGISTRO Y LOGIN

    [Rpc(SendTo.Server)]
    public void RegisterPlayerServerRpc(string username, string password, ulong clientId)
    {
        // 1. Validar en el servidor: ¿Ya existe el usuario?
        if (playerDataStore.ContainsKey(username))
        {
            // El usuario ya existe, informamos al cliente del error.
            NotifyClientOfFailureClientRpc("El nombre de usuario ya está en uso.", clientId);
            return;
        }

        // 2. Crear y guardar los datos del nuevo jugador.
        PlayerData newPlayerData = new PlayerData(username, password);
        playerDataStore.Add(username, newPlayerData);
        Debug.Log($"[Servidor] Nuevo jugador registrado: {username}");

        // 3. Proceder al login directamente después del registro.
        LoginPlayerServerRpc(username, password, clientId);
    }

    [Rpc(SendTo.Server)]
    public void LoginPlayerServerRpc(string username, string password, ulong clientId)
    {
        // 1. Validar: ¿Existe el usuario?
        if (!playerDataStore.TryGetValue(username, out PlayerData data))
        {
            NotifyClientOfFailureClientRpc("El usuario no existe.", clientId);
            return;
        }

        // 2. Validar: ¿La contraseña es correcta?
        if (data.password != password)
        {
            NotifyClientOfFailureClientRpc("Contraseña incorrecta.", clientId);
            return;
        }

        // 3. Guardar la relación entre el ID de conexión y los datos del jugador.
        clientDataMap[clientId] = data;

        Debug.Log($"[Servidor] Jugador {username} ha iniciado sesión. Spawneando...");

        // 4. Spawnear el jugador.
        SpawnPlayerForClient(clientId, data);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void NotifyClientOfFailureClientRpc(string message, ulong clientId)
    {
        // Solo el cliente que hizo la petición debe recibir este mensaje.
        if (NetworkManager.Singleton.LocalClientId != clientId) return;

        Debug.LogError($"Error de autenticación: {message}");
        // Aquí llamamos a la UI para que muestre el error y reactive los botones.
        UiGameManager.Instance.ShowErrorAndReset(message);
    }

    #endregion

    #region SPAWN DE JUGADOR

    // Sobreescribimos la lógica de spawn original.
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Ya no spawneamos al conectar, esperamos al login.
            // NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        }
    }

    private void SpawnPlayerForClient(ulong clientId, PlayerData data)
    {
        Transform playerInstance = Instantiate(playerPrefab);
        NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();
        networkObject.SpawnAsPlayerObject(clientId, true);

        // Cargar los datos guardados en el jugador recién creado.
        // Esto se hace en el servidor para que los NetworkVariables se sincronicen.
        SimplePlayerController playerController = playerInstance.GetComponent<SimplePlayerController>();
        if (playerController != null)
        {
            playerController.CurrentHealth.Value = data.health;
            // Podrías cargar más datos aquí, como la posición inicial.
            // playerInstance.transform.position = data.position;
        }
    }
    #endregion
}