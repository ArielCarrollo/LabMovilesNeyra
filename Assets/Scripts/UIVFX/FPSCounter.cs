using UnityEngine;
using TMPro;

public class FPSCounter : MonoBehaviour
{
    public static FPSCounter Instance;

    [Header("Referencia al texto TMP")]
    [SerializeField] private TextMeshProUGUI fpsText;

    [Header("Suavizado")]
    [SerializeField] private float updateInterval = 0.2f;

    private float timeCounter;
    private int framesCounter;

    private void Awake()
    {
        // Singleton básico
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (fpsText == null)
        {
            Debug.LogWarning("FPSCounter: Asigna el TextMeshProUGUI en el inspector.");
        }
    }

    private void Update()
    {
        timeCounter += Time.unscaledDeltaTime;
        framesCounter++;

        if (timeCounter >= updateInterval)
        {
            float fps = framesCounter / timeCounter;
            if (fpsText != null)
            {
                fpsText.text = Mathf.RoundToInt(fps).ToString() + " FPS";
            }

            timeCounter = 0f;
            framesCounter = 0;
        }
    }
}
