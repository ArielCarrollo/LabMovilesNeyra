using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class LogoIntroTransition : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private RectTransform logo;
    [SerializeField] private RectTransform pressText;

    [Tooltip("CanvasGroup SOLO del logo + texto (si quieres que se difuminen).")]
    [SerializeField] private CanvasGroup uiCanvasGroup;

    [Header("Fondo (cielo / tierra)")]
    [Tooltip("Padre que contiene tus fondos de cielo/tierra.")]
    [SerializeField] private RectTransform backgroundRoot;
    [Tooltip("Opcional: si quieres que el fondo también se difumine.")]
    [SerializeField] private CanvasGroup backgroundCanvasGroup;

    [Header("Movimiento")]
    [SerializeField] private float moveUpDistance = 200f;     // cuánto suben logo/texto
    [SerializeField] private float moveDownDistance = 150f;   // cuánto baja el fondo
    [SerializeField] private float moveDuration = 0.6f;
    [SerializeField] private Ease moveEase = Ease.InOutQuad;

    [Header("Escena siguiente")]
    [SerializeField] private string nextSceneName = "Lobby";

    private bool _isPlaying;

    private void Awake()
    {
        // Si no asignas uiCanvasGroup, intenta buscar uno en los hijos del logo
        if (uiCanvasGroup == null)
            uiCanvasGroup = GetComponentInChildren<CanvasGroup>();
    }

    public void OnClickAnywhere()
    {
        if (_isPlaying) return;
        _isPlaying = true;

        Vector2 logoStart = logo.anchoredPosition;
        Vector2 textStart = pressText.anchoredPosition;
        Vector2 bgStart = backgroundRoot != null ? backgroundRoot.anchoredPosition : Vector2.zero;

        Sequence seq = DOTween.Sequence().SetUpdate(true);

        // Logo + texto suben
        seq.Join(logo.DOAnchorPosY(logoStart.y + moveUpDistance, moveDuration)
                   .SetEase(moveEase));
        seq.Join(pressText.DOAnchorPosY(textStart.y + moveUpDistance, moveDuration)
                          .SetEase(moveEase));

        // Fondo baja (simula que "tú" vas hacia abajo)
        if (backgroundRoot != null)
        {
            seq.Join(backgroundRoot.DOAnchorPosY(bgStart.y - moveDownDistance, moveDuration)
                                   .SetEase(moveEase));
        }

        // Difuminado: UI y/o fondo
        if (uiCanvasGroup != null)
        {
            seq.Join(uiCanvasGroup.DOFade(0f, moveDuration)
                                  .SetEase(Ease.OutQuad));
        }

        if (backgroundCanvasGroup != null)
        {
            seq.Join(backgroundCanvasGroup.DOFade(0.6f, moveDuration) // un poco más oscuro
                                          .SetEase(Ease.OutQuad));
        }

        // Cuando termina la animación de la intro, activamos el fade global + cambio de escena
        seq.OnComplete(() =>
        {
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.LoadSceneWithFade(nextSceneName);
            }
            else
            {
                SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
            }
        });
    }
}
