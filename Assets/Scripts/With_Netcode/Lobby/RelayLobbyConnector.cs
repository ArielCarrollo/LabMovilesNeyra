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
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gestiona la conexión al Lobby y Relay de Unity, adaptado 
/// para una estructura de UI con múltiples paneles.
/// CORREGIDO para UTP antiguo y FixedString.
/// </summary>
public class RelayLobbyConnector : MonoBehaviour
{
    [Header("Panel References")]
    [Tooltip("El GameObject padre que contiene TODOS los paneles de selección de lobby")]
    [SerializeField] private GameObject rootLobbySelectionPanel;
    [SerializeField] private GameObject joiningPanel;
    [SerializeField] private GameObject createLobbyPanel;
    [SerializeField] private GameObject joinByCodePanel;
    [SerializeField] private GameObject manualJoinPanel;

    [Header("Create Lobby UI")]
    [SerializeField] private TMP_InputField lobbyNameInput;
    [SerializeField] private bool isPrivate = false; 

    [Header("Join By Code UI")]
    [SerializeField] private TMP_InputField joinCodeInput;

    [Header("Manual Join UI")]
    [SerializeField] private Transform lobbyListContainer; 
    [SerializeField] private GameObject lobbyButtonPrefab; 
    
    private Task _lobbyRefreshTask;
    private bool _isRefreshingLobbies = false;
    private const float LOBBY_REFRESH_INTERVAL = 5.0f; 

