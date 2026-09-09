using UnityEngine;

public class HunterCrosshair : MonoBehaviour
{
    [SerializeField] private GameObject crosshair;

    private void Start()
    {
        bool isHunter = GameManager.IsLocalAssassin();

        if (crosshair != null)
            crosshair.SetActive(isHunter);
    }
}