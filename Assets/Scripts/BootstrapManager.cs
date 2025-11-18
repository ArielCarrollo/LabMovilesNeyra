using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using Unity.Services.Core;

public class BootstrapManager : MonoBehaviour
{
    [Tooltip("El nombre de la escena a la que quieres ir después del arranque.")]
    [SerializeField] private string sceneToLoad = "IntroLogo"; // o "Login", como lo tengas

    async void Start()
    {
        try
        {
            // MODIFICACIÓN: Bloque Xbox Offline
#if UNITY_WSA_10_0
            Debug.Log("Bootstrap (Xbox): Saltando inicialización de Unity Services (Modo Offline).");
            // No inicializamos AuthManager ni UnityServices aquí.
            // Pasamos directo a cargar la escena.
#else
            // Lógica original para otras plataformas
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                CloudAuthManager authManager = FindObjectOfType<CloudAuthManager>();
                if (authManager == null)
                {
                    Debug.LogError("¡BootstrapManager no pudo encontrar CloudAuthManager!");
                    return;
                }

                Debug.Log("Bootstrap: Inicializando Unity Services...");
                await authManager.InitializeUnityServices();
                Debug.Log("Bootstrap: Unity Services inicializados.");
            }
#endif

            Debug.Log($"Bootstrap: Cargando escena '{sceneToLoad}'...");

            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.LoadSceneWithFade(sceneToLoad);
            }
            else
            {
                SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Fallo en el arranque (Bootstrap): " + e.Message);
        }
    }
}
