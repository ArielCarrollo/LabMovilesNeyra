using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;

    [Header("Panels")]
    [SerializeField] private GameObject connectionPanel;
    [SerializeField] private GameObject loginPanel;

    [Header("Prefabs")]
    [SerializeField] private GameObject gameManagerPrefab; 

    private void Start()
    {
        hostButton.onClick.AddListener(OnHostClicked);
        clientButton.onClick.AddListener(OnClientClicked);

        connectionPanel.SetActive(true);
        loginPanel.SetActive(false);

        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        }
    }

    private void OnHostClicked()
    {
        NetworkManager.Singleton.StartHost();

        GameObject gameManagerInstance = Instantiate(gameManagerPrefab);
        gameManagerInstance.GetComponent<NetworkObject>().Spawn();
    }


    private void OnClientClicked()
    {
        NetworkManager.Singleton.StartClient();
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId) return;

        connectionPanel.SetActive(false);
        loginPanel.SetActive(true);
    }
}