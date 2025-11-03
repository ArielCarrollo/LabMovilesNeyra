using System;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;
using System.Threading.Tasks;

public class VivoxLobbyChatManager : MonoBehaviour
{
    public static VivoxLobbyChatManager Instance { get; private set; }

    // sender, message, isHost
    public event Action<string, string, bool> OnTextMessage;

    private string currentChannelName;
    private bool isLoggedIn;
    private bool iAmHost;

    private async void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Por si tu bootstrap de servicios aún no corrió
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            await UnityServices.InitializeAsync();
        }
    }

    public async Task LoginIfNeeded(string displayName, bool isHostFlag)
    {
        iAmHost = isHostFlag;

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            Debug.LogWarning("VivoxChat: no hay sesión de Unity Authentication.");
            return;
        }

        if (isLoggedIn)
            return;

        // 👇 aquí va LoginOptions, no un string
        var options = new LoginOptions
        {
            DisplayName = displayName
        };

        await VivoxService.Instance.LoginAsync(options);
        isLoggedIn = true;

        // Suscribirse a los mensajes de canal
        VivoxService.Instance.ChannelMessageReceived -= OnChannelMessageReceived;
        VivoxService.Instance.ChannelMessageReceived += OnChannelMessageReceived;
    }

    private void OnChannelMessageReceived(VivoxMessage msg)
    {
        string sender = msg.SenderDisplayName;
        string text = msg.MessageText;

        bool isHost = false;
        if (!string.IsNullOrEmpty(text) && text.StartsWith("[HOST] "))
        {
            isHost = true;
            text = text.Substring(7); // quitar "[HOST] "
        }

        OnTextMessage?.Invoke(sender, text, isHost);
    }

    public async Task JoinLobbyChannel(string lobbyCodeOrId)
    {
        if (!isLoggedIn)
        {
            Debug.LogWarning("VivoxChat: intenta unirse a canal sin login.");
            return;
        }

        currentChannelName = $"lobby-{lobbyCodeOrId}";

        // Canal de grupo con texto
        await VivoxService.Instance.JoinGroupChannelAsync(
            currentChannelName,
            ChatCapability.TextOnly
        );  // puedes pasar ChannelOptions si quieres algo más

        Debug.Log($"VivoxChat: unido al canal {currentChannelName}");
    }

    public async Task LeaveCurrentChannel()
    {
        if (string.IsNullOrEmpty(currentChannelName))
            return;

        await VivoxService.Instance.LeaveChannelAsync(currentChannelName);
        currentChannelName = null;
    }

    public async Task SendTextMessage(string message)
    {
        if (string.IsNullOrEmpty(currentChannelName))
            return;

        string finalMsg = iAmHost ? $"[HOST] {message}" : message;

        // 👇 este es el nombre correcto del método
        await VivoxService.Instance.SendChannelTextMessageAsync(currentChannelName, finalMsg);
    }

    private async void OnDestroy()
    {
        if (Instance == this)
        {
            // desuscribir
            VivoxService.Instance.ChannelMessageReceived -= OnChannelMessageReceived;

            if (!string.IsNullOrEmpty(currentChannelName))
                await LeaveCurrentChannel();

            if (isLoggedIn)
                await VivoxService.Instance.LogoutAsync();

            Instance = null;
        }
    }
}
