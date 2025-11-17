using UnityEngine;

public class GenericEnableDisable : MonoBehaviour
{
    public void EnableObject(GameObject objectToControl)
    {
        if (objectToControl == null) return;

        var panelTween = objectToControl.GetComponent<PanelCanvasGroupTween>();
        if (panelTween != null)
        {
            panelTween.Show();
        }
        else
        {
            objectToControl.SetActive(true);
        }
    }

    public void Disable(GameObject objectToControl)
    {
        if (objectToControl == null) return;

        var panelTween = objectToControl.GetComponent<PanelCanvasGroupTween>();
        if (panelTween != null)
        {
            panelTween.HideAndDisable();
        }
        else
        {
            objectToControl.SetActive(false);
        }
    }
}
