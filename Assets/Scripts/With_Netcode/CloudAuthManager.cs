using UnityEngine;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.CloudSave;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

public class CloudAuthManager : MonoBehaviour
{
    public static CloudAuthManager Instance { get; private set; }

    public event Action OnSignInSuccess;
    public event Action<string> OnSignInFailed;

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

    private async void Start()
    {
        try
        {
            await UnityServices.InitializeAsync();
            Debug.Log("Unity Services Initialized: " + UnityServices.State);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to initialize Unity Services: " + e);
        }
    }

    public async Task SignUpWithUsernamePassword(string username, string password)
    {
        try
        {
            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password);
            Debug.Log("Sign Up Successful.");

            // Después del registro, se inicia sesión automáticamente.
            await SignInWithUsernamePassword(username, password);
        }
        catch (AuthenticationException ex)
        {
            Debug.LogException(ex);
            OnSignInFailed?.Invoke(ex.Message);
        }
        catch (RequestFailedException ex)
        {
            Debug.LogException(ex);
            OnSignInFailed?.Invoke(ex.Message);
        }
    }

    public async Task SignInWithUsernamePassword(string username, string password)
    {
        try
        {
            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);

            playerId = AuthenticationService.Instance.PlayerId;
            playerName = await AuthenticationService.Instance.GetPlayerNameAsync();

            Debug.Log($"Sign In Successful. Player ID: {playerId}, Player Name: {playerName}");

            // Notificar al resto del juego que el inicio de sesión fue exitoso.
            OnSignInSuccess?.Invoke();
        }
        catch (AuthenticationException ex)
        {
            Debug.LogException(ex);
            OnSignInFailed?.Invoke("Usuario o contraseña incorrectos.");
        }
        catch (RequestFailedException ex)
        {
            Debug.LogException(ex);
            OnSignInFailed?.Invoke("Error de conexión. Inténtalo de nuevo.");
        }
    }

    public async Task SavePlayerData(string key, string value)
    {
        var data = new Dictionary<string, object> { { key, value } };
        await CloudSaveService.Instance.Data.Player.SaveAsync(data);
        Debug.Log($"Data saved: {key} = {value}");
    }

    public async Task<string> LoadPlayerData(string key)
    {
        var loadedData = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { key });
        if (loadedData.TryGetValue(key, out var value))
        {
            Debug.Log($"Data loaded: {key} = {value.Value.GetAs<string>()}");
            return value.Value.GetAs<string>();
        }
        return null;
    }

    public string GetPlayerName() => playerName;
    public string GetPlayerId() => playerId;
}