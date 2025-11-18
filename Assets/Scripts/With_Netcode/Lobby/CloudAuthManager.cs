using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;

public class CloudAuthManager : MonoBehaviour
{
    public static CloudAuthManager Instance { get; private set; }
    private bool IsXboxOffline =>
#if UNITY_WSA_10_0
        true;
#else
        false;
#endif
    public event Action OnSignInSuccess;
    public event Action<string> OnSignInFailed;
    public event Action<string> OnPlayerNameUpdated;

    public PlayerData LocalPlayerData { get; private set; }
    private const string PLAYER_PROGRESS_KEY = "PLAYER_PROGRESS_DATA";
    private string playerId;
    private string playerName;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public async Task InitializeUnityServices()
    {
        if (IsXboxOffline) return; // No hacer nada en Xbox
        if (UnityServices.State == ServicesInitializationState.Initialized) return;

        try
        {
            var options = new InitializationOptions();
            options.SetEnvironmentName("production");
            await UnityServices.InitializeAsync(options);
            Debug.Log("Unity Services Initialized: " + UnityServices.State);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to initialize Unity Services: " + e);
            OnSignInFailed?.Invoke("Error al inicializar servicios.");
        }
    }

    public async Task SignUpWithUsernamePassword(string username, string password)
    {
#if UNITY_WSA_10_0
        // Bypass completo para Xbox
        MockLoginInfo(username);
        await SavePlayerProgress(); // Guarda localmente
        OnSignInSuccess?.Invoke();
        return;
#endif
        SignOutIfSignedIn();
        await InitializeUnityServices();
        try
        {
            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password);

            playerId = AuthenticationService.Instance.PlayerId;
            playerName = username; 

            Debug.Log($"Sign Up & Sign In Successful. Player ID: {playerId}, Player Name: {playerName}");

            await UpdatePlayerNameAsync(username);
            LocalPlayerData = new PlayerData(0, username); 
            await SavePlayerProgress();
            OnSignInSuccess?.Invoke();
        }
        catch (AuthenticationException ex)
        {
            string errorMessage = ConvertExceptionToMessage(ex);
            Debug.LogException(ex);
            OnSignInFailed?.Invoke(errorMessage);
        }
        catch (RequestFailedException ex)
        {
            Debug.LogException(ex);
            OnSignInFailed?.Invoke("Error de conexión. Inténtalo de nuevo.");
        }
    }
    public async Task UpdatePlayerNameAsync(string newName)
    {
#if UNITY_WSA_10_0
        playerName = newName;
        OnPlayerNameUpdated?.Invoke(newName);
        await Task.CompletedTask;
        return;
#endif
        if (string.IsNullOrWhiteSpace(newName))
        {
            Debug.LogError("El nombre no puede estar vacío.");
            return;
        }

        try
        {
            await AuthenticationService.Instance.UpdatePlayerNameAsync(newName);
            this.playerName = newName;
            Debug.Log($"Nombre actualizado exitosamente a: {newName}");
            OnPlayerNameUpdated?.Invoke(newName);
        }
        catch (AuthenticationException ex)
        {
            Debug.LogException(ex);
        }
        catch (RequestFailedException ex)
        {
            Debug.LogException(ex);
        }
    }
    public async Task SignInWithUsernamePassword(string username, string password)
    {

#if UNITY_WSA_10_0
        // Bypass completo para Xbox
        // En Xbox podrías incluso ignorar username/password y usar "PlayerXbox"
        MockLoginInfo(string.IsNullOrEmpty(username) ? "XboxPlayer" : username);
        await LoadPlayerProgress(); // Carga localmente
        OnSignInSuccess?.Invoke();
        return;
#endif
        SignOutIfSignedIn();
        await InitializeUnityServices();
        try
        {
            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);

            playerId = AuthenticationService.Instance.PlayerId;
            playerName = await AuthenticationService.Instance.GetPlayerNameAsync();

            Debug.Log($"Sign In Successful. Player ID: {playerId}, Player Name: {playerName}");
            await LoadPlayerProgress();

            // ✅ aquí sin "!= null"
            if (LocalPlayerData.Username.Length == 0 && !string.IsNullOrWhiteSpace(playerName))
            {
                var tmp = LocalPlayerData;                                   // copiar struct
                tmp.Username = new Unity.Collections.FixedString64Bytes(playerName);
                UpdateLocalData(tmp);                                        // reasignar a la propiedad
            }

            OnSignInSuccess?.Invoke();
        }
        catch (AuthenticationException)
        {
            OnSignInFailed?.Invoke("Usuario o contraseña incorrectos.");
        }
        catch (RequestFailedException ex)
        {
            Debug.LogException(ex);
            OnSignInFailed?.Invoke("Error de conexión. Inténtalo de nuevo.");
        }
    }


    private string ConvertExceptionToMessage(AuthenticationException ex)
    {
        // https://docs.unity.com/authentication/manual/exception-codes
        switch (ex.ErrorCode)
        {
            case 10200: // USERNAME_EXISTS
                return "Este nombre de usuario ya está en uso.";
            case 10202: // INVALID_PASSWORD
                return "La contraseña no es válida. Debe tener al menos 8 caracteres.";
            case 10203: // INVALID_USERNAME
                return "El nombre de usuario no es válido.";
            default:
                return "Error desconocido en el registro.";
        }
    }
    public async Task LoadPlayerProgress()
    {
#if UNITY_WSA_10_0
        // CARGA LOCAL (PlayerPrefs o File)
        Debug.Log("[Xbox Offline] Cargando datos locales...");
        string json = PlayerPrefs.GetString(PLAYER_PROGRESS_KEY, "");

        if (!string.IsNullOrEmpty(json))
        {
            LocalPlayerData = JsonConvert.DeserializeObject<PlayerData>(json);
        }
        else
        {
            LocalPlayerData = new PlayerData(0, playerName);
        }
        await Task.CompletedTask;
        return;
#endif
        try
        {
            var serverData = await CloudSaveService.Instance.Data.Player
                .LoadAsync(new HashSet<string> { PLAYER_PROGRESS_KEY });

            if (serverData.TryGetValue(PLAYER_PROGRESS_KEY, out var data))
            {
                string jsonData = data.Value.GetAs<string>();

                // 👇 primero deserializamos a una variable normal
                var loaded = JsonConvert.DeserializeObject<PlayerData>(jsonData);
                Debug.Log("Datos del jugador cargados desde la nube.");

                // 👇 si el JSON venía sin nombre, lo reinyectamos del Auth
                string authName = GetPlayerName();
                if (loaded.Username.Length == 0 && !string.IsNullOrWhiteSpace(authName))
                {
                    loaded.Username = new Unity.Collections.FixedString64Bytes(authName);
                    Debug.Log($"CloudAuthManager: nombre reinyectado desde Auth: {authName}");
                }

                // 👇 AHORA sí lo guardo en la propiedad
                LocalPlayerData = loaded;
            }
            else
            {
                Debug.Log("No se encontraron datos en la nube. Creando datos locales por defecto.");
                string authName = GetPlayerName();
                LocalPlayerData = new PlayerData(0,
                    string.IsNullOrWhiteSpace(authName) ? "Player" : authName);
                await SavePlayerProgress();
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Error al cargar los datos del jugador: " + e);
            string authName = GetPlayerName();
            LocalPlayerData = new PlayerData(0,
                string.IsNullOrWhiteSpace(authName) ? "Player" : authName);
        }
    }


    public async Task SavePlayerProgress()
    {
#if UNITY_WSA_10_0
        // GUARDADO LOCAL
        string json = JsonConvert.SerializeObject(LocalPlayerData);
        PlayerPrefs.SetString(PLAYER_PROGRESS_KEY, json);
        PlayerPrefs.Save();
        Debug.Log("[Xbox Offline] Progreso guardado en PlayerPrefs.");
        await Task.CompletedTask;
        return;
#endif
        try
        {
            string jsonData = JsonConvert.SerializeObject(LocalPlayerData);
            var dataToSave = new Dictionary<string, object> { { PLAYER_PROGRESS_KEY, jsonData } };
            await CloudSaveService.Instance.Data.Player.SaveAsync(dataToSave);
            Debug.Log("Progreso del jugador guardado en la nube.");
        }
        catch (Exception e)
        {
            Debug.LogError("Error al guardar el progreso del jugador: " + e);
        }
    }
    public void SignOutIfSignedIn()
    {
        if (AuthenticationService.Instance.IsSignedIn)
        {
            AuthenticationService.Instance.SignOut();
            playerId = null;
            playerName = null;
            Debug.Log("Auth: sesión anterior cerrada.");
        }
    }
    private void MockLoginInfo(string name)
    {
        playerId = "LocalXboxID_" + Guid.NewGuid().ToString().Substring(0, 5);
        playerName = name;
        LocalPlayerData = new PlayerData(0, name);
        Debug.Log($"[Xbox Offline] Login simulado para: {playerName}");
    }

    public void UpdateLocalData(PlayerData data)
    {
        LocalPlayerData = data;
    }
    public string GetPlayerName() => playerName;
    public string GetPlayerId() => playerId;
}