// Archivo: BootstrapManager.cs
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Este script solo existe en la escena Bootstrap.
/// Su única función es cargar la escena del menú principal o login
/// tan pronto como el juego se inicia.
/// </summary>
public class BootstrapManager : MonoBehaviour
{
    [Tooltip("El nombre de la escena a la que quieres ir después del arranque.")]
    [SerializeField] private string sceneToLoad = "Login";

    void Start()
    {
        // Cargamos la escena de Login. Los objetos con DontDestroyOnLoad
        // (GameManager, NetworkManager) sobrevivirán a este cambio.
        SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
    }
}