using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

public class LobbyUIManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private TextMeshProUGUI readyButtonText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI readyCountText;
    [SerializeField] private TextMeshProUGUI lobbyCodeText;

    [Header("Host Controls")]
    [SerializeField] private Button closeLobbyButton;

    [Header("Player List")]
    [SerializeField] private Transform playerListContent;
    [SerializeField] private GameObject playerCardPrefab;
    private Dictionary<ulong, GameObject> playerCardInstances = new Dictionary<ulong, GameObject>();

    [Header("Chat (público)")]
    [SerializeField] private GameObject publicChatPanel;
    [SerializeField] private TMP_InputField chatInputField;
    [SerializeField] private Transform chatMessagesContainer;
    [SerializeField] private GameObject chatMessagePrefab;
    [SerializeField] private ScrollRect chatScroll;

    [Header("Chat privado")]
    [SerializeField] private GameObject privateChatPanel;
    [SerializeField] private Transform privatePlayersContainer;
    [SerializeField] private GameObject privatePlayerButtonPrefab;
    [SerializeField] private Transform privateMessagesContainer;
    [SerializeField] private GameObject privateMessagePrefab;
    [SerializeField] private TMP_InputField privateChatInputField;
    [SerializeField] private Color privateUnreadColor = Color.yellow;

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

    // ---- Estructuras de chat ----
    // público
    [System.Serializable]
    private class PublicChatMessage
    {
        public string sender;
        public string text;
        public bool isHost;
    }
    private List<PublicChatMessage> publicChatHistory = new List<PublicChatMessage>();

    // privado
    private class PrivateEntry
    {
        public string playerId;        // id de Unity Services (para DM real)
        public ulong clientId;         // para fallback desde GameManager
        public GameObject go;
        public Image bg;
        public bool hasUnread;
        public Color baseColor;
        public string displayName;
    }
    private Dictionary<string, PrivateEntry> privateEntries = new Dictionary<string, PrivateEntry>();

    [System.Serializable]
    private class PrivateChatMessage
    {
        public string sender;
        public string text;
    }
    // clave: "pid:<playerId>" o "cid:<clientId>"
    private Dictionary<string, List<PrivateChatMessage>> privateChatHistory = new Dictionary<string, List<PrivateChatMessage>>();

    // canal privado abierto actualmente
    private string currentPrivateTargetPlayerId = null;
    private ulong currentPrivateTargetClientId = 0;

    public void Initialize()
    {
        if (isInitialized) return;

        lobbyPanel.SetActive(true);

        if (GameManager.Instance != null && GameManager.Instance.PlayersInLobby != null)
        {
            GameManager.Instance.PlayersInLobby.OnListChanged += HandlePlayerListChanged;
        }
        else
        {
            statusText.text = "Error al conectar con GameManager.";
            return;
        }

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

        LoadLocalPlayerData();
        isInitialized = true;

        if (GameManager.Instance != null)
        {
            string myName = localCustomData.Username.ToString();
            if (!string.IsNullOrWhiteSpace(myName))
            {
                GameManager.Instance.UpdatePlayerNameServerRpc(myName);
            }
        }

        // enganchar chat vivox
        if (VivoxLobbyChatManager.Instance != null)
        {
            VivoxLobbyChatManager.Instance.OnTextMessage -= HandleChatMessage;
            VivoxLobbyChatManager.Instance.OnTextMessage += HandleChatMessage;

            VivoxLobbyChatManager.Instance.OnDirectMessage -= HandleDirectMessage;
            VivoxLobbyChatManager.Instance.OnDirectMessage += HandleDirectMessage;
        }

        if (chatInputField != null)
        {
            chatInputField.onSubmit?.RemoveAllListeners();
            chatInputField.onEndEdit.AddListener(OnChatSubmit);
        }

        if (privateChatInputField != null)
        {
            privateChatInputField.onSubmit?.RemoveAllListeners();
            privateChatInputField.onEndEdit.AddListener(OnPrivateChatSubmit);
        }

        ShowPublicChatPanel(); // al entrar pintamos el historial público
        StartCoroutine(InitialListRefresh());
    }

    private IEnumerator InitialListRefresh()
    {
        float timeout = 2.5f;
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
            VivoxLobbyChatManager.Instance.OnDirectMessage -= HandleDirectMessage;
        }

        foreach (var card in playerCardInstances.Values)
        {
            Destroy(card);
        }
        playerCardInstances.Clear();
        privateEntries.Clear();

        isInitialized = false;
    }

    private void Update()
    {
        // parpadeo de los que tienen mensajes sin leer
        if (privateEntries.Count > 0 && privatePlayersContainer != null && privateChatPanel != null && privateChatPanel.activeSelf)
        {
            float t = Mathf.PingPong(Time.unscaledTime * 3.5f, 1f);
            foreach (var kvp in privateEntries)
            {
                var entry = kvp.Value;
                if (entry.hasUnread && entry.bg != null)
                {
                    entry.bg.color = Color.Lerp(entry.baseColor, privateUnreadColor, t);
                }
            }
        }
    }

    private void LoadLocalPlayerData()
    {
        if (CloudAuthManager.Instance == null)
        {
            localCustomData = new PlayerData(0, "Jugador");
            UpdateNameAndLevelUI(localCustomData);
            return;
        }

        localCustomData = CloudAuthManager.Instance.LocalPlayerData;

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
            return;

        // ids actuales
        List<ulong> currentIds = new List<ulong>();
        foreach (var player in GameManager.Instance.PlayersInLobby)
        {
            currentIds.Add(player.ClientId);
        }

        // limpiar los que ya no están
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

        int readyCount = 0;
        bool localPlayerFound = false;
        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        PlayerData localPlayerData = default;

        foreach (var player in GameManager.Instance.PlayersInLobby)
        {
            if (!playerCardInstances.TryGetValue(player.ClientId, out var cardInstance))
            {
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

                if (player.Username.Length > 0)
                {
                    localCustomData = player;
                    UpdateNameAndLevelUI(localCustomData);
                }
            }
        }

        UpdateLobbyControls(localPlayerData, readyCount, localPlayerFound);

        // si estamos viendo los privados, refrescamos la lista
        if (privateChatPanel != null && privateChatPanel.activeSelf)
        {
            RefreshPrivatePlayersList();
        }
    }

    private string SanitizeName(FixedString64Bytes fs, ulong clientId)
    {
        string s = fs.ToString();
        if (!string.IsNullOrEmpty(s))
            s = s.Replace("\0", string.Empty);

        if (string.IsNullOrWhiteSpace(s))
            s = $"Player_{clientId}";

        return s;
    }

    private void UpdatePlayerCard(GameObject cardInstance, PlayerData player)
    {
        TextMeshProUGUI nameText = cardInstance.transform
            .Find("PlayerNameText")?.GetComponent<TextMeshProUGUI>();

        if (nameText == null)
            nameText = cardInstance.GetComponentInChildren<TextMeshProUGUI>(true);

        string nick = SanitizeName(player.Username, player.ClientId);

        if (nameText != null)
            nameText.text = nick;

        var panelImage = cardInstance.GetComponent<Image>();
        if (panelImage != null)
            panelImage.color = player.IsReady ? readyColor : notReadyColor;

        var hostIcon = cardInstance.transform.Find("HostIcon")?.gameObject;
        if (hostIcon != null)
            hostIcon.SetActive(player.ClientId == NetworkManager.ServerClientId);

        var kickBtnTransform = cardInstance.transform.Find("KickButton");
        if (kickBtnTransform != null)
        {
            var kickBtn = kickBtnTransform.GetComponent<Button>();
            if (kickBtn != null)
            {
                bool iAmHost = NetworkManager.Singleton.IsHost;
                bool isThisTheHost = (player.ClientId == NetworkManager.ServerClientId);

                kickBtn.gameObject.SetActive(iAmHost && !isThisTheHost);
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

    private void OnChangeAppearance(int type, int direction)
    {
        if (previewPlayer == null) return;

        int maxBody = previewPlayer.GetBodyCount();
        int maxEyes = previewPlayer.GetEyesCount();
        int maxGloves = previewPlayer.GetGlovesCount();

        if (maxBody == 0 && type == 0) return;
        if (maxEyes == 0 && type == 1) return;
        if (maxGloves == 0 && type == 2) return;

        switch (type)
        {
            case 0:
                localCustomData.BodyIndex = (localCustomData.BodyIndex + direction + maxBody) % maxBody;
                break;
            case 1:
                localCustomData.EyesIndex = (localCustomData.EyesIndex + direction + maxEyes) % maxEyes;
                break;
            case 2:
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

    // =====================================================================
    // -------------------------- CHAT PÚBLICO ------------------------------
    // =====================================================================

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
            HandleChatMessage("Local", text, NetworkManager.Singleton.IsHost);
        }
    }

    private void HandleChatMessage(string sender, string message, bool isHost)
    {
        // 1) guardamos en historial
        publicChatHistory.Add(new PublicChatMessage
        {
            sender = sender,
            text = message,
            isHost = isHost
        });

        // 2) y también lo pintamos si estamos en el chat público
        if (publicChatPanel != null && publicChatPanel.activeSelf)
            AddPublicMessageToUI(sender, message, isHost);
    }

    private void AddPublicMessageToUI(string sender, string message, bool isHost)
    {
        if (chatMessagePrefab == null || chatMessagesContainer == null)
            return;

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

    private void RenderPublicChatHistory()
    {
        if (chatMessagesContainer == null) return;

        foreach (Transform child in chatMessagesContainer)
            Destroy(child.gameObject);

        foreach (var m in publicChatHistory)
            AddPublicMessageToUI(m.sender, m.text, m.isHost);
    }

    public void ShowPublicChatPanel()
    {
        if (publicChatPanel) publicChatPanel.SetActive(true);
        if (privateChatPanel) privateChatPanel.SetActive(false);

        // repintar historial público
        RenderPublicChatHistory();
    }

    // =====================================================================
    // -------------------------- CHAT PRIVADO ------------------------------
    // =====================================================================

    public void ShowPrivateChatPanel()
    {
        if (publicChatPanel) publicChatPanel.SetActive(false);
        if (privateChatPanel) privateChatPanel.SetActive(true);

        RefreshPrivatePlayersList();
    }

    // Esto ahora es una corrutina en tu versión actual
    private void RefreshPrivatePlayersList()
    {
        StartCoroutine(DoRefreshPrivatePlayersList());
    }

    private IEnumerator DoRefreshPrivatePlayersList()
    {
        if (privatePlayersContainer == null)
            yield break;

        foreach (Transform child in privatePlayersContainer)
            Destroy(child.gameObject);
        privateEntries.Clear();

        var relay = FindObjectOfType<RelayLobbyConnector>();
        Lobby lobby = null;

        if (relay != null && relay.CurrentLobby != null)
        {
            var task = LobbyService.Instance.GetLobbyAsync(relay.CurrentLobby.Id);
            yield return new WaitUntil(() => task.IsCompleted);

            if (!task.IsFaulted && task.Result != null)
            {
                lobby = task.Result;
            }
            else
            {
                lobby = relay.CurrentLobby;
            }
        }

        // mi id
        string myPlayerId = null;
        if (CloudAuthManager.Instance != null)
            myPlayerId = CloudAuthManager.Instance.GetPlayerId();
        else if (AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
            myPlayerId = AuthenticationService.Instance.PlayerId;

        if (privatePlayerButtonPrefab == null)
        {
            Debug.LogWarning("[LobbyUI] privatePlayerButtonPrefab no asignado.");
            yield break;
        }

        bool filledFromLobby = false;

        if (lobby != null && lobby.Players != null && lobby.Players.Count > 1)
        {
            Debug.Log($"[LobbyUI] Refrescando privados desde LOBBY (fresco). Jugadores en lobby: {lobby.Players.Count}");
            foreach (var p in lobby.Players)
            {
                if (p == null) continue;
                if (!string.IsNullOrEmpty(myPlayerId) && p.Id == myPlayerId)
                    continue;

                GameObject btnGO = Instantiate(privatePlayerButtonPrefab, privatePlayersContainer);
                var txt = btnGO.GetComponentInChildren<TextMeshProUGUI>();
                string displayName = GetLobbyPlayerDisplayName(p);
                if (txt != null) txt.text = displayName;

                Image bg = btnGO.GetComponent<Image>();

                var entry = new PrivateEntry
                {
                    playerId = p.Id,
                    clientId = 0,
                    go = btnGO,
                    bg = bg,
                    hasUnread = false,
                    baseColor = bg != null ? bg.color : Color.white,
                    displayName = displayName
                };
                privateEntries[p.Id] = entry;

                string targetId = p.Id;
                var button = btnGO.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.AddListener(() => OpenPrivateChannel(targetId, 0));
                }

                Debug.Log($"[LobbyUI] + jugador privado (lobby): {displayName} ({p.Id})");
            }

            filledFromLobby = true;
        }

        if (!filledFromLobby)
        {
            if (GameManager.Instance != null && GameManager.Instance.PlayersInLobby != null)
            {
                Debug.Log($"[LobbyUI] Refrescando privados desde GameManager. Jugadores: {GameManager.Instance.PlayersInLobby.Count}");
                foreach (var p in GameManager.Instance.PlayersInLobby)
                {
                    if (p.ClientId == NetworkManager.Singleton.LocalClientId)
                        continue;

                    GameObject btnGO = Instantiate(privatePlayerButtonPrefab, privatePlayersContainer);
                    var txt = btnGO.GetComponentInChildren<TextMeshProUGUI>();
                    string displayName = SanitizeName(p.Username, p.ClientId);
                    if (txt != null) txt.text = displayName;

                    Image bg = btnGO.GetComponent<Image>();

                    var entry = new PrivateEntry
                    {
                        playerId = null,
                        clientId = p.ClientId,
                        go = btnGO,
                        bg = bg,
                        hasUnread = false,
                        baseColor = bg != null ? bg.color : Color.white,
                        displayName = displayName
                    };
                    privateEntries[p.ClientId.ToString()] = entry;

                    var button = btnGO.GetComponent<Button>();
                    if (button != null)
                    {
                        ulong cid = p.ClientId;
                        button.onClick.AddListener(() => OpenPrivateChannel(null, cid));
                    }

                    Debug.Log($"[LobbyUI] + jugador privado (GM): {displayName} (clientId {p.ClientId})");
                }
            }
            else
            {
                Debug.Log("[LobbyUI] No hay lobby ni GameManager para listar privados.");
            }
        }
    }

    private string GetLobbyPlayerDisplayName(Unity.Services.Lobbies.Models.Player p)
    {
        if (p.Data != null && p.Data.TryGetValue("PlayerName", out var dataObj) && dataObj != null)
        {
            return dataObj.Value;
        }

        return p.Id;
    }

    private string GetConversationKey(string playerId, ulong clientId)
    {
        if (!string.IsNullOrEmpty(playerId))
            return "pid:" + playerId;
        if (clientId != 0)
            return "cid:" + clientId;
        return null;
    }

    private void OpenPrivateChannel(string targetPlayerId, ulong fallbackClientId)
    {
        currentPrivateTargetPlayerId = targetPlayerId;
        currentPrivateTargetClientId = fallbackClientId;

        Debug.Log($"[LobbyUI] Abriendo canal privado con: " +
                  $"{(string.IsNullOrEmpty(targetPlayerId) ? $"clientId {fallbackClientId}" : targetPlayerId)}");

        // quitar parpadeo
        if (!string.IsNullOrEmpty(targetPlayerId))
        {
            if (privateEntries.TryGetValue(targetPlayerId, out var entry))
            {
                entry.hasUnread = false;
                if (entry.bg != null) entry.bg.color = entry.baseColor;
            }
        }
        else if (fallbackClientId != 0)
        {
            string key = fallbackClientId.ToString();
            if (privateEntries.TryGetValue(key, out var entry))
            {
                entry.hasUnread = false;
                if (entry.bg != null) entry.bg.color = entry.baseColor;
            }
        }

        // repintar la conversación que ya había
        RenderPrivateConversation(targetPlayerId, fallbackClientId);
    }

    private void RenderPrivateConversation(string targetPlayerId, ulong fallbackClientId)
    {
        if (privateMessagesContainer == null) return;

        // limpiar UI
        foreach (Transform child in privateMessagesContainer)
            Destroy(child.gameObject);

        // 1º intentamos con playerId
        string keyPid = GetConversationKey(targetPlayerId, 0);
        if (!string.IsNullOrEmpty(keyPid) && privateChatHistory.TryGetValue(keyPid, out var msgsPid))
        {
            foreach (var m in msgsPid)
                AddPrivateMessageToUI(m.sender, m.text);
            return;
        }

        // 2º intentamos con clientId
        string keyCid = GetConversationKey(null, fallbackClientId);
        if (!string.IsNullOrEmpty(keyCid) && privateChatHistory.TryGetValue(keyCid, out var msgsCid))
        {
            foreach (var m in msgsCid)
                AddPrivateMessageToUI(m.sender, m.text);
            return;
        }
    }

    private void OnPrivateChatSubmit(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (string.IsNullOrEmpty(currentPrivateTargetPlayerId) && currentPrivateTargetClientId == 0)
        {
            Debug.LogWarning("[LobbyUI] Intenté enviar DM pero no hay destinatario seleccionado.");
            return;
        }

        SendPrivateChat(text);
        if (privateChatInputField != null)
        {
            privateChatInputField.text = string.Empty;
            privateChatInputField.ActivateInputField();
        }
    }

    private async void SendPrivateChat(string text)
    {
        string convKey = GetConversationKey(currentPrivateTargetPlayerId, currentPrivateTargetClientId);
        if (!string.IsNullOrEmpty(convKey))
        {
            if (!privateChatHistory.TryGetValue(convKey, out var list))
            {
                list = new List<PrivateChatMessage>();
                privateChatHistory[convKey] = list;
            }
            list.Add(new PrivateChatMessage { sender = "Yo", text = text });
        }

        if (!string.IsNullOrEmpty(currentPrivateTargetPlayerId))
        {
            Debug.Log($"[LobbyUI] Enviando DM a playerId {currentPrivateTargetPlayerId}: {text}");
            if (VivoxLobbyChatManager.Instance != null)
            {
                await VivoxLobbyChatManager.Instance.SendDirectMessage(currentPrivateTargetPlayerId, text);
            }
            AddPrivateMessageToUI("Yo", text);
        }
        else if (currentPrivateTargetClientId != 0)
        {
            Debug.LogWarning($"[LobbyUI] Quise mandar DM al clientId {currentPrivateTargetClientId} pero no tengo playerId de Vivox/UGS.");
            AddPrivateMessageToUI("Yo (local)", text);
        }
        else
        {
            Debug.LogWarning("[LobbyUI] No hay destinatario privado seleccionado.");
        }
    }

    private void HandleDirectMessage(string senderName, string senderPlayerId, string message)
    {
        Debug.Log($"[LobbyUI] DM recibido de {senderName} ({senderPlayerId}): {message}");

        // guardamos SIEMPRE
        string key = GetConversationKey(senderPlayerId, 0);
        if (!string.IsNullOrEmpty(key))
        {
            if (!privateChatHistory.TryGetValue(key, out var list))
            {
                list = new List<PrivateChatMessage>();
                privateChatHistory[key] = list;
            }
            list.Add(new PrivateChatMessage { sender = senderName, text = message });
        }

        bool isCurrentOpenByPlayerId = !string.IsNullOrEmpty(currentPrivateTargetPlayerId) &&
                                       currentPrivateTargetPlayerId == senderPlayerId;

        bool isCurrentOpenByClientId = false;

        if (!isCurrentOpenByPlayerId && currentPrivateTargetClientId != 0)
        {
            // ver si el actual se abrió por clientId pero el que escribe es ese mismo
            foreach (var kvp in privateEntries)
            {
                var entry = kvp.Value;
                if (!string.IsNullOrEmpty(entry.playerId) && entry.playerId == senderPlayerId)
                {
                    isCurrentOpenByClientId = true;
                    break;
                }
            }
        }

        if (isCurrentOpenByPlayerId || isCurrentOpenByClientId)
        {
            // lo mostramos de una
            AddPrivateMessageToUI(senderName, message);
        }
        else
        {
            // marcar como pendiente
            if (privateEntries.TryGetValue(senderPlayerId, out var entry))
            {
                entry.hasUnread = true;
            }
            else
            {
                foreach (var kvp in privateEntries)
                {
                    var e = kvp.Value;
                    if (!string.IsNullOrEmpty(e.playerId) && e.playerId == senderPlayerId)
                    {
                        e.hasUnread = true;
                        break;
                    }
                }
            }
        }
    }

    private void AddPrivateMessageToUI(string sender, string message)
    {
        if (privateMessagePrefab == null || privateMessagesContainer == null)
            return;

        var go = Instantiate(privateMessagePrefab, privateMessagesContainer);
        var txt = go.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null)
            txt.text = $"<b>{sender}:</b> {message}";
    }
}
