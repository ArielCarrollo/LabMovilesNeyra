using UnityEngine;
using Unity.Netcode;
using TMPro;
using UnityEngine.UI;

public class UiGameManager : MonoBehaviour
{
    public static UiGameManager Instance { get; private set; }

    [Header("Paneles")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject registerPanel;
    [SerializeField] private TextMeshProUGUI errorText;

    [Header("Login UI")]
    [SerializeField] private TMP_InputField loginUsername;
    [SerializeField] private TMP_InputField loginPassword;
    [SerializeField] private Button loginButton;

    [Header("Registro UI")]
    [SerializeField] private TMP_InputField registerUsername;
    [SerializeField] private TMP_InputField registerPassword;
    [SerializeField] private Button registerButton;

    private void Awake()
    {
        if (Instance == null) { Instance = this; } else { Destroy(gameObject); }
    }

    void Start()
    {
        // Asignamos las funciones a los botones.
        loginButton.onClick.AddListener(OnLoginClicked);
        registerButton.onClick.AddListener(OnRegisterClicked);

        // Al empezar, solo mostramos el panel de login.
        loginPanel.SetActive(true);
        registerPanel.SetActive(false);
        errorText.gameObject.SetActive(false);
    }

    private void OnLoginClicked()
    {
        string username = loginUsername.text;
        string password = loginPassword.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowErrorAndReset("Usuario y contraseña no pueden estar vacíos.");
            return;
        }

        // Desactivamos la UI para que el jugador no pueda hacer spam.
        SetUIInteractable(false);

        // Llamamos al ServerRpc en el GameManager.
        GameManager.Instance.LoginPlayerServerRpc(username, password, NetworkManager.Singleton.LocalClientId);
    }

    private void OnRegisterClicked()
    {
        string username = registerUsername.text;
        string password = registerPassword.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowErrorAndReset("Usuario y contraseña no pueden estar vacíos.");
            return;
        }

        SetUIInteractable(false);
        GameManager.Instance.RegisterPlayerServerRpc(username, password, NetworkManager.Singleton.LocalClientId);
    }

    // Esta función la llamará el servidor si algo falla.
    public void ShowErrorAndReset(string message)
    {
        errorText.text = message;
        errorText.gameObject.SetActive(true);
        SetUIInteractable(true);
    }

    private void SetUIInteractable(bool isInteractable)
    {
        // Si el login es exitoso, los paneles se desactivarán.
        // Si falla, se reactivarán.
        loginButton.interactable = isInteractable;
        registerButton.interactable = isInteractable;
    }
}
