using UnityEngine;


using Unity.Services.Authentication;
using Unity.Services.Core;
using System.Threading.Tasks;

public class AnonymAuth : MonoBehaviour
{
    private async void Start()
    {
        await UnityServices.InitializeAsync();//->Inicializar los servicios de UGS
        Debug.Log(UnityServices.State);
        SetupEvents();

        await SignInAnonymouslyAsync();
    }
    private void SetupEvents()
    {
        AuthenticationService.Instance.SignedIn += () =>
        {
            Debug.Log("Player ID " + AuthenticationService.Instance.PlayerId);
            Debug.Log("Acces Token " + AuthenticationService.Instance.AccessToken);
        };

        AuthenticationService.Instance.SignInFailed += (err) =>
        {
            Debug.Log(err);
        };
        AuthenticationService.Instance.SignedOut += () =>
        {
            Debug.Log("Player log out");
        };
        AuthenticationService.Instance.Expired += () =>
        {
            Debug.Log("Player session expired");
        };
    }
    private async Task SignInAnonymouslyAsync()
    {
        try
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            /* Debug.Log("Sign in anon succeeded");
             Debug.Log("Player ID :" + AuthenticationService.Instance.PlayerId);*/
        }
        catch (AuthenticationException ex)
        {
            Debug.LogError(ex);
        }
        catch (RequestFailedException ex)
        {
            Debug.LogError(ex);
        }
    }


}