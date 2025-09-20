using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [SerializeField] private Slider playerHealthSlider;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    public void RegisterPlayer(NetworkVariable<int> playerHealth, int maxHealth)
    {
        if (playerHealthSlider == null)
        {
            Debug.LogError("No se ha asignado el Player Health Slider en el UIManager.");
            return;
        }

        playerHealthSlider.maxValue = maxHealth;

        playerHealthSlider.value = playerHealth.Value;

        playerHealth.OnValueChanged += (previousValue, newValue) =>
        {
            playerHealthSlider.value = newValue;
        };
    }
}