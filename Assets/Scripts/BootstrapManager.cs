// Archivo: BootstrapManager.cs
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Este script solo existe en la escena Bootstrap.
/// Su �nica funci�n es cargar la escena del men� principal o login
/// tan pronto como el juego se inicia.
/// </summary>
public class BootstrapManager : MonoBehaviour
{
    [Tooltip("El nombre de la escena a la que quieres ir despu�s del arranque.")]
    [SerializeField] private string sceneToLoad = "Login";

    void Start()
    {
        // Cargamos la escena de Login. Los objetos con DontDestroyOnLoad
        // (GameManager, NetworkManager) sobrevivir�n a este cambio.
        SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
    }
}