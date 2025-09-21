// Archivo: UiGameManager.cs (Corregido)
using UnityEngine;
using Unity.Netcode;
using TMPro;
using UnityEngine.UI;

public class UiGameManager : MonoBehaviour
{
    // ... (variables y Awake/Start sin cambios) ...
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
        loginButton.onClick.AddListener(OnLoginClicked);
        registerButton.onClick.AddListener(OnRegisterClicked);
        loginPanel.SetActive(true);
        registerPanel.SetActive(false);
        errorText.gameObject.SetActive(false);
    }


    private void OnLoginClicked()
    {
        // ... (validaci�n de campos sin cambios) ...
        string username = loginUsername.text;
        string password = loginPassword.text;
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowErrorAndReset("Usuario y contrase�a no pueden estar vac�os.");
            return;
        }
        SetUIInteractable(false);

        // --- L�NEA CORREGIDA ---
        // Llamamos a la funci�n con los 3 argumentos que espera.
        GameManager.Instance.LoginPlayerServerRpc(username, password, NetworkManager.Singleton.LocalClientId);
    }

    private void OnRegisterClicked()
    {
        // ... (validaci�n de campos sin cambios) ...
        string username = registerUsername.text;
        string password = registerPassword.text;
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowErrorAndReset("Usuario y contrase�a no pueden estar vac�os.");
            return;
        }
        SetUIInteractable(false);

        // --- L�NEA CORREGIDA ---
        GameManager.Instance.RegisterPlayerServerRpc(username, password, NetworkManager.Singleton.LocalClientId);
    }

    // ... (ShowErrorAndReset y SetUIInteractable sin cambios) ...
    public void ShowErrorAndReset(string message)
    {
        errorText.text = message;
        errorText.gameObject.SetActive(true);
        SetUIInteractable(true);
    }

    private void SetUIInteractable(bool isInteractable)
    {
        loginButton.interactable = isInteractable;
        registerButton.interactable = isInteractable;
    }
}