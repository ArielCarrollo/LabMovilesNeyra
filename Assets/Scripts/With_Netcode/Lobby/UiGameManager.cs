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
    [SerializeField] private GameObject lobbySelectionPanel;
    [SerializeField] private GameObject lobbiesPanel;
    [Header("Login UI")]
    [SerializeField] private TMP_InputField loginUsername;
    [SerializeField] private TMP_InputField loginPassword;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button goToRegisterButton;

    [Header("Registro UI")]
    [SerializeField] private TMP_InputField registerUsername;
    [SerializeField] private TMP_InputField registerPassword;
    [SerializeField] private Button registerButton;
    [SerializeField] private Button goToLoginButton;

    [Header("Managers")]
    [SerializeField] private LobbyUIManager lobbyUIManager;




    private void Awake()
    {
        if (Instance == null) { Instance = this; } else { Destroy(gameObject); }
    }

    private void OnEnable()
    {
        // Asegurarse de que la instancia exista antes de suscribirse
        if (CloudAuthManager.Instance != null)
        {
            CloudAuthManager.Instance.OnSignInSuccess += HandleSignInSuccess;
            CloudAuthManager.Instance.OnSignInFailed += HandleSignInFailed;
        }
    }

    private void OnDisable()
    {
        // Evitar errores si el objeto ya fue destruido
        if (CloudAuthManager.Instance != null)
        {
            CloudAuthManager.Instance.OnSignInSuccess -= HandleSignInSuccess;
            CloudAuthManager.Instance.OnSignInFailed -= HandleSignInFailed;
        }
    }

    void Start()
    {
        loginButton.onClick.AddListener(OnLoginClicked);
        registerButton.onClick.AddListener(OnRegisterClicked);
        goToRegisterButton.onClick.AddListener(() => {
            loginPanel.SetActive(false);
            registerPanel.SetActive(true);
        });
        goToLoginButton.onClick.AddListener(() => {
            loginPanel.SetActive(true);
            registerPanel.SetActive(false);
        });

        loginPanel.SetActive(true);
        registerPanel.SetActive(false);
        errorText.gameObject.SetActive(false);
        if (lobbySelectionPanel) lobbySelectionPanel.SetActive(false);
        if (lobbiesPanel) lobbiesPanel.SetActive(false);
    }

    private async void OnLoginClicked()
    {
        string username = loginUsername.text;
        string password = loginPassword.text;
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowError("Usuario y contraseña no pueden estar vacíos.");
            return;
        }
        SetUIInteractable(false);
        await CloudAuthManager.Instance.SignInWithUsernamePassword(username, password);
    }

    private async void OnRegisterClicked()
    {
        string username = registerUsername.text;
        string password = registerPassword.text;
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowError("Usuario y contraseña no pueden estar vacíos.");
            return;
        }
        SetUIInteractable(false);
        await CloudAuthManager.Instance.SignUpWithUsernamePassword(username, password);
    }

    private void HandleSignInSuccess()
    {
        loginPanel.SetActive(false);
        registerPanel.SetActive(false);
        errorText.gameObject.SetActive(false);
        if (lobbySelectionPanel) lobbySelectionPanel.SetActive(true);
    }

    private void HandleSignInFailed(string message)
    {
        ShowError(message);
        SetUIInteractable(true);
    }

    public void ShowError(string message)
    {
        errorText.text = message;
        errorText.gameObject.SetActive(true);
    }

    private void SetUIInteractable(bool isInteractable)
    {
        loginButton.interactable = isInteractable;
        registerButton.interactable = isInteractable;
        goToLoginButton.interactable = isInteractable;
        goToRegisterButton.interactable = isInteractable;
    }

    public void GoToLobby()
    {
        if (lobbiesPanel) lobbiesPanel.SetActive(true); 
        if (lobbySelectionPanel) lobbySelectionPanel.SetActive(false);
        loginPanel.SetActive(false);
        registerPanel.SetActive(false);
        errorText.gameObject.SetActive(false);
        lobbyUIManager.Initialize();
    }
    public void GoToLobbySelection()
    {
        if (lobbiesPanel) lobbiesPanel.SetActive(false);
        if (lobbySelectionPanel) lobbySelectionPanel.SetActive(true);
        loginPanel.SetActive(false);
        registerPanel.SetActive(false);
        errorText.gameObject.SetActive(false);
    }

}