using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using TMPro;
using System.Linq; 

public class LobbyUIManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button readyButton; 
    [SerializeField] private TextMeshProUGUI readyButtonText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI readyCountText;

    [Header("Player List")]
    [SerializeField] private Transform playerListContent;
    [SerializeField] private GameObject playerCardPrefab;

    [Header("Colors")]
    [SerializeField] private Color readyColor = Color.green;
    [SerializeField] private Color notReadyColor = Color.white;

    [Header("Customization")]
    [Tooltip("Un modelo del jugador en la escena del Lobby para vista previa")]
    [SerializeField] private PlayerAppearance previewPlayer;
    [SerializeField] private Button nextBodyButton;
    [SerializeField] private Button prevBodyButton;
    [SerializeField] private Button nextEyesButton;
    [SerializeField] private Button prevEyesButton;
    [SerializeField] private Button nextGlovesButton;
    [SerializeField] private Button prevGlovesButton;
    private PlayerData localPlayerData;


    public void Initialize()
    {
        GameManager.Instance.PlayersInLobby.OnListChanged += UpdateLobbyUI;

        startGameButton.onClick.AddListener(() => {
            GameManager.Instance.StartGameServerRpc();
        });

        readyButton.onClick.AddListener(() => {
            GameManager.Instance.ToggleReadyServerRpc(NetworkManager.Singleton.LocalClientId);
        });

        lobbyPanel.SetActive(true);
        UpdateLobbyUI(new NetworkListEvent<PlayerData>());
        nextBodyButton.onClick.AddListener(() => ChangePart(0, 1));
        prevBodyButton.onClick.AddListener(() => ChangePart(0, -1));
        nextEyesButton.onClick.AddListener(() => ChangePart(1, 1));
        prevEyesButton.onClick.AddListener(() => ChangePart(1, -1));
        nextGlovesButton.onClick.AddListener(() => ChangePart(2, 1));
        prevGlovesButton.onClick.AddListener(() => ChangePart(2, -1));
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PlayersInLobby.OnListChanged -= UpdateLobbyUI;
        }
    }
    private void ChangePart(int partIndex, int direction)
    {
        int maxIndex = 0;

        // Aquí guardas una copia de los datos actuales
        PlayerData updatedData = localPlayerData;

        // Lógica para cambiar el índice de la pieza correcta
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
                // ... Añade un case para cada parte del cuerpo ...
        }

        // Envía la petición de cambio al servidor con TODOS los datos actualizados
        GameManager.Instance.ChangeAppearanceServerRpc(updatedData, NetworkManager.Singleton.LocalClientId);
    }
    private void UpdateLobbyUI(NetworkListEvent<PlayerData> changeEvent)
    {
        if (!NetworkManager.Singleton.IsConnectedClient || GameManager.Instance == null) return;

        lobbyPanel.SetActive(true);

        foreach (Transform child in playerListContent)
        {
            Destroy(child.gameObject);
        }
        int readyCount = 0;
        foreach (var player in GameManager.Instance.PlayersInLobby)
        {
            if (player.IsReady)
            {
                readyCount++;
            }
        }
        // Buscamos los datos del jugador local
        bool localPlayerFound = false;
        foreach (var player in GameManager.Instance.PlayersInLobby)
        {
            if (player.ClientId == NetworkManager.Singleton.LocalClientId)
            {
                localPlayerData = player; // Guardamos los datos del jugador local
                localPlayerFound = true;
                break;
            }
        }

        // Actualizamos el modelo de vista previa si existimos en la lista
        if (localPlayerFound && previewPlayer != null)
        {
            previewPlayer.ApplyAppearance(localPlayerData);
        }
        int totalPlayers = GameManager.Instance.PlayersInLobby.Count;
        readyCountText.text = $"Listos: {readyCount}/{totalPlayers}";

        bool allPlayersReady = totalPlayers > 0 && readyCount == totalPlayers;
        startGameButton.gameObject.SetActive(NetworkManager.Singleton.IsHost);
        startGameButton.interactable = allPlayersReady;
        PlayerData localPlayer = new PlayerData(); // Struct por defecto
        foreach (var player in GameManager.Instance.PlayersInLobby)
        {
            if (player.ClientId == NetworkManager.Singleton.LocalClientId)
            {
                localPlayer = player;
                localPlayerFound = true;
                break;
            }
        }

        if (localPlayerFound)
        {
            readyButtonText.text = localPlayer.IsReady ? "No Listo" : "Listo";
        }

        if (totalPlayers == 0 && !NetworkManager.Singleton.IsHost)
        {
            statusText.text = "Esperando a que el Host cree una sala...";
            statusText.gameObject.SetActive(true);
            readyCountText.gameObject.SetActive(false);
        }
        else
        {
            statusText.gameObject.SetActive(false);
            readyCountText.gameObject.SetActive(true);

            foreach (var player in GameManager.Instance.PlayersInLobby)
            {
                GameObject card = Instantiate(playerCardPrefab, playerListContent);
                card.GetComponentInChildren<TextMeshProUGUI>().text = player.Username.ToString();

                Image cardImage = card.GetComponent<Image>();
                if (cardImage != null)
                {
                    cardImage.color = player.IsReady ? readyColor : notReadyColor;
                }
            }
        }
    }
}