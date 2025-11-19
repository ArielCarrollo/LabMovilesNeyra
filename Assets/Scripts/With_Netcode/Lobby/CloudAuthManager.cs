using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;

public class CloudAuthManager : MonoBehaviour
{
    public static CloudAuthManager Instance { get; private set; }

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
    public async Task SignInWithUnityPlayerAccount(bool isSigningUp = false)
    {
        // Cerramos cualquier sesión anterior (anónima o de usuario/contraseña)
        SignOutIfSignedIn();

        await InitializeUnityServices();

        // Evitar suscribirse dos veces
        PlayerAccountService.Instance.SignedIn -= OnUnityPlayerAccountSignedIn;
        PlayerAccountService.Instance.SignedIn += OnUnityPlayerAccountSignedIn;

        try
        {
            // Esto es lo que abre el navegador y muestra la pantalla de Unity / Google / etc.
            await PlayerAccountService.Instance.StartSignInAsync(isSigningUp);
        }
        catch (PlayerAccountsException ex)
        {
            Debug.LogError("Error al iniciar flujo de Unity Player Accounts: " + ex);
            OnSignInFailed?.Invoke("No se pudo abrir el inicio de sesión de Unity. Revisa tu conexión.");
        }
        catch (RequestFailedException ex)
        {
            Debug.LogError("Error al iniciar flujo de Unity Player Accounts (RequestFailed): " + ex);
            OnSignInFailed?.Invoke("Error de conexión. Inténtalo de nuevo.");
        }
    }

    public async Task UpdatePlayerNameAsync(string newName)
    {
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

    public async Task SignInAnonymouslyIfNeeded()
    {
        // Aseguramos servicios
        await InitializeUnityServices();

        // Si ya hay sesión, solo usamos esa y nos aseguramos de tener datos locales
        if (AuthenticationService.Instance.IsSignedIn)
        {
            Debug.Log("CloudAuthManager: ya había una sesión activa, usando la existente (puede ser anónima).");

            // Si LocalPlayerData está “vacío”, nos aseguramos de rellenarlo
            if (LocalPlayerData.Username.Length == 0)
            {
                string authName = GetPlayerName();
                if (!string.IsNullOrWhiteSpace(authName))
                {
                    var tmp = LocalPlayerData;
                    tmp.Username = new FixedString64Bytes(authName);
                    UpdateLocalData(tmp);
                }
                else
                {
                    // Nombre fallback
                    LocalPlayerData = new PlayerData(0, "Player");
                }
            }

            OnSignInSuccess?.Invoke();
            return;
        }

        // Si no había sesión, creamos una anónima
        try
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            playerId = AuthenticationService.Instance.PlayerId;
            // Puede que GetPlayerNameAsync devuelva vacío para anónimos, pero lo intentamos
            try
            {
                playerName = await AuthenticationService.Instance.GetPlayerNameAsync();
            }
            catch
            {
                playerName = null;
            }

            Debug.Log($"CloudAuthManager: sesión anónima creada. PlayerID: {playerId}, Name: {playerName}");

            // Cargamos progreso desde Cloud Save (si hay)
            await LoadPlayerProgress();

            // Aseguramos que el PlayerData tenga un nombre visible
            if (LocalPlayerData.Username.Length == 0)
            {
                string authName = GetPlayerName();
                string nameToUse = string.IsNullOrWhiteSpace(authName)
                    ? $"Invitado_{playerId.Substring(0, 6)}"
                    : authName;

                var tmp = LocalPlayerData;
                tmp.Username = new FixedString64Bytes(nameToUse);
                UpdateLocalData(tmp);
            }

            // Guardamos por si acaso
            await SavePlayerProgress();

            OnSignInSuccess?.Invoke();
        }
        catch (AuthenticationException ex)
        {
            Debug.LogException(ex);
            OnSignInFailed?.Invoke("No se pudo iniciar sesión anónima.");
        }
        catch (RequestFailedException ex)
        {
            Debug.LogException(ex);
            OnSignInFailed?.Invoke("Error de conexión al iniciar sesión anónima.");
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
    private async void OnUnityPlayerAccountSignedIn()
    {
        // Importante: este evento se dispara cuando el navegador ya devolvió los tokens de Unity Player Accounts
        try
        {
            // Aquí creas/inicias sesión en Authentication con el token de Unity Player Accounts
            await AuthenticationService.Instance.SignInWithUnityAsync(PlayerAccountService.Instance.AccessToken);

            playerId = AuthenticationService.Instance.PlayerId;
            playerName = await AuthenticationService.Instance.GetPlayerNameAsync();

            Debug.Log($"Sign In via Unity Player Accounts OK. Player ID: {playerId}, Player Name: {playerName}");

            // Cargamos progreso desde Cloud Save como en el login normal
            await LoadPlayerProgress();

            // Si tu PlayerData aún no tiene nombre, lo rellenamos con el de Unity
            if (LocalPlayerData.Username.Length == 0 && !string.IsNullOrWhiteSpace(playerName))
            {
                var tmp = LocalPlayerData;
                tmp.Username = new FixedString64Bytes(playerName);
                UpdateLocalData(tmp);
            }

            OnSignInSuccess?.Invoke();
        }
        catch (AuthenticationException ex)
        {
            Debug.LogException(ex);
            OnSignInFailed?.Invoke("Error al iniciar sesión con la cuenta de Unity.");
        }
        catch (RequestFailedException ex)
        {
            Debug.LogException(ex);
            OnSignInFailed?.Invoke("Error de conexión al validar la cuenta de Unity.");
        }
        catch (Exception ex)
        {
            Debug.LogError("Error inesperado en OnUnityPlayerAccountSignedIn: " + ex);
            OnSignInFailed?.Invoke("Error inesperado al iniciar sesión.");
        }
        finally
        {
            // Evitar múltiples suscripciones
            PlayerAccountService.Instance.SignedIn -= OnUnityPlayerAccountSignedIn;
        }
    }


    public async Task SavePlayerProgress()
    {
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

    public void UpdateLocalData(PlayerData data)
    {
        LocalPlayerData = data;
    }
    public string GetPlayerName() => playerName;
    public string GetPlayerId() => playerId;
}