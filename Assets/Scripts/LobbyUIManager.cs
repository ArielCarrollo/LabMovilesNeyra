using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using TMPro;
using System.Linq; // Necesario para usar Linq

public class LobbyUIManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button readyButton; // Botón para marcar "Listo"
    [SerializeField] private TextMeshProUGUI readyButtonText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI readyCountText; // Texto para "Listos: X/Y"

    [Header("Player List")]
    [SerializeField] private Transform playerListContent;
    [SerializeField] private GameObject playerCardPrefab;

    [Header("Colors")]
    [SerializeField] private Color readyColor = Color.green;
    [SerializeField] private Color notReadyColor = Color.white;


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
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PlayersInLobby.OnListChanged -= UpdateLobbyUI;
        }
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

        int totalPlayers = GameManager.Instance.PlayersInLobby.Count;
        readyCountText.text = $"Listos: {readyCount}/{totalPlayers}";

        bool allPlayersReady = totalPlayers > 0 && readyCount == totalPlayers;
        startGameButton.gameObject.SetActive(NetworkManager.Singleton.IsHost);
        startGameButton.interactable = allPlayersReady;
        PlayerData localPlayer = new PlayerData(); // Struct por defecto
        bool localPlayerFound = false;
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