using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using TMPro; 

public class LobbyUIManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private Button startGameButton;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Player List")]
    [SerializeField] private Transform playerListContent;
    [SerializeField] private GameObject playerCardPrefab; 

    public void Initialize()
    {
        GameManager.Instance.PlayersInLobby.OnListChanged += UpdateLobbyUI;

        startGameButton.onClick.AddListener(() => {
            GameManager.Instance.StartGameServerRpc();
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
        if (!NetworkManager.Singleton.IsConnectedClient) return;

        lobbyPanel.SetActive(true);

        foreach (Transform child in playerListContent)
        {
            Destroy(child.gameObject);
        }

        startGameButton.gameObject.SetActive(NetworkManager.Singleton.IsHost);

        if (GameManager.Instance.PlayersInLobby.Count == 0 && !NetworkManager.Singleton.IsHost)
        {
            statusText.text = "Esperando a que el Host cree una sala...";
            statusText.gameObject.SetActive(true);
        }
        else
        {
            statusText.gameObject.SetActive(false);
            foreach (var player in GameManager.Instance.PlayersInLobby)
            {
                GameObject card = Instantiate(playerCardPrefab, playerListContent);
                card.GetComponentInChildren<TextMeshProUGUI>().text = player.Username.ToString();
            }
        }
    }
}