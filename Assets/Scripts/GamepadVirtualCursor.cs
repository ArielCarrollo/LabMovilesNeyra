using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(RectTransform))]
public class GamepadVirtualCursor : MonoBehaviour
{
    [Header("Referencias")]
    public Canvas canvas;                     // Canvas donde está el cursor
    public GraphicRaycaster raycaster;        // Raycaster del Canvas (UI)

    [Header("Movimiento")]
    public bool useRightStick = false;        // Usa el stick derecho si es true, izquierdo si es false
    public float cursorSpeed = 1000f;         // Velocidad del puntero

    private RectTransform cursorRect;         // RectTransform del puntero
    private RectTransform parentRect;         // RectTransform padre (límites)
    private EventSystem eventSystem;

    // Para hover y clicks
    private PointerEventData pointerData;
    private GameObject currentHover;
    private GameObject pressedObject;
    private Vector2 lastScreenPos;

    private void Awake()
    {
        cursorRect = GetComponent<RectTransform>();

        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (raycaster == null && canvas != null)
            raycaster = canvas.GetComponent<GraphicRaycaster>();

        if (canvas != null)
            parentRect = canvas.GetComponent<RectTransform>();

        eventSystem = EventSystem.current;

        if (eventSystem == null)
        {
            Debug.LogWarning("No hay EventSystem en la escena. Los eventos de UI no funcionarán.");
        }

        pointerData = new PointerEventData(eventSystem);

        // Opcional: ocultar cursor del sistema en PC
        Cursor.visible = false;
    }

    private void Update()
    {
        Gamepad gamepad = Gamepad.current;
        if (gamepad == null)
            return; // No hay mando conectado

        // --- Movimiento con stick ---
        Vector2 input = useRightStick ? gamepad.rightStick.ReadValue()
                                      : gamepad.leftStick.ReadValue();

        if (input.sqrMagnitude > 0.0001f)
        {
            MoveCursor(input);
        }

        // Actualizar hover cada frame (aunque no se mueva, por si cambió algo en UI)
        UpdateHover();

        // --- Click con botón South ---
        if (gamepad.buttonSouth.wasPressedThisFrame)
        {
            PressUnderCursor();
        }

        if (gamepad.buttonSouth.wasReleasedThisFrame)
        {
            ReleaseUnderCursor();
        }
    }

    private void MoveCursor(Vector2 input)
    {
        if (parentRect == null)
            return;

        Vector2 anchoredPos = cursorRect.anchoredPosition;
        anchoredPos += input * cursorSpeed * Time.unscaledDeltaTime;

        Rect rect = parentRect.rect;
        anchoredPos.x = Mathf.Clamp(anchoredPos.x, rect.xMin, rect.xMax);
        anchoredPos.y = Mathf.Clamp(anchoredPos.y, rect.yMin, rect.yMax);

        cursorRect.anchoredPosition = anchoredPos;
    }

    private void UpdateHover()
    {
        if (raycaster == null || eventSystem == null || canvas == null)
            return;

        // Posición del puntero en pantalla
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, cursorRect.position);

        // Actualizar PointerEventData
        pointerData.Reset();
        pointerData.position = screenPos;
        pointerData.delta = screenPos - lastScreenPos;
        lastScreenPos = screenPos;

        List<RaycastResult> results = new List<RaycastResult>();
        raycaster.Raycast(pointerData, results);

        GameObject newHover = results.Count > 0 ? results[0].gameObject : null;

        if (newHover != currentHover)
        {
            // Notificar salida del anterior
            if (currentHover != null)
            {
                ExecuteEvents.Execute(currentHover, pointerData, ExecuteEvents.pointerExitHandler);
            }

            currentHover = newHover;

            // Notificar entrada al nuevo
            if (currentHover != null)
            {
                ExecuteEvents.Execute(currentHover, pointerData, ExecuteEvents.pointerEnterHandler);
                eventSystem.SetSelectedGameObject(currentHover);
            }
        }

        // Notificar movimiento de puntero al elemento actual
        if (currentHover != null)
        {
            ExecuteEvents.Execute(currentHover, pointerData, ExecuteEvents.pointerMoveHandler);
        }
    }

    private void PressUnderCursor()
    {
        if (currentHover == null || eventSystem == null)
            return;

        pressedObject = currentHover;

        // Guardar posición de presionado (por si lo necesitas)
        pointerData.pressPosition = pointerData.position;
        pointerData.pointerPressRaycast = new RaycastResult { gameObject = pressedObject };

        ExecuteEvents.Execute(pressedObject, pointerData, ExecuteEvents.pointerDownHandler);
        eventSystem.SetSelectedGameObject(pressedObject);
    }

    private void ReleaseUnderCursor()
    {
        if (pressedObject == null || eventSystem == null)
            return;

        ExecuteEvents.Execute(pressedObject, pointerData, ExecuteEvents.pointerUpHandler);

        // Si soltamos sobre el mismo objeto donde presionamos → Click
        if (pressedObject == currentHover)
        {
            ExecuteEvents.Execute(pressedObject, pointerData, ExecuteEvents.pointerClickHandler);
        }

        pressedObject = null;
    }
}
