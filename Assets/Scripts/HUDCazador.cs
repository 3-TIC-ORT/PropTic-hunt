using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDCazador : MonoBehaviour
{
    [Header("Panel (se muestra solo al Cazador)")]
    public GameObject panel;

    [Header("Habilidad")]
    public Image fondoTecla;
    public TMP_Text textoTecla;
    public TMP_Text textoHabilidad;
    public Color colorLista = Color.white;
    public Color colorActiva = new Color(1f, 0.85f, 0.2f);
    public Color colorRecarga = new Color(0.5f, 0.5f, 0.5f);

    [Header("Vida")]
    public Image relleno;
    public TMP_Text textoVida;
    public Color colorVidaLlena = new Color(0.2f, 0.8f, 0.3f);
    public Color colorVidaBaja = new Color(0.85f, 0.15f, 0.15f);

    private void Update()
    {
        VidaCazador vida = VidaCazador.Local;
        bool mostrar = vida != null && GameManager.IsLocalAssassin();

        if (panel != null && panel.activeSelf != mostrar) panel.SetActive(mostrar);
        if (!mostrar) return;

        // Vida
        float fraccion = vida.vidaMaxima > 0 ? (float)vida.VidaActual / vida.vidaMaxima : 0f;
        relleno.rectTransform.localScale = new Vector3(fraccion, 1f, 1f);
        relleno.color = Color.Lerp(colorVidaBaja, colorVidaLlena, fraccion);
        textoVida.text = vida.VidaActual.ToString();

        // Habilidad
        HabilidadCazador hab = vida.GetComponent<HabilidadCazador>();
        if (hab == null) return;

        string tecla = hab.tecla.ToString().ToUpper();
        textoTecla.text = tecla;

        if (hab.Activa)
        {
            fondoTecla.color = colorActiva;
            textoHabilidad.text = "Habilidad activa: " + hab.DisparosRestantes + " disparos protegidos";
        }
        else if (hab.SegundosRecarga > 0f)
        {
            fondoTecla.color = colorRecarga;
            textoHabilidad.text = "Habilidad en recarga: " + Mathf.CeilToInt(hab.SegundosRecarga);
        }
        else
        {
            fondoTecla.color = colorLista;
            textoHabilidad.text = "Presioná " + tecla + " para activar la habilidad";
        }
    }
}