using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode; // ¡Importante!

public class PlayerUIPortrait : MonoBehaviour
{
    [Header("Componentes de UI")]
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI scoreText;

    private CharacterBase targetPlayer;
    private ulong myPlayerId;

    public void Initialize(CharacterBase player)
    {
        this.targetPlayer = player;
        this.myPlayerId = player.OwnerClientId; // ¡Guardamos el ID!

        // --- Configuración de Estamina ---
        staminaSlider.maxValue = targetPlayer.EstaminaMaxima;
        targetPlayer.Estamina.OnValueChanged += OnStaminaChanged;
        OnStaminaChanged(0, targetPlayer.Estamina.Value);

        // --- Configuración de Nombre ---
        if (playerNameText != null)
        {
            playerNameText.text = "Player " + (myPlayerId + 1);
        }

        // --- ¡LÓGICA DE SUSCRIPCIÓN MEJORADA! ---
        if (MinigameManager.Instance != null)
        {
            // ¡Éxito! Nos suscribimos
            MinigameManager.Instance.PlayerPoints.OnListChanged += OnScoreListChanged;
            Debug.Log($"PlayerUIPortrait (Player {myPlayerId}): ¡Suscripción a puntos EXITOSA!");

            // Actualizamos el puntaje una vez al inicio
            UpdateScoreText();
        }
        else
        {
            // ¡Fracaso! Si ves esto, el Script Execution Order está mal.
            Debug.LogError($"PlayerUIPortrait (Player {myPlayerId}): ¡FALLO DE SUSCRIPCIÓN! MinigameManager.Instance era NULL.");
        }
    }

    /// <summary>
    /// Esta función se llamará AUTOMÁTICAMENTE en todos los clientes
    /// cada vez que el servidor cambie la lista de puntajes.
    /// </summary>
    private void OnScoreListChanged(NetworkListEvent<PlayerScore> changeEvent)
    {
        // No importa qué cambió, volvemos a buscar nuestro puntaje
        UpdateScoreText();
    }

    /// <summary>
    /// Una función "ayudante" para buscar y actualizar nuestro texto de puntaje
    /// </summary>
    private void UpdateScoreText()
    {
        if (MinigameManager.Instance == null || scoreText == null) return;

        int currentScore = MinigameManager.Instance.GetPlayerScore(myPlayerId);

        // Debug para ver si la UI recibe el dato
        Debug.Log($"UI (Player {myPlayerId}): Actualizando texto a {currentScore}");

        scoreText.text = "Puntos: " + currentScore.ToString();

        // Truco: Forzar actualización visual por si acaso
        scoreText.SetAllDirty();
    }


    // --- ¡HEMOS ELIMINADO LA FUNCIÓN Update() COMPLETAMENTE! ---
    // (Ya no la necesitamos)


    // --- Funciones de Estamina/Vida (Siguen igual) ---
    private void OnStaminaChanged(float previousValue, float newValue)
    {
        staminaSlider.value = newValue;
    }

    private void OnHealthChanged(int previousValue, int newValue)
    {
        healthSlider.value = newValue;
    }

    // --- Limpieza (¡IMPORTANTE!) ---
    private void OnDestroy()
    {
        // Nos desuscribimos de todo
        if (targetPlayer != null)
        {
            targetPlayer.Estamina.OnValueChanged -= OnStaminaChanged;
            // targetPlayer.Vida.OnValueChanged -= OnHealthChanged;
        }

        // ¡No olvides desuscribirte de la lista también!
        if (MinigameManager.Instance != null)
        {
            MinigameManager.Instance.PlayerPoints.OnListChanged -= OnScoreListChanged;
        }
    }
}