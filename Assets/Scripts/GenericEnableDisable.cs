using UnityEngine;

public class GenericEnableDisable : MonoBehaviour
{
    public void EnableObject(GameObject ObjectToControl)
    {
        ObjectToControl.SetActive(true);
    }

    public void Disable(GameObject ObjectToControl)
    {
        ObjectToControl.SetActive(false);
    }
}