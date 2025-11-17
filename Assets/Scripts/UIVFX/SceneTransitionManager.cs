using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

[RequireComponent(typeof(Canvas))]
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("Fade overlay")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float lobbyToGameplayFadeOut = 0.45f;
    [SerializeField] private float lobbyToGameplayFadeIn = 0.35f;
    [SerializeField] private float gameplayToLobbyFadeOut = 0.35f;
    [SerializeField] private float gameplayToLobbyFadeIn = 0.4f;
    [SerializeField] private float genericFadeOut = 0.4f;
    [SerializeField] private float genericFadeIn = 0.4f;

    private bool _isTransitioning;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        var canvas = GetComponent<Canvas>();
        // Muy importante: este canvas SIEMPRE arriba
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1000;

        if (fadeCanvasGroup == null)
        {
            fadeCanvasGroup = GetComponentInChildren<CanvasGroup>();
        }
    }

    private void Start()
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }
    }

    private IEnumerator TransitionRoutine(string sceneName, float fadeOutDuration, float fadeInDuration)
    {
        _isTransitioning = true;

        // Fade OUT (a negro)
        fadeCanvasGroup.blocksRaycasts = true;
        Tween fadeOut = fadeCanvasGroup
            .DOFade(1f, fadeOutDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true); // tiempo real

        yield return fadeOut.WaitForCompletion();

        // Cargar escena
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        while (!op.isDone)
        {
            yield return null;
        }

        // Fade IN (quitar negro)
        Tween fadeIn = fadeCanvasGroup
            .DOFade(0f, fadeInDuration)
            .SetEase(Ease.InQuad)
            .SetUpdate(true);

        yield return fadeIn.WaitForCompletion();

        fadeCanvasGroup.blocksRaycasts = false;
        _isTransitioning = false;
    }

    // ---------- API PÚBLICA ----------

    public void LoadSceneWithFade(string sceneName)
    {
        if (_isTransitioning || fadeCanvasGroup == null) return;
        StartCoroutine(TransitionRoutine(sceneName, genericFadeOut, genericFadeIn));
    }

    public void LoadGameplayFromLobby(string gameplaySceneName)
    {
        if (_isTransitioning || fadeCanvasGroup == null) return;
        StartCoroutine(TransitionRoutine(gameplaySceneName, lobbyToGameplayFadeOut, lobbyToGameplayFadeIn));
    }

    public void LoadLobbyFromGameplay(string lobbySceneName)
    {
        if (_isTransitioning || fadeCanvasGroup == null) return;
        StartCoroutine(TransitionRoutine(lobbySceneName, gameplayToLobbyFadeOut, gameplayToLobbyFadeIn));
    }

    public void LoadSceneInstant(string sceneName)
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}
