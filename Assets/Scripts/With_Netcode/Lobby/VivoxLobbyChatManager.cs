using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;

public class VivoxLobbyChatManager : MonoBehaviour
{
    public static VivoxLobbyChatManager Instance { get; private set; }

    // Mensajes públicos (canal de lobby)
    public event Action<string, string, bool> OnTextMessage;

    // Mensajes privados (DM): senderName, senderPlayerId, message
    public event Action<string, string, string> OnDirectMessage;

    private string currentChannelName;
    private bool isLoggedIn;
    private bool iAmHost;

    public string CurrentLobbyChannel => currentChannelName;

    // --- Estados de voz para UI ---
    public bool IsMicMuted
    {
        get
        {
            if (!isLoggedIn || VivoxService.Instance == null)
                return false;
            return VivoxService.Instance.IsInputDeviceMuted;
        }
    }

    public bool IsDeafened
    {
        get
        {
            if (!isLoggedIn || VivoxService.Instance == null)
                return false;
            return VivoxService.Instance.IsOutputDeviceMuted;
        }
    }
    public bool IsLoggedIn => isLoggedIn;

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

        if (isLoggedIn) return;

        var options = new LoginOptions { DisplayName = displayName };
        await VivoxService.Instance.LoginAsync(options);
        isLoggedIn = true;

        // Eventos de texto canal
        VivoxService.Instance.ChannelMessageReceived -= OnChannelMessageReceived;
        VivoxService.Instance.ChannelMessageReceived += OnChannelMessageReceived;

        // Eventos de DM
        VivoxService.Instance.DirectedMessageReceived -= OnDirectedMessageReceived;
        VivoxService.Instance.DirectedMessageReceived += OnDirectedMessageReceived;
    }

    private void OnChannelMessageReceived(VivoxMessage msg)
    {
        string sender = msg.SenderDisplayName;
        string text = msg.MessageText;

        bool isHostTag = false;
        if (!string.IsNullOrEmpty(text) && text.StartsWith("[HOST] "))
        {
            isHostTag = true;
            text = text.Substring(7);
        }

        OnTextMessage?.Invoke(sender, text, isHostTag);
    }

    private void OnDirectedMessageReceived(VivoxMessage msg)
    {
        string senderName = msg.SenderDisplayName;
        string senderPlayerId = msg.SenderPlayerId; // Id UGS
        string text = msg.MessageText;

        OnDirectMessage?.Invoke(senderName, senderPlayerId, text);
    }

    // ===>> AHORA UNE TEXTO + VOZ EN EL MISMO CANAL DEL LOBBY
    public async Task JoinLobbyChannel(string lobbyCodeOrId)
    {
        if (!isLoggedIn)
        {
            Debug.LogWarning("VivoxChat: intenta unirse a canal sin login.");
            return;
        }

        currentChannelName = $"lobby-{lobbyCodeOrId}";

        await VivoxService.Instance.JoinGroupChannelAsync(
            currentChannelName,
            ChatCapability.TextAndAudio   // <-- aquí el cambio
        );

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
        await VivoxService.Instance.SendChannelTextMessageAsync(currentChannelName, finalMsg);
    }

    // --- DMs (directo a playerId UGS) ---
    public async Task SendDirectMessage(string targetPlayerId, string message)
    {
        if (!isLoggedIn)
        {
            Debug.LogWarning("VivoxChat: intenta DM sin login.");
            return;
        }

        await VivoxService.Instance.SendDirectTextMessageAsync(targetPlayerId, message);
    }

    // --- Controles de VOZ para UI ---
    public void ToggleMicMute()
    {
        if (VivoxService.Instance == null) return;

        if (VivoxService.Instance.IsInputDeviceMuted)
        {
            VivoxService.Instance.UnmuteInputDevice();
            Debug.Log("[VivoxVoice] Mic: UNMUTED");
        }
        else
        {
            VivoxService.Instance.MuteInputDevice();
            Debug.Log("[VivoxVoice] Mic: MUTED");
        }
    }

    public void ToggleDeafen()
    {
        if (VivoxService.Instance == null) return;

        if (VivoxService.Instance.IsOutputDeviceMuted)
        {
            VivoxService.Instance.UnmuteOutputDevice();
            Debug.Log("[VivoxVoice] Output: UNMUTED");
        }
        else
        {
            VivoxService.Instance.MuteOutputDevice();
            Debug.Log("[VivoxVoice] Output: MUTED (Deafened)");
        }
    }

    private async void OnDestroy()
    {
        if (Instance == this)
        {
            VivoxService.Instance.ChannelMessageReceived -= OnChannelMessageReceived;
            VivoxService.Instance.DirectedMessageReceived -= OnDirectedMessageReceived;

            if (!string.IsNullOrEmpty(currentChannelName))
                await LeaveCurrentChannel();

            if (isLoggedIn)
                await VivoxService.Instance.LogoutAsync();

            Instance = null;
        }
    }
}
