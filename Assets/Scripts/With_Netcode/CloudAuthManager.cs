using UnityEngine;
using Unity.Services.Authentication;
using Unity.Services.Core;
using System;
using System.Threading.Tasks;
using Unity.Services.Core.Environments;
using Newtonsoft.Json;
using Unity.Services.CloudSave;
using System.Collections.Generic;

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
            options.SetEnvironmentName("production"); // O el entorno que est�s usando
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
        await InitializeUnityServices();
        try
        {
            // El registro inicia sesi�n autom�ticamente si tiene �xito
            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password);

            playerId = AuthenticationService.Instance.PlayerId;
            playerName = username; // Al registrar, el nombre de usuario es el nombre por defecto

            Debug.Log($"Sign Up & Sign In Successful. Player ID: {playerId}, Player Name: {playerName}");

            // Invocamos el �xito directamente desde aqu�
            await UpdatePlayerNameAsync(username);
            LocalPlayerData = new PlayerData(0, username); // Creamos datos por defecto
            await SavePlayerProgress();
            OnSignInSuccess?.Invoke();
        }
        catch (AuthenticationException ex)
        {
            // Convertimos los c�digos de error en mensajes claros para el usuario
            string errorMessage = ConvertExceptionToMessage(ex);
            Debug.LogException(ex);
            OnSignInFailed?.Invoke(errorMessage);
        }
        catch (RequestFailedException ex)
        {
            Debug.LogException(ex);
            OnSignInFailed?.Invoke("Error de conexi�n. Int�ntalo de nuevo.");
        }
    }
    public async Task UpdatePlayerNameAsync(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            Debug.LogError("El nombre no puede estar vac�o.");
            return;
        }

        try
        {
            await AuthenticationService.Instance.UpdatePlayerNameAsync(newName);
            this.playerName = newName;
            Debug.Log($"Nombre actualizado exitosamente a: {newName}");
            OnPlayerNameUpdated?.Invoke(newName); // Notificar a los suscriptores
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
        await InitializeUnityServices();
        try
        {
            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);

            playerId = AuthenticationService.Instance.PlayerId;
            playerName = await AuthenticationService.Instance.GetPlayerNameAsync();

            Debug.Log($"Sign In Successful. Player ID: {playerId}, Player Name: {playerName}");
            await LoadPlayerProgress();
            OnSignInSuccess?.Invoke();
        }
        catch (AuthenticationException)
        {
            OnSignInFailed?.Invoke("Usuario o contrase�a incorrectos.");
        }
        catch (RequestFailedException ex)
        {
            Debug.LogException(ex);
            OnSignInFailed?.Invoke("Error de conexi�n. Int�ntalo de nuevo.");
        }
    }

    private string ConvertExceptionToMessage(AuthenticationException ex)
    {
        // https://docs.unity.com/authentication/manual/exception-codes
        switch (ex.ErrorCode)
        {
            case 10200: // USERNAME_EXISTS
                return "Este nombre de usuario ya est� en uso.";
            case 10202: // INVALID_PASSWORD
                return "La contrase�a no es v�lida. Debe tener al menos 8 caracteres.";
            case 10203: // INVALID_USERNAME
                return "El nombre de usuario no es v�lido.";
            default:
                return "Error desconocido en el registro.";
        }
    }
    public async Task LoadPlayerProgress()
    {
        try
        {
            var serverData = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { PLAYER_PROGRESS_KEY });

            if (serverData.TryGetValue(PLAYER_PROGRESS_KEY, out var data))
            {
                // Si encontramos datos, los deserializamos
                string jsonData = data.Value.GetAs<string>();
                LocalPlayerData = JsonConvert.DeserializeObject<PlayerData>(jsonData);
                Debug.Log("Datos del jugador cargados desde la nube.");
            }
            else
            {
                // Si no hay datos (es la primera vez que inicia sesi�n tras la actualizaci�n), creamos datos por defecto
                Debug.Log("No se encontraron datos en la nube. Creando datos locales por defecto.");
                LocalPlayerData = new PlayerData(0, GetPlayerName()); // Usamos el nombre ya cargado
                await SavePlayerProgress(); // Y los guardamos en la nube para la pr�xima vez
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Error al cargar los datos del jugador: " + e);
            // Si falla la carga, usamos datos locales para no bloquear el juego
            LocalPlayerData = new PlayerData(0, GetPlayerName());
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
    public void UpdateLocalData(PlayerData data)
    {
        LocalPlayerData = data;
    }
    public string GetPlayerName() => playerName;
    public string GetPlayerId() => playerId;
}