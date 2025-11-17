using UnityEngine;
using UnityEngine.UI; // ¡Importante para el Slider!
using Unity.Netcode;

public class PlayerStaminaUI : MonoBehaviour
{
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private CharacterBase characterBase;
    private Camera mainCamera;

    void Start()
    {
        // Encontrar la cámara principal
        mainCamera = Camera.main;

        // Configurar la barra (usando la propiedad pública que creamos)
        staminaSlider.maxValue = characterBase.EstaminaMaxima;
        staminaSlider.value = characterBase.Estamina.Value;

        // ¡LA MAGIA! Suscribirse al evento de cambio de la NetworkVariable
        characterBase.Estamina.OnValueChanged += OnStaminaChanged;
    }

    // Esta función se llamará SOLA cada vez que Estamina.Value cambie
    private void OnStaminaChanged(float previousValue, float newValue)
    {
        staminaSlider.value = newValue;
    }

    // Efecto "Billboard" para que la UI siempre mire a la cámara
    void LateUpdate()
    {
        if (mainCamera == null) return;

        transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                         mainCamera.transform.rotation * Vector3.up);
    }

    // Buena práctica: desuscribirse cuando el objeto se destruye
    void OnDestroy()
    {
        if (characterBase != null)
        {
            characterBase.Estamina.OnValueChanged -= OnStaminaChanged;
        }
    }
}