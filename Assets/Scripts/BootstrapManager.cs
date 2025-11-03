using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks; // Importante para async
using Unity.Services.Core; // Para ServicesInitializationState

/// <summary>
/// Este script solo existe en la escena Bootstrap.
/// Su función es inicializar Unity Services y LUEGO
/// cargar la escena de Login.
/// </summary>
public class BootstrapManager : MonoBehaviour
{
    [Tooltip("El nombre de la escena a la que quieres ir después del arranque.")]
    [SerializeField] private string sceneToLoad = "Login";

    // Convertimos Start() en un método asíncrono
    async void Start()
    {
        try
        {
            // Evitar doble inicialización si ya se hizo
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                CloudAuthManager authManager = FindObjectOfType<CloudAuthManager>();
                if (authManager == null)
                {
                    Debug.LogError("¡BootstrapManager no pudo encontrar CloudAuthManager en la escena 'Bootstrap'!");
                    return;
                }

                // 2. Esperamos a que termine la inicialización de servicios
                Debug.Log("Bootstrap: Inicializando Unity Services...");
                await authManager.InitializeUnityServices();
                Debug.Log("Bootstrap: Unity Services inicializados.");
            }

            Debug.Log("Bootstrap: Cargando escena 'Login'...");
            SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Fallo en el arranque (Bootstrap): " + e.Message);
        }
    }
}
