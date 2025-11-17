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
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                CloudAuthManager authManager = FindObjectOfType<CloudAuthManager>();
                if (authManager == null)
                {
                    Debug.LogError("¡BootstrapManager no pudo encontrar CloudAuthManager en la escena 'Bootstrap'!");
                    return;
                }

                Debug.Log("Bootstrap: Inicializando Unity Services...");
                await authManager.InitializeUnityServices();
                Debug.Log("Bootstrap: Unity Services inicializados.");
            }

            Debug.Log($"Bootstrap: Cargando escena '{sceneToLoad}'...");

            if (SceneTransitionManager.Instance != null)
            {
                // Ahora sí, usamos el fade genérico
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
