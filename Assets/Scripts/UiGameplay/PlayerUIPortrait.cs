using UnityEngine;
using UnityEngine.UI;
using TMPro; // Si usas TextMeshPro para el nombre

public class PlayerUIPortrait : MonoBehaviour
{
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private Slider healthSlider; // Opcional, pero recomendado
    [SerializeField] private TextMeshProUGUI playerNameText; // Opcional

    private CharacterBase targetPlayer;

    /// <summary>
    /// Esta función es llamada por el UIManager para conectar 
    /// esta UI con un jugador específico.
    /// </summary>
    public void Initialize(CharacterBase player)
    {
        this.targetPlayer = player;

        // Configurar los valores máximos
        staminaSlider.maxValue = targetPlayer.EstaminaMaxima;
        // healthSlider.maxValue = targetPlayer.VidaMaxima; // Necesitarías añadir 'VidaMaxima' como propiedad en CharacterBase

        // Suscribirse a los cambios de las NetworkVariables
        targetPlayer.Estamina.OnValueChanged += OnStaminaChanged;
        targetPlayer.Vida.OnValueChanged += OnHealthChanged;

        // Actualizar los valores iniciales
        OnStaminaChanged(0, targetPlayer.Estamina.Value);
        OnHealthChanged(0, targetPlayer.Vida.Value);

        // Poner el nombre (Ej. "Player 1", "Player 2", etc.)
        if (playerNameText != null)
        {
            playerNameText.text = "Player " + (targetPlayer.OwnerClientId + 1);
        }
    }

    private void OnStaminaChanged(float previousValue, float newValue)
    {
        staminaSlider.value = newValue;
    }

    private void OnHealthChanged(int previousValue, int newValue)
    {
        // Asumiendo que 'healthSlider.maxValue' ya está puesto
        healthSlider.value = newValue;
    }

    /// <summary>
    /// Limpieza: Cuando el retrato se destruye, deja de escuchar al jugador.
    /// </summary>
    private void OnDestroy()
    {
        if (targetPlayer != null)
        {
            targetPlayer.Estamina.OnValueChanged -= OnStaminaChanged;
            targetPlayer.Vida.OnValueChanged -= OnHealthChanged;
        }
    }
}