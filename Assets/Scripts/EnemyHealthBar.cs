using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class EnemyHealthBar : NetworkBehaviour
{
    [Header("UI")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private GameObject healthBarCanvas;

    private EnemyAI enemyAI;
    private Camera mainCamera;

    public override void OnNetworkSpawn()
    {
        enemyAI = GetComponentInParent<EnemyAI>();
        if (enemyAI == null)
        {
            Debug.LogError("EnemyHealthBar: No se encontró el script EnemyAI en el padre.");
            enabled = false; // Desactiva este script si no encuentra al enemigo.
            return;
        }

        mainCamera = Camera.main;

        // 1. Configura los valores máximo y mínimo del slider.
        healthSlider.maxValue = enemyAI.GetMaxHealth();
        healthSlider.minValue = 0;

        // 2. Suscríbete al evento para futuros cambios de vida.
        enemyAI.CurrentHealth.OnValueChanged += OnHealthChanged;

        // 3. LA SOLUCIÓN: Llama manualmente a la función de actualización una vez.
        // Esto sincroniza la barra de vida con el valor que la variable TENGA en este preciso instante.
        // Si el valor del servidor ya llegó, se pondrá a 100. Si no, se pondrá a 0,
        // pero el evento OnValueChanged lo corregirá a 100 una fracción de segundo después.
        OnHealthChanged(0, enemyAI.CurrentHealth.Value);
    }

    private void OnHealthChanged(int previousValue, int newValue)
    {
        // Actualiza el valor del slider.
        healthSlider.value = newValue;
    }

    void LateUpdate()
    {
        // Esta lógica hace que la barra de vida siempre mire a la cámara.
        if (healthBarCanvas != null && mainCamera != null)
        {
            healthBarCanvas.transform.rotation = Quaternion.LookRotation(transform.position - mainCamera.transform.position);
        }
    }

    public override void OnNetworkDespawn()
    {
        // MUY IMPORTANTE: Darse de baja del evento para evitar errores.
        if (enemyAI != null)
        {
            enemyAI.CurrentHealth.OnValueChanged -= OnHealthChanged;
        }
    }
}