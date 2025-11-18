using System;
using System.Collections;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using Unity.Services.Vivox.AudioTaps;   // taps de audio (canal)
using UnityEngine;

public class VivoxLobbyChatManager : MonoBehaviour
{
    public static VivoxLobbyChatManager Instance { get; private set; }

    // Eventos de texto
    public event Action<string, string, bool> OnTextMessage;
    public event Action<string, string, string> OnDirectMessage;

    private string currentChannelName;
    private bool isLoggedIn;
    private bool iAmHost;

    // Tap para oír a los OTROS (no incluye tu propia voz)
    private VivoxChannelAudioTap _channelTap;

    // Estados seguros para la UI
    public bool IsMicMuted
    {
        get
        {
            if (!isLoggedIn || VivoxService.Instance == null) return false;
            return VivoxService.Instance.IsInputDeviceMuted;
        }
    }
    public bool IsDeafened
    {
        get
        {
            if (!isLoggedIn || VivoxService.Instance == null) return false;
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
            await UnityServices.InitializeAsync();

        // Permiso de micro (en iOS/WebGL muestra diálogo; en desktop no)
        StartCoroutine(RequestMicPermission());
    }

    private IEnumerator RequestMicPermission()
    {
        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
    }

    /// <summary>Login a Vivox si hace falta (después de autenticarte en UGS).</summary>
    public async Task LoginIfNeeded(string displayName, bool isHostFlag)
    {
#if UNITY_WSA_10_0
        return; // Vivox desactivado en local
#endif
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

        // Suscribir eventos de texto
        VivoxService.Instance.ChannelMessageReceived -= OnChannelMessageReceived;
        VivoxService.Instance.ChannelMessageReceived += OnChannelMessageReceived;
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
        string senderPlayerId = msg.SenderPlayerId;
        string text = msg.MessageText;
        OnDirectMessage?.Invoke(senderName, senderPlayerId, text);
    }

    /// <summary>Únete al canal del lobby con voz + texto y prepara el audio tap.</summary>
    public async Task JoinLobbyChannel(string lobbyCodeOrId)
    {
#if UNITY_WSA_10_0
        return;
#endif
        if (!isLoggedIn)
        {
            Debug.LogWarning("VivoxChat: intenta unirse a canal sin login.");
            return;
        }

        currentChannelName = $"lobby-{lobbyCodeOrId}";

        // 1) Entrar al canal de grupo con VOZ + TEXTO
        await VivoxService.Instance.JoinGroupChannelAsync(
            currentChannelName,
            ChatCapability.TextAndAudio
        );

        // 2) Transmitir el micro a todos los canales activos (incluye este)
        await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.All);

        // 3) Crear el tap de canal (solo este, para no oírte a ti mismo)
        EnsureChannelTap();

        Debug.Log($"VivoxChat: unido al canal {currentChannelName} (voz+texto) con ChannelAudioTap.");
    }

    private void EnsureChannelTap()
    {
        if (_channelTap != null) return;

        var go = new GameObject("Vivox Channel Audio Tap");
        DontDestroyOnLoad(go);
        _channelTap = go.AddComponent<VivoxChannelAudioTap>();
        _channelTap.AutoAcquireChannel = true; // se engancha solo al último canal activo
        // El componente crea su propio AudioSource. Asegúrate de tener un AudioListener en escena.
    }

    public async Task LeaveCurrentChannel()
    {
        if (string.IsNullOrEmpty(currentChannelName)) return;
        await VivoxService.Instance.LeaveChannelAsync(currentChannelName);
        currentChannelName = null;
    }

    public async Task SendTextMessage(string message)
    {
        if (string.IsNullOrEmpty(currentChannelName)) return;
        string finalMsg = iAmHost ? $"[HOST] {message}" : message;
        await VivoxService.Instance.SendChannelTextMessageAsync(currentChannelName, finalMsg);
    }

    public async Task SendDirectMessage(string targetPlayerId, string message)
    {
        if (!isLoggedIn)
        {
            Debug.LogWarning("VivoxChat: intenta DM sin login.");
            return;
        }
        await VivoxService.Instance.SendDirectTextMessageAsync(targetPlayerId, message);
    }

    // === Mute / Deafen (tu UI llama a esto) ===
    public void ToggleMicMute()
    {
        if (!isLoggedIn || VivoxService.Instance == null) return;
        if (VivoxService.Instance.IsInputDeviceMuted) VivoxService.Instance.UnmuteInputDevice();
        else VivoxService.Instance.MuteInputDevice();
    }

    public void ToggleDeafen()
    {
        if (!isLoggedIn || VivoxService.Instance == null) return;
        if (VivoxService.Instance.IsOutputDeviceMuted) VivoxService.Instance.UnmuteOutputDevice();
        else VivoxService.Instance.MuteOutputDevice();
    }

    private async void OnDestroy()
    {
        if (Instance != this) return;

        if (VivoxService.Instance != null)
        {
            VivoxService.Instance.ChannelMessageReceived -= OnChannelMessageReceived;
            VivoxService.Instance.DirectedMessageReceived -= OnDirectedMessageReceived;
        }
        if (!string.IsNullOrEmpty(currentChannelName))
            await LeaveCurrentChannel();
        if (isLoggedIn && VivoxService.Instance != null)
            await VivoxService.Instance.LogoutAsync();

        Instance = null;
    }
}
