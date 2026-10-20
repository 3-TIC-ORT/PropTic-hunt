using UnityEngine;

public class ConfirmarSalida : MonoBehaviour
{
    [SerializeField] private GameObject panelConfirmacion;

    private void Start()
    {
        // El panel empieza oculto.
        panelConfirmacion.SetActive(false);
    }

    // Se ejecuta al tocar el botón rojo.
    public void AbrirPanel()
    {
        panelConfirmacion.SetActive(true);
    }

    // Se ejecuta al tocar "No".
    public void CerrarPanel()
    {
        panelConfirmacion.SetActive(false);
    }
}