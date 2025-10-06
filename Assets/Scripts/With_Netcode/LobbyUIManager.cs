using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using TMPro;

public class LobbyUIManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private TextMeshProUGUI readyButtonText;
    [SerializeField] private TextMeshProUGUI statusText; // Aunque no se use, lo mantenemos por si acaso
    [SerializeField] private TextMeshProUGUI readyCountText;

    [Header("Player List")]
    [SerializeField] private Transform playerListContent;
    [SerializeField] private GameObject playerCardPrefab;

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

    [Header("Player Name Management")]
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TMP_InputField nameChangeInputField;
    [SerializeField] private Button saveNameButton;

    [Header("Player Progression")]
    [SerializeField] private Slider xpBar;
    [SerializeField] private TextMeshProUGUI levelText;

    private PlayerData localPlayerData; // Necesario para la personalización

    public void Initialize()
    {
        // 1. Suscribirse a UN SOLO evento para actualizar la UI cuando la lista cambie.
        GameManager.Instance.PlayersInLobby.OnListChanged += OnPlayersListChanged;

        // 2. Configurar los listeners de los botones.
        startGameButton.onClick.AddListener(() => GameManager.Instance.StartGameServerRpc());
        readyButton.onClick.AddListener(() => GameManager.Instance.ToggleReadyServerRpc(NetworkManager.Singleton.LocalClientId));

        // Listeners de personalización
        nextBodyButton.onClick.AddListener(() => ChangePart(0, 1));
        prevBodyButton.onClick.AddListener(() => ChangePart(0, -1));
        nextEyesButton.onClick.AddListener(() => ChangePart(1, 1));
        prevEyesButton.onClick.AddListener(() => ChangePart(1, -1));
        nextGlovesButton.onClick.AddListener(() => ChangePart(2, 1));
        prevGlovesButton.onClick.AddListener(() => ChangePart(2, -1));

        // Listeners de gestión de nombre
        saveNameButton.onClick.AddListener(OnSaveNameClicked);
        CloudAuthManager.Instance.OnPlayerNameUpdated += HandlePlayerNameUpdated;

        // 3. Activar el panel del lobby.
        lobbyPanel.SetActive(true);

        // 4. Configurar la UI inicial del jugador local (nombre).
        string currentName = CloudAuthManager.Instance.GetPlayerName();
        playerNameText.text = $"Jugador: {currentName}";
        nameChangeInputField.text = currentName;

        // 5. Llamar a la función de actualización para mostrar el estado inicial de TODO el lobby.
        UpdateLobbyDisplay();
    }

    private void OnDestroy()
    {
        // Limpiar suscripciones para evitar errores
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PlayersInLobby.OnListChanged -= OnPlayersListChanged;
        }
        if (CloudAuthManager.Instance != null)
        {
            CloudAuthManager.Instance.OnPlayerNameUpdated -= HandlePlayerNameUpdated;
        }
    }

    private void ChangePart(int partIndex, int direction)
    {
        int maxIndex = 0;
        PlayerData updatedData = localPlayerData;

        switch (partIndex)
        {
            case 0: // Cuerpo
                maxIndex = previewPlayer.transform.Find("Bodies").childCount;
                updatedData.BodyIndex = (localPlayerData.BodyIndex + direction + maxIndex) % maxIndex;
                break;
            case 1: // Ojos
                maxIndex = previewPlayer.transform.Find("Eyes").childCount;
                updatedData.EyesIndex = (localPlayerData.EyesIndex + direction + maxIndex) % maxIndex;
                break;
            case 2: // Guantes
                maxIndex = previewPlayer.transform.Find("Gloves").childCount;
                updatedData.GlovesIndex = (localPlayerData.GlovesIndex + direction + maxIndex) % maxIndex;
                break;
        }
        GameManager.Instance.ChangeAppearanceServerRpc(updatedData, NetworkManager.Singleton.LocalClientId);
    }

    // El callback del evento ahora solo llama a la función principal
    private void OnPlayersListChanged(NetworkListEvent<PlayerData> changeEvent)
    {
        UpdateLobbyDisplay();
    }

    // Esta función se encarga de redibujar todo el lobby
    private void UpdateLobbyDisplay()
    {
        if (GameManager.Instance == null) return;

        foreach (Transform child in playerListContent)
        {
            Destroy(child.gameObject);
        }

        int readyCount = 0;
        bool allPlayersReady = GameManager.Instance.PlayersInLobby.Count > 0;

        foreach (var player in GameManager.Instance.PlayersInLobby)
        {
            GameObject card = Instantiate(playerCardPrefab, playerListContent);
            card.GetComponentInChildren<TextMeshProUGUI>().text = player.Username.ToString();
            Image cardImage = card.GetComponent<Image>();
            cardImage.color = player.IsReady ? readyColor : notReadyColor;

            if (player.IsReady)
            {
                readyCount++;
            }
            else
            {
                allPlayersReady = false;
            }

            if (player.ClientId == NetworkManager.Singleton.LocalClientId)
            {
                localPlayerData = player;

                // --- LA CORRECCIÓN CLAVE ---
                // Cambiamos 'UpdateAppearance' por 'ApplyAppearance'
                if (previewPlayer != null)
                {
                    previewPlayer.ApplyAppearance(player);
                }

                UpdateExperienceUI(player);
                readyButtonText.text = player.IsReady ? "No Listo" : "Listo";
            }
        }

        readyCountText.text = $"{readyCount} / {GameManager.Instance.PlayersInLobby.Count}";
        startGameButton.gameObject.SetActive(NetworkManager.Singleton.IsHost);
        startGameButton.interactable = allPlayersReady;
    }

    private void UpdateExperienceUI(PlayerData data)
    {
        if (GameManager.Instance == null) return;
        int xpNeededForNextLevel = GameManager.Instance.GetXpForLevel(data.Level);

        levelText.text = $"Nivel {data.Level} | ({data.CurrentXP} / {xpNeededForNextLevel} XP)";
        xpBar.maxValue = xpNeededForNextLevel;
        xpBar.value = data.CurrentXP;
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
        saveNameButton.interactable = true;
    }

    private void HandlePlayerNameUpdated(string newName)
    {
        playerNameText.text = $"Jugador: {newName}";
        GameManager.Instance.UpdatePlayerNameServerRpc(newName);
    }
}