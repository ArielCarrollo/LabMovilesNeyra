using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

[RequireComponent(typeof(Button))]
public class AnimatedButtonTween : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    [Header("Scale")]
    public bool useScaleAnimation = true;
    public float hoverScale = 1.05f;
    public float pressedScale = 0.95f;
    public float duration = 0.15f;
    public Ease ease = Ease.OutQuad;

    [Header("Optional Color")]
    public Graphic targetGraphic;
    public Color normalColor = Color.white;
    public Color hoverColor = Color.white;

    private Vector3 _originalScale;
    private Tween _scaleTween;
    private Tween _colorTween;

    private void Awake()
    {
        _originalScale = transform.localScale;
        if (targetGraphic == null)
            targetGraphic = GetComponent<Graphic>();
    }

    private void OnEnable()
    {
        KillTweens();
        transform.localScale = _originalScale;
        if (targetGraphic != null)
            targetGraphic.color = normalColor;
    }

    private void OnDisable()
    {
        KillTweens();
    }

    private void KillTweens()
    {
        _scaleTween?.Kill();
        _colorTween?.Kill();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!useScaleAnimation) return;

        KillTweens();
        _scaleTween = transform
            .DOScale(_originalScale * hoverScale, duration)
            .SetEase(ease)
            .SetUpdate(true); // tiempo real (unscaled)

        if (targetGraphic != null)
        {
            _colorTween = targetGraphic
                .DOColor(hoverColor, duration)
                .SetUpdate(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!useScaleAnimation) return;

        KillTweens();
        _scaleTween = transform
            .DOScale(_originalScale, duration)
            .SetEase(ease)
            .SetUpdate(true);

        if (targetGraphic != null)
        {
            _colorTween = targetGraphic
                .DOColor(normalColor, duration)
                .SetUpdate(true);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!useScaleAnimation) return;

        KillTweens();
        _scaleTween = transform
            .DOScale(_originalScale * pressedScale, duration * 0.8f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!useScaleAnimation) return;

        KillTweens();
        _scaleTween = transform
            .DOScale(_originalScale, duration)
            .SetEase(ease)
            .SetUpdate(true);
    }
}