    [Header("General UI")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI joinCodeLabel; 

    [Header("Lobby Config")]
    [SerializeField] private int maxPlayers = 5;

    [Header("Network Prefabs")]
    [SerializeField] private GameObject gameManagerPrefab;

    private Lobby _currentLobby;
    private Coroutine _heartbeatRoutine;

    // --- Inicialización y Flujo de UI ---

    private void Start()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

        ShowJoiningPanel();

        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            Debug.LogWarning("Unity Services aún no inicializados (ok, te logueas en esta escena). Continuamos y esperaremos al login.");
        }
    }


    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
        StopHeartbeat();
        StopLobbyRefresh();
    }

    // --- Métodos Públicos (para botones de UI) ---

    public void ShowJoiningPanel()
    {
        if (joiningPanel) joiningPanel.SetActive(true);
        if (createLobbyPanel) createLobbyPanel.SetActive(false);
        if (joinByCodePanel) joinByCodePanel.SetActive(false);
        if (manualJoinPanel) manualJoinPanel.SetActive(false);
        StopLobbyRefresh();
    }

    public void ShowCreateLobbyPanel()
    {
        if (joiningPanel) joiningPanel.SetActive(false);
        if (createLobbyPanel) createLobbyPanel.SetActive(true);
    }

    public void ShowJoinByCodePanel()
    {
        if (joiningPanel) joiningPanel.SetActive(false);
        if (joinByCodePanel) joinByCodePanel.SetActive(true);
    }

    public void ShowManualJoinPanel()
    {
        if (joiningPanel) joiningPanel.SetActive(false);
        if (manualJoinPanel) manualJoinPanel.SetActive(true);
        StartLobbyRefresh();
    }

    // --- Lógica de Creación de Lobby ---

    public async void OnCreateLobbyClicked()
    {
        string lobbyName = lobbyNameInput.text;
        if (string.IsNullOrWhiteSpace(lobbyName))
        {
            SetStatus("Error: El nombre del lobby no puede estar vacío.");
            return;
        }

        SetStatus("Creando lobby...");
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            SetStatus($"Código de Relay: {joinCode}");

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetHostRelayData(
                allocation.RelayServer.IpV4, (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData
            );

            var createOptions = new CreateLobbyOptions
            {
                IsPrivate = isPrivate,
                Data = new Dictionary<string, DataObject>
            {
                { "JoinCodeKey", new DataObject(DataObject.VisibilityOptions.Member, joinCode) }
            },
                Player = GetHostPlayer()
            };

            _currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, createOptions);
            StartHeartbeat();

            NetworkManager.Singleton.StartHost();

            if (gameManagerPrefab != null)
            {
                var gm = Instantiate(gameManagerPrefab);
                var no = gm.GetComponent<NetworkObject>();
                no.Spawn(); 
            }
            else
            {
                Debug.LogError("gameManagerPrefab no está asignado en el Inspector!");
                SetStatus("Error: Prefab de GameManager no encontrado.");
                return;
            }

            SetStatus($"Lobby creado! Código: {_currentLobby.LobbyCode}");
            if (joinCodeLabel) joinCodeLabel.text = $"Código: {_currentLobby.LobbyCode}";
        }
        catch (Exception e)
        {
            SetStatus("Error al crear el lobby.");
            Debug.LogError(e);
        }
    }


    // --- Lógica de Unión a Lobby (Refactorizada) ---

    public async void OnJoinByCodeClicked()
    {
        string code = joinCodeInput.text;
        if (string.IsNullOrWhiteSpace(code))
        {
            SetStatus("Error: El código no puede estar vacío.");
            return;
        }

        SetStatus("Uniéndose por código...");
        try
        {
            var options = new JoinLobbyByCodeOptions { Player = GetClientPlayer() };
            Lobby joinedLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(code, options);
            await JoinLobbyAndConnect(joinedLobby);
        }
        catch (LobbyServiceException e)
        {
            SetStatus("Error al unirse por código.");
            Debug.LogError(e);
        }
    }

    public async void OnQuickJoinClicked()
    {
        SetStatus("Buscando lobby rápido...");
        try
        {
            var options = new QuickJoinLobbyOptions
            {
                Filter = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                },
                Player = GetClientPlayer()
            };
            Lobby joinedLobby = await LobbyService.Instance.QuickJoinLobbyAsync(options);
            await JoinLobbyAndConnect(joinedLobby);
        }
        catch (LobbyServiceException e)
        {
            SetStatus("No se encontraron lobbies disponibles.");
            Debug.LogError(e);
        }
    }

    private async Task OnJoinByLobbyIdClicked(string lobbyId)
    {
        SetStatus("Uniéndose a lobby seleccionado...");
        try
        {
            var options = new JoinLobbyByIdOptions { Player = GetClientPlayer() };
            Lobby joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, options);
            await JoinLobbyAndConnect(joinedLobby);
        }
        catch (LobbyServiceException e)
        {
            SetStatus("Error al unirse a ese lobby.");
            Debug.LogError(e);
        }
    }

    private async Task JoinLobbyAndConnect(Lobby lobby)
    {
        try
        {
            _currentLobby = lobby;
            SetStatus("Conectando con Relay...");

            string joinCode = _currentLobby.Data["JoinCodeKey"].Value;

            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetClientRelayData(
                allocation.RelayServer.IpV4, (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes, allocation.Key,
                allocation.ConnectionData, allocation.HostConnectionData
            );

            NetworkManager.Singleton.StartClient();
        }
        catch (Exception e)
        {
            SetStatus("Error al conectar con Relay.");
            Debug.LogError(e);
            _currentLobby = null;
        }
    }

    // --- Lógica de Lista de Lobbies (Manual Join) ---

    private void StartLobbyRefresh()
    {
        if (!_isRefreshingLobbies)
        {
            _isRefreshingLobbies = true;
            _lobbyRefreshTask = RefreshLobbyListRoutine();
        }
    }

    private void StopLobbyRefresh()
    {
        _isRefreshingLobbies = false;
    }
    
    private async Task RefreshLobbyListRoutine()
    {
        while (_isRefreshingLobbies)
        {
            await RefreshLobbyList();
            await Task.Delay((int)(LOBBY_REFRESH_INTERVAL * 1000));
        }
    }

    private async Task RefreshLobbyList()
    {
        if (lobbyListContainer == null || lobbyButtonPrefab == null)
        {
            Debug.LogWarning("No se ha configurado el contenedor o prefab de la lista de lobbies.");
            return; 
        }

        foreach (Transform child in lobbyListContainer)
        {
            Destroy(child.gameObject);
        }

        List<Lobby> lobbies = await QueryLobbiesAsync();

        foreach (Lobby lobby in lobbies)
        {
            GameObject buttonGO = Instantiate(lobbyButtonPrefab, lobbyListContainer);
            
            var buttonText = buttonGO.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = $"{lobby.Name} ({lobby.Players.Count}/{lobby.MaxPlayers})";
            }

            var button = buttonGO.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(() => HandleJoinFromButton(lobby.Id));
            }
        }
    }

    private void HandleJoinFromButton(string lobbyId)
    {
        _ = OnJoinByLobbyIdClicked(lobbyId);
    }


    private async Task<List<Lobby>> QueryLobbiesAsync()
    {
        try
        {
            var options = new QueryLobbiesOptions
            {
                Count = 20, 
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                },
                Order = new List<QueryOrder>
                {
                    new QueryOrder(false, QueryOrder.FieldOptions.Created)
                }
            };

            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);
            return response.Results;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Error al consultar lobbies: {e}");
            return new List<Lobby>();
        }
    }


    // --- Callbacks y Helpers ---

    private void HandleClientConnected(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId) return;

        SetStatus("Conectado ✅");
        
        StartCoroutine(DelayedAuthentication());
    }

    private IEnumerator DelayedAuthentication()
    {
        float timeout = 5f;
        while (GameManager.Instance == null && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        if (GameManager.Instance == null)
        {
            Debug.LogError("DelayedAuthentication: GameManager.Instance nunca apareció.");
            yield break;
        }

        timeout = 5f;
        while (CloudAuthManager.Instance == null && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        if (CloudAuthManager.Instance == null)
        {
            Debug.LogError("DelayedAuthentication: no hay CloudAuthManager.");
            yield break;
        }

        PlayerData dataToSend = CloudAuthManager.Instance.LocalPlayerData;

        string cleanName = dataToSend.Username.ToString();
        if (!string.IsNullOrEmpty(cleanName))
            cleanName = cleanName.Replace("\0", string.Empty);

        if (string.IsNullOrWhiteSpace(cleanName))
        {
            string authName = CloudAuthManager.Instance.GetPlayerName();
            if (!string.IsNullOrWhiteSpace(authName))
                cleanName = authName;
        }

        if (string.IsNullOrWhiteSpace(cleanName))
        {
            cleanName = $"Player_{NetworkManager.Singleton.LocalClientId}";
        }

        dataToSend.Username = new Unity.Collections.FixedString64Bytes(cleanName);

        CloudAuthManager.Instance.UpdateLocalData(dataToSend);

        GameManager.Instance.OnPlayerAuthenticatedServerRpc(dataToSend);
    }



    private void HandleClientDisconnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            SetStatus("Desconectado.");
            _currentLobby = null;
            StopHeartbeat();
            
            if (UiGameManager.Instance != null)
            {
                if (rootLobbySelectionPanel) rootLobbySelectionPanel.SetActive(true);
                ShowJoiningPanel();
            }
        }
    }

    private Player GetHostPlayer()
    {
        return new Player
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, CloudAuthManager.Instance.GetPlayerName()) }
            }
        };
    }

    private Player GetClientPlayer()
    {
        return GetHostPlayer();
    }

    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
        Debug.Log($"[RelayLobbyConnector] {msg}");
    }

private void StartHeartbeat()
    {
        if (_heartbeatRoutine != null) StopCoroutine(_heartbeatRoutine);
        _heartbeatRoutine = StartCoroutine(HeartbeatLobbyCoroutine(_currentLobby.Id, 15f));
    }

    private void StopHeartbeat()
    {
        if (_heartbeatRoutine != null)
        {
            StopCoroutine(_heartbeatRoutine);
            _heartbeatRoutine = null;
        }
    }

    private IEnumerator HeartbeatLobbyCoroutine(string lobbyId, float waitTimeSeconds)
    {
        while (true)
        {
            yield return new WaitForSeconds(waitTimeSeconds);
            try
            {
                if (_currentLobby != null)
                {
                    LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
                }
            }
            catch (LobbyServiceException e)
            {
                Debug.Log($"Error en Heartbeat (probablemente el host cerró): {e}");
                _currentLobby = null;
                yield break;
            }
        }
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