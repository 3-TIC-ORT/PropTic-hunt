using UnityEngine;
using UnityEngine.UI;

public class ProgresoObjetivo : MonoBehaviour
{
    [SerializeField] private Image relleno;

    private void Awake()
    {
        // Asegura la configuración correcta por código y arranca vacía
        relleno.type = Image.Type.Filled;
        relleno.fillMethod = Image.FillMethod.Horizontal;
        relleno.fillOrigin = (int)Image.OriginHorizontal.Left;
        relleno.fillAmount = 0f;
    }

    public void Actualizar(int actual, int meta)
    {
        meta = Mathf.Max(1, meta);
        actual = Mathf.Clamp(actual, 0, meta);
        relleno.fillAmount = (float)actual / meta;
    }

    [ContextMenu("Probar 40%")]
    private void Probar() { Actualizar(2, 5); }

    [ContextMenu("Probar 0%")]
    private void ProbarVacio() { Actualizar(0, 5); }

    [ContextMenu("Probar 100%")]
    private void ProbarLleno() { Actualizar(5, 5); }
}