using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.UI;

public class RelayLobbyConnector : MonoBehaviour
{
    [Header("UI (Opcional)")]
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI joinCodeLabel; // para mostrar el código si eres host
    [SerializeField] private GameObject connectionPanel;     // Panel con Crear/Join/Quick Join
    [SerializeField] private LobbyUIManager lobbyUIManager;  // Se inicializa tras conectar

    [Header("Lobby Config")]
    [SerializeField] private string lobbyName = "Sala";
    [SerializeField] private int maxPlayers = 5;
    [SerializeField] private bool isPrivate = false;

    [Header("Network Prefabs")]
    [SerializeField] private GameObject gameManagerPrefab; // mismo prefab que antes para el Host

    private Lobby _currentLobby;
    private Coroutine _heartbeatRoutine;
    private const float HEARTBEAT_INTERVAL = 15f;

    private void Awake()
    {
        // Asegura servicios por si el login no los encendió (o si entras directo a esta escena en pruebas)
        _ = EnsureServices();
    }

    private async Task EnsureServices()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            var options = new InitializationOptions().SetEnvironmentName("production");
            await UnityServices.InitializeAsync(options);
        }
    }

    // ====== Botones ======

    public async void OnCreateLobbyClicked()
    {
        await EnsureServices();
        SetStatus("Creando lobby...");
        try
        {
            // 1) Allocation Relay (host) - maxConnections = maxPlayers - 1 (clientes)
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // 2) Crear Lobby e incluir joinCode en Data
            var createOpts = new CreateLobbyOptions
            {
                IsPrivate = isPrivate,
                Data = new Dictionary<string, DataObject>
                {
                    { "joinCode", new DataObject(DataObject.VisibilityOptions.Member, joinCode) },
                    { "hostName", new DataObject(DataObject.VisibilityOptions.Public, CloudAuthManager.Instance?.GetPlayerName() ?? "Host") }
                },
                Player = new Unity.Services.Lobbies.Models.Player
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        { "name", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, CloudAuthManager.Instance?.GetPlayerName() ?? "Host") }
                    }
                }
            };

            _currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, createOpts);
            SetStatus($"Lobby creado: {_currentLobby.Name}");
            if (joinCodeLabel) joinCodeLabel.text = $"Código: {joinCode}";

            // 3) Configurar UnityTransport con Relay (host) y arrancar Host
            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            var connType = GetConnType();
            RelayServerData relayData = AllocationUtils.ToRelayServerData(allocation, connType);
            utp.SetRelayServerData(relayData);


            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

            // IMPORTANTE: spawnear tu GameManager en el host (como hacías antes)
            NetworkManager.Singleton.StartHost();
            if (gameManagerPrefab != null)
            {
                var go = Instantiate(gameManagerPrefab);
                go.GetComponent<NetworkObject>().Spawn();
            }

            // 4) Mantener vivo el lobby (heartbeat)
            _heartbeatRoutine = StartCoroutine(HeartbeatCoroutine());

            // 5) Oculta panel de conexión (opcional, tu flujo)
            if (connectionPanel) connectionPanel.SetActive(false);

            SetStatus($"Host listo. Comparte el código: {joinCode}");
        }
        catch (Exception e)
        {
            SetStatus($"Error al crear lobby: {e.Message}");
            Debug.LogException(e);
        }
    }

    public async void OnJoinByCodeClicked()
    {
        await EnsureServices();
        string code = (joinCodeInput != null) ? joinCodeInput.text.Trim() : "";
        if (string.IsNullOrEmpty(code))
        {
            SetStatus("Ingresa un código de lobby.");
            return;
        }

        SetStatus("Uniéndose por código...");
        try
        {
            _currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(code, new JoinLobbyByCodeOptions
            {
                Player = new Unity.Services.Lobbies.Models.Player
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        { "name", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, CloudAuthManager.Instance?.GetPlayerName() ?? "Client") }
                    }
                }
            });

            // Obtener el joinCode de Relay desde los datos del lobby (miembro/host lo escribió)
            string relayJoinCode = _currentLobby.Data.ContainsKey("joinCode") ? _currentLobby.Data["joinCode"].Value : null;
            if (string.IsNullOrEmpty(relayJoinCode))
            {
                throw new Exception("El lobby no tiene joinCode de Relay.");
            }

            // Join Allocation como cliente
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);

            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            var connType = GetConnType();
            RelayServerData relayData = AllocationUtils.ToRelayServerData(joinAllocation, connType);
            utp.SetRelayServerData(relayData);


            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

            NetworkManager.Singleton.StartClient();

            // Oculta panel de conexión (opcional)
            if (connectionPanel) connectionPanel.SetActive(false);
            SetStatus("Conectando al host...");
        }
        catch (Exception e)
        {
            SetStatus($"Error al unirse: {e.Message}");
            Debug.LogException(e);
        }
    }

    public async void OnQuickJoinClicked()
    {
        await EnsureServices();
        SetStatus("Buscando lobby disponible...");
        try
        {
            // Criterios simples; puedes filtrar por región o data si quieres
            _currentLobby = await LobbyService.Instance.QuickJoinLobbyAsync(new QuickJoinLobbyOptions
            {
                Player = new Unity.Services.Lobbies.Models.Player
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        { "name", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, CloudAuthManager.Instance?.GetPlayerName() ?? "Client") }
                    }
                }
            });

            string relayJoinCode = _currentLobby.Data.ContainsKey("joinCode") ? _currentLobby.Data["joinCode"].Value : null;
            if (string.IsNullOrEmpty(relayJoinCode))
            {
                throw new Exception("El lobby no publicó joinCode de Relay.");
            }

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);

            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            var connType = GetConnType();
            RelayServerData relayData = AllocationUtils.ToRelayServerData(joinAllocation, connType);
            utp.SetRelayServerData(relayData);

            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

            NetworkManager.Singleton.StartClient();

            if (connectionPanel) connectionPanel.SetActive(false);
            SetStatus("Conectando al host (quick join)...");
        }
        catch (Exception e)
        {
            SetStatus($"Quick Join falló: {e.Message}");
            Debug.LogException(e);
        }
    }

    public async void OnLeaveLobbyClicked()
    {
        try
        {
            if (_heartbeatRoutine != null) StopCoroutine(_heartbeatRoutine);
            _heartbeatRoutine = null;

            if (_currentLobby != null)
            {
                // Si eres host y quieres cerrar lobby:
                if (_currentLobby.HostId == AuthenticationService.Instance.PlayerId)
                    await LobbyService.Instance.DeleteLobbyAsync(_currentLobby.Id);
                else
                    await LobbyService.Instance.RemovePlayerAsync(_currentLobby.Id, AuthenticationService.Instance.PlayerId);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Error al salir del lobby: {e.Message}");
        }
        finally
        {
            _currentLobby = null;
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient)
                NetworkManager.Singleton.Shutdown();

            if (connectionPanel) connectionPanel.SetActive(true);
            if (joinCodeLabel) joinCodeLabel.text = "Código: —";
            SetStatus("Saliste del lobby.");
        }
    }

    private IEnumerator HeartbeatCoroutine()
    {
        while (_currentLobby != null && _currentLobby.HostId == AuthenticationService.Instance.PlayerId)
        {
            LobbyService.Instance.SendHeartbeatPingAsync(_currentLobby.Id);
            yield return new WaitForSeconds(HEARTBEAT_INTERVAL);
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId) return;

        SetStatus("Conectado ✅");

        // Notifica al servidor tu nombre de jugador (idempotente con tu lógica actual)
        var playerName = CloudAuthManager.Instance != null ? CloudAuthManager.Instance.GetPlayerName() : $"Player_{clientId}";

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerAuthenticatedServerRpc(playerName, NetworkManager.Singleton.LocalClientId);
        }

    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            SetStatus("Desconectado.");
            if (connectionPanel) connectionPanel.SetActive(true);
        }
    }

    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
        Debug.Log($"[RelayLobbyConnector] {msg}");
    }

    private void OnDestroy()
    {
        NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        if (_heartbeatRoutine != null) StopCoroutine(_heartbeatRoutine);
    }
    private static string GetConnType()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
    return "wss";
#else
        return "dtls";
#endif
    }

}
