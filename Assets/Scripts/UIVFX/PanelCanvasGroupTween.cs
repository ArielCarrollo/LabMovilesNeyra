using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;

[RequireComponent(typeof(CanvasGroup))]
public class PanelCanvasGroupTween : MonoBehaviour
{
    [Header("Show")]
    public float showDuration = 0.25f;
    public float showScale = 1.0f;
    public Ease showEase = Ease.OutQuad;

    [Header("Hide")]
    public float hideDuration = 0.2f;
    public float hideScale = 0.9f;
    public Ease hideEase = Ease.InQuad;

    [Header("Options")]
    public bool playShowOnEnable = true;

    [Header("Events")]
    public UnityEvent onShown;
    public UnityEvent onHidden;

    private CanvasGroup _canvasGroup;
    private RectTransform _rect;
    private Sequence _sequence;
    private Vector3 _originalScale;
    private bool _initialized;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _rect = transform as RectTransform;
        _originalScale = _rect.localScale;
        _initialized = true;
    }

    private void OnEnable()
    {
        if (!_initialized) return;

        KillSequence();

        if (playShowOnEnable)
        {
            // Empieza oculto y entra animado
            _canvasGroup.alpha = 0f;
            _rect.localScale = _originalScale * hideScale;
            Show();
        }
    }

    private void OnDisable()
    {
        KillSequence();
    }

    private void KillSequence()
    {
        if (_sequence != null)
        {
            _sequence.Kill();
            _sequence = null;
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);
        KillSequence();

        _sequence = DOTween.Sequence();
        _sequence.SetUpdate(true); // unscaled time

        _sequence.Join(_canvasGroup.DOFade(1f, showDuration));
        _sequence.Join(_rect.DOScale(_originalScale * showScale, showDuration).SetEase(showEase));

        _sequence.OnComplete(() =>
        {
            _canvasGroup.alpha = 1f;
            _rect.localScale = _originalScale * showScale;
            onShown?.Invoke();
            _sequence = null;
        });
    }

    public void HideAndDisable()
    {
        KillSequence();

        _sequence = DOTween.Sequence();
        _sequence.SetUpdate(true); // unscaled time

        _sequence.Join(_canvasGroup.DOFade(0f, hideDuration));
        _sequence.Join(_rect.DOScale(_originalScale * hideScale, hideDuration).SetEase(hideEase));

        _sequence.OnComplete(() =>
        {
            _canvasGroup.alpha = 0f;
            _rect.localScale = _originalScale * hideScale;
            onHidden?.Invoke();
            _sequence = null;
            gameObject.SetActive(false);
        });
    }

    // Opcional: mostrar/ocultar instantáneo (sin animación)
    public void ShowInstant()
    {
        KillSequence();
        gameObject.SetActive(true);
        _canvasGroup.alpha = 1f;
        _rect.localScale = _originalScale * showScale;
        onShown?.Invoke();
    }

    public void HideInstant()
    {
        KillSequence();
        _canvasGroup.alpha = 0f;
        _rect.localScale = _originalScale * hideScale;
        gameObject.SetActive(false);
        onHidden?.Invoke();
    }
}
