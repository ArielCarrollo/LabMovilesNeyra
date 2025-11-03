using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUIManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private TextMeshProUGUI readyButtonText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI readyCountText;
    [SerializeField] private TextMeshProUGUI lobbyCodeText; // Para mostrar el código
    [Header("Host Controls")]
    [SerializeField] private Button closeLobbyButton;

    [Header("Player List")]
    [SerializeField] private Transform playerListContent;
    [SerializeField] private GameObject playerCardPrefab;
    private Dictionary<ulong, GameObject> playerCardInstances = new Dictionary<ulong, GameObject>();

    [Header("Chat")]
    [SerializeField] private TMP_InputField chatInputField;
    [SerializeField] private Transform chatMessagesContainer;
    [SerializeField] private GameObject chatMessagePrefab;
    [SerializeField] private ScrollRect chatScroll;

    [Header("Colors")]
    [SerializeField] private Color readyColor = Color.green;
    [SerializeField] private Color notReadyColor = Color.white;

    [Header("Customization")]
    [SerializeField] private PlayerAppearance previewPlayer;
    [SerializeField] private Button nextBodyButton;
    [SerializeField] private Button prevBodyButton;
    [SerializeField] private Button nextEyesButton;
    [SerializeField] private Button prevEyesButton;
    [SerializeField] private Button nextGlovesButton;
    [SerializeField] private Button prevGlovesButton;
    private PlayerData localCustomData;


    [Header("Player Name Management")]
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TMP_InputField nameChangeInputField;
    [SerializeField] private Button saveNameButton;

    [Header("Player Progression")]
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Slider xpBar;
    
    private bool isInitialized = false;

    // --- Inicialización y Suscripciones ---

    public void Initialize()
    {
        if (isInitialized) return;

        Debug.Log("LobbyUIManager: Inicializando.");

        lobbyPanel.SetActive(true);

        if (GameManager.Instance != null && GameManager.Instance.PlayersInLobby != null)
        {
            GameManager.Instance.PlayersInLobby.OnListChanged += HandlePlayerListChanged;
        }
        else
        {
            Debug.LogError("GameManager o PlayersInLobby es nulo durante la inicialización.");
            statusText.text = "Error al conectar con GameManager.";
            return;
        }

        // botones
        startGameButton.onClick.AddListener(OnStartGameClicked);
        readyButton.onClick.AddListener(OnReadyClicked);
        saveNameButton.onClick.AddListener(OnSaveNameClicked);

        nextBodyButton.onClick.AddListener(() => OnChangeAppearance(0, 1));
        prevBodyButton.onClick.AddListener(() => OnChangeAppearance(0, -1));
        nextEyesButton.onClick.AddListener(() => OnChangeAppearance(1, 1));
        prevEyesButton.onClick.AddListener(() => OnChangeAppearance(1, -1));
        nextGlovesButton.onClick.AddListener(() => OnChangeAppearance(2, 1));
        prevGlovesButton.onClick.AddListener(() => OnChangeAppearance(2, -1));
        if (closeLobbyButton != null)
        {
            bool iAmHost = NetworkManager.Singleton.IsHost;
            closeLobbyButton.gameObject.SetActive(iAmHost);
            if (iAmHost)
                closeLobbyButton.onClick.AddListener(OnCloseLobbyClicked);
        }
        if (CloudAuthManager.Instance != null)
        {
            CloudAuthManager.Instance.OnPlayerNameUpdated += HandlePlayerNameUpdated;
        }

        // 1) leer lo que tenemos del auth / cloud
        LoadLocalPlayerData();  // aquí puede venir “bien” pero el server quizá guardó vacío

        // 2) marcamos como listo
        isInitialized = true;

        // 3) 👉 forzar al SERVER a tener este nombre (esto es lo que te faltaba)
        if (GameManager.Instance != null)
        {
            string myName = localCustomData.Username.ToString();
            if (!string.IsNullOrWhiteSpace(myName))
            {
                // esto escribe de nuevo en la NetworkList del server
                GameManager.Instance.UpdatePlayerNameServerRpc(myName);
            }
        }
        if (VivoxLobbyChatManager.Instance != null)
        {
            VivoxLobbyChatManager.Instance.OnTextMessage -= HandleChatMessage; // por si acaso
            VivoxLobbyChatManager.Instance.OnTextMessage += HandleChatMessage;
        }
        if (chatInputField != null)
        {
            chatInputField.onSubmit?.RemoveAllListeners();
            chatInputField.onEndEdit.AddListener(OnChatSubmit);
        }
        // 4) 👉 y como la NetworkList puede llegar 1–2 frames después, refrescamos con un pequeño wait
        StartCoroutine(InitialListRefresh());

        Debug.Log("LobbyUIManager: Inicialización completa.");
    }

    private IEnumerator InitialListRefresh()
    {
        float timeout = 2.5f;
        // esperamos a que el cliente YA tenga los datos que el server metió en la NetworkList
        while (timeout > 0f)
        {
            if (GameManager.Instance != null &&
                GameManager.Instance.PlayersInLobby != null &&
                GameManager.Instance.PlayersInLobby.Count > 0)
            {
                HandlePlayerListChanged(new NetworkListEvent<PlayerData>());
                yield break;
            }
            timeout -= Time.deltaTime;
            yield return null;
        }

        // último intento aunque siga vacía
        HandlePlayerListChanged(new NetworkListEvent<PlayerData>());
    }


    private void OnDisable()
    {
        if (GameManager.Instance != null && GameManager.Instance.PlayersInLobby != null)
        {
            GameManager.Instance.PlayersInLobby.OnListChanged -= HandlePlayerListChanged;
        }
        
        if (CloudAuthManager.Instance != null)
        {
            CloudAuthManager.Instance.OnPlayerNameUpdated -= HandlePlayerNameUpdated;
        }
        if (VivoxLobbyChatManager.Instance != null)
        {
            VivoxLobbyChatManager.Instance.OnTextMessage -= HandleChatMessage;
        }
        foreach (var card in playerCardInstances.Values)
        {
            Destroy(card);
        }
        playerCardInstances.Clear();
        
        isInitialized = false; 
    }

    // --- Carga de Datos y Sincronización de UI ---

    private void LoadLocalPlayerData()
    {
        if (CloudAuthManager.Instance == null)
        {
            Debug.LogError("CloudAuthManager no tiene datos de jugador local.");
            localCustomData = new PlayerData(0, "Jugador");
            UpdateNameAndLevelUI(localCustomData);
            return;
        }

        localCustomData = CloudAuthManager.Instance.LocalPlayerData;

        // 👇 si vino vacío por la carga del JSON, usemos el nombre real del Auth
        if (localCustomData.Username.Length == 0)
        {
            string authName = CloudAuthManager.Instance.GetPlayerName();
            if (!string.IsNullOrWhiteSpace(authName))
            {
                localCustomData.Username = new FixedString64Bytes(authName);
                CloudAuthManager.Instance.UpdateLocalData(localCustomData);
            }
        }

        UpdateNameAndLevelUI(localCustomData);

        if (previewPlayer != null)
        {
            previewPlayer.ApplyAppearance(localCustomData);
        }
    }


    private void HandlePlayerListChanged(NetworkListEvent<PlayerData> changeEvent)
    {
        if (GameManager.Instance == null || !isInitialized)
        {
            if (!isInitialized) Debug.LogWarning("HandlePlayerListChanged llamado antes de inicializar.");
            return;
        }

        Debug.Log("LobbyUIManager: Actualizando lista de jugadores...");

        // 1. recolectar ids actuales
        List<ulong> currentIds = new List<ulong>();
        foreach (var player in GameManager.Instance.PlayersInLobby)
        {
            currentIds.Add(player.ClientId);
        }

        // 2. borrar los que ya no están
        List<ulong> idsToRemove = new List<ulong>();
        foreach (var kvp in playerCardInstances)
        {
            if (!currentIds.Contains(kvp.Key))
            {
                Destroy(kvp.Value);
                idsToRemove.Add(kvp.Key);
            }
        }
        foreach (var id in idsToRemove)
            playerCardInstances.Remove(id);

        // 3. volver a crear / actualizar
        int readyCount = 0;
        bool localPlayerFound = false;
        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        PlayerData localPlayerData = default;

        foreach (var player in GameManager.Instance.PlayersInLobby)
        {
            // crear card si no existe
            if (!playerCardInstances.TryGetValue(player.ClientId, out var cardInstance))
            {
                // ✅ usar el que SÍ tienes en el inspector
                cardInstance = Instantiate(playerCardPrefab, playerListContent);
                playerCardInstances[player.ClientId] = cardInstance;
            }

            UpdatePlayerCard(cardInstance, player);

            if (player.IsReady)
                readyCount++;

            if (player.ClientId == localClientId)
            {
                localPlayerFound = true;
                localPlayerData = player;

                // solo si el server sí nos mandó nombre
                if (player.Username.Length > 0)
                {
                    localCustomData = player;
                    UpdateNameAndLevelUI(localCustomData);
                }
            }
        }


        UpdateLobbyControls(localPlayerData, readyCount, localPlayerFound);
    }


    private string SanitizeName(FixedString64Bytes fs, ulong clientId)
    {
        string s = fs.ToString();
        if (!string.IsNullOrEmpty(s))
            s = s.Replace("\0", string.Empty);   // 👈 quitar nulos

        if (string.IsNullOrWhiteSpace(s))
            s = $"Player_{clientId}";

        return s;
    }

    private void UpdatePlayerCard(GameObject cardInstance, PlayerData player)
    {
        // 1. nombre (como ya lo tenías)
        TextMeshProUGUI nameText = cardInstance.transform
            .Find("PlayerNameText")?.GetComponent<TextMeshProUGUI>();

        if (nameText == null)
            nameText = cardInstance.GetComponentInChildren<TextMeshProUGUI>(true);

        string nick = SanitizeName(player.Username, player.ClientId);

        if (nameText != null)
            nameText.text = nick;

        // 2. color ready
        var panelImage = cardInstance.GetComponent<Image>();
        if (panelImage != null)
            panelImage.color = player.IsReady ? readyColor : notReadyColor;

        // 3. icono de host
        var hostIcon = cardInstance.transform.Find("HostIcon")?.gameObject;
        if (hostIcon != null)
            hostIcon.SetActive(player.ClientId == NetworkManager.ServerClientId);

        // 4. 🟡 BOTÓN KICK (solo host, y no sobre el host)
        var kickBtnTransform = cardInstance.transform.Find("KickButton");
        if (kickBtnTransform != null)
        {
            var kickBtn = kickBtnTransform.GetComponent<Button>();
            if (kickBtn != null)
            {
                bool iAmHost = NetworkManager.Singleton.IsHost;
                bool isThisTheHost = (player.ClientId == NetworkManager.ServerClientId);

                // mostrar solo si soy host y no es el host
                kickBtn.gameObject.SetActive(iAmHost && !isThisTheHost);

                // limpiar listeners anteriores, porque reutilizas las cards
                kickBtn.onClick.RemoveAllListeners();

                if (iAmHost && !isThisTheHost)
                {
                    ulong targetId = player.ClientId;
                    kickBtn.onClick.AddListener(() => OnKickPlayerClicked(targetId));
                }
            }
        }
    }




    private void UpdateLobbyControls(PlayerData localPlayer, int readyCount, bool localPlayerFound)
    {
        if (GameManager.Instance == null) return;
        
        bool allPlayersReady = (readyCount == GameManager.Instance.PlayersInLobby.Count) && (GameManager.Instance.PlayersInLobby.Count > 0);

        readyCountText.text = $"{readyCount} / {GameManager.Instance.PlayersInLobby.Count}";

        if (localPlayerFound)
        {
            readyButtonText.text = localPlayer.IsReady ? "No Listo" : "Listo";
        }
        else
        {
            readyButtonText.text = "Listo"; 
        }
        
        startGameButton.gameObject.SetActive(NetworkManager.Singleton.IsHost);
        startGameButton.interactable = allPlayersReady;
    }
    private void UpdateNameAndLevelUI(PlayerData data)
    {
        string nick = SanitizeName(data.Username,
            NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0);

        playerNameText.text = nick;
        nameChangeInputField.text = nick;

        if (GameManager.Instance == null) return;

        int xpNeededForNextLevel = GameManager.Instance.GetXpForLevel(data.Level);
        levelText.text = $"Nivel {data.Level} | ({data.CurrentXP} / {xpNeededForNextLevel} XP)";
        xpBar.maxValue = xpNeededForNextLevel;
        xpBar.value = data.CurrentXP;
    }


    // --- Handlers de Eventos de UI ---

    private void OnStartGameClicked()
    {
        if (GameManager.Instance != null && NetworkManager.Singleton.IsHost)
        {
            GameManager.Instance.StartGameServerRpc();
        }
    }

    private void OnReadyClicked()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ToggleReadyServerRpc();
        }
    }

    private async void OnSaveNameClicked()
    {
        string newName = nameChangeInputField.text;
        if (string.IsNullOrWhiteSpace(newName) || newName.Length < 3)
        {
            Debug.LogError("El nombre debe tener al menos 3 caracteres.");
            return;
        }

        saveNameButton.interactable = false;
        await CloudAuthManager.Instance.UpdatePlayerNameAsync(newName);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.UpdatePlayerNameServerRpc(newName);
        }
        saveNameButton.interactable = true;
    }

    private void HandlePlayerNameUpdated(string newName)
    {
        playerNameText.text = newName;
        localCustomData.Username = new FixedString64Bytes(newName);
        CloudAuthManager.Instance.UpdateLocalData(localCustomData);
    }
    
    // --- Lógica de Personalización ---

    private void OnChangeAppearance(int type, int direction)
    {
        if (previewPlayer == null) return;
        
        // (Asegúrate de que PlayerAppearance tenga estos métodos públicos Get...Count)
        int maxBody = previewPlayer.GetBodyCount();
        int maxEyes = previewPlayer.GetEyesCount();
        int maxGloves = previewPlayer.GetGlovesCount();
        
        if (maxBody == 0 && type == 0) return;
        if (maxEyes == 0 && type == 1) return;
        if (maxGloves == 0 && type == 2) return;

        switch (type)
        {
            case 0: // Body
                localCustomData.BodyIndex = (localCustomData.BodyIndex + direction + maxBody) % maxBody;
                break;
            case 1: // Eyes
                localCustomData.EyesIndex = (localCustomData.EyesIndex + direction + maxEyes) % maxEyes;
                break;
            case 2: // Gloves
                localCustomData.GlovesIndex = (localCustomData.GlovesIndex + direction + maxGloves) % maxGloves;
                break;
        }
        
        previewPlayer.ApplyAppearance(localCustomData);
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.UpdatePlayerAppearanceServerRpc(localCustomData);
        }

        if (CloudAuthManager.Instance != null)
        {
            CloudAuthManager.Instance.UpdateLocalData(localCustomData);
            _ = CloudAuthManager.Instance.SavePlayerProgress(); 
        }
    }
    private void OnKickPlayerClicked(ulong targetClientId)
    {
        if (GameManager.Instance == null) return;
        if (!NetworkManager.Singleton.IsHost) return;

        GameManager.Instance.KickPlayerServerRpc(targetClientId);
    }
    private void OnCloseLobbyClicked()
    {
        if (!NetworkManager.Singleton.IsHost) return;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CloseLobbyServerRpc();
        }

        // UI local del host (por si el RPC tarda un frame)
        var relay = FindObjectOfType<RelayLobbyConnector>();
        if (relay != null)
        {
            relay.ShowJoiningPanel();
        }

        if (UiGameManager.Instance != null)
        {
            UiGameManager.Instance.GoToLobbySelection();
        }
    }
    private void OnChatSubmit(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        SendChat(text);
        chatInputField.text = string.Empty;
        chatInputField.ActivateInputField();
    }

    private async void SendChat(string text)
    {
        if (VivoxLobbyChatManager.Instance != null)
        {
            await VivoxLobbyChatManager.Instance.SendTextMessage(text);
        }
        else
        {
            // fallback local, por si vivox no está
            HandleChatMessage("Local", text, NetworkManager.Singleton.IsHost);
        }
    }

    private void HandleChatMessage(string sender, string message, bool isHost)
    {
        if (chatMessagePrefab == null || chatMessagesContainer == null)
        {
            Debug.LogWarning("Chat UI no configurado en LobbyUIManager.");
            return;
        }

        GameObject msgGO = Instantiate(chatMessagePrefab, chatMessagesContainer);
        var txt = msgGO.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null)
        {
            if (isHost)
                txt.text = $"<b>{sender} [HOST]:</b> {message}";
            else
                txt.text = $"<b>{sender}:</b> {message}";
        }

        if (chatScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            chatScroll.verticalNormalizedPosition = 0f;
        }
    }


}