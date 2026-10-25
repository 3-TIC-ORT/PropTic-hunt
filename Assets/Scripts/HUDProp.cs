using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDProp : MonoBehaviour
{
    [Header("Panel (se muestra solo al Prop)")]
    public GameObject panel;

    public Image fondoTecla;
    public TMP_Text textoTecla;
    public TMP_Text textoSilbido;

    public Color colorLista = Color.white;
    public Color colorEspera = new Color(0.5f, 0.5f, 0.5f);

    private void Update()
    {
        SilbidoProp silbido = SilbidoProp.Local;

        bool mostrar = silbido != null
            && silbido.isActiveAndEnabled
            && RoleManager.HasKiller
            && (!GameManager.IsLocalAssassin() || silbido.permitirAlCazadorEnPruebas);

        if (panel != null && panel.activeSelf != mostrar) panel.SetActive(mostrar);
        if (!mostrar) return;

        string tecla = silbido.tecla.ToString().ToUpper();
        textoTecla.text = tecla;

        if (silbido.SegundosEspera > 0f)
        {
            fondoTecla.color = colorEspera;
            textoSilbido.text = "Silbido disponible en: " + Mathf.CeilToInt(silbido.SegundosEspera);
        }
        else
        {
            fondoTecla.color = colorLista;
            textoSilbido.text = "Presioná " + tecla + " para silbar";
        }
    }
}