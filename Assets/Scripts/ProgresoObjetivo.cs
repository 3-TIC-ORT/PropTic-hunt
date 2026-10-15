using UnityEngine;
using UnityEngine.UI;

public class ProgresoObjetivo : MonoBehaviour
{
    [SerializeField] private Image relleno;

    public void Actualizar(int actual, int meta)
    {
        meta = Mathf.Max(1, meta);
        actual = Mathf.Clamp(actual, 0, meta);
        relleno.fillAmount = (float)actual / meta;
    }

    [ContextMenu("Probar 40%")]
    private void Probar() { Actualizar(2, 5); }
}