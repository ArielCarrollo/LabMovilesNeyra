using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;

    [Header("Panels")]
    [SerializeField] private GameObject connectionPanel; // El panel con los botones Host/Client
    [SerializeField] private GameObject loginPanel;      // El panel de UiGameManager

    private void Start()
    {
        hostButton.onClick.AddListener(OnHostClicked);
        clientButton.onClick.AddListener(OnClientClicked);

        // Al empezar, solo vemos los botones de conexión.
        connectionPanel.SetActive(true);
        loginPanel.SetActive(false);

        // Nos suscribimos para saber cuándo nos conectamos.
        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
    }

    private void OnDestroy()
    {
        // Limpieza de la suscripción.
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        }
    }

    private void OnHostClicked()
    {
        NetworkManager.Singleton.StartHost();
    }

    private void OnClientClicked()
    {
        NetworkManager.Singleton.StartClient();
    }

    private void HandleClientConnected(ulong clientId)
    {
        // Solo el cliente local debe reaccionar a su propia conexión.
        if (clientId != NetworkManager.Singleton.LocalClientId) return;

        // Una vez conectados, escondemos los botones de Host/Client y mostramos el login.
        connectionPanel.SetActive(false);
        loginPanel.SetActive(true);
    }
}