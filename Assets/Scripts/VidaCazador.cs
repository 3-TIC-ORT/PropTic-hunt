using System;
using Photon.Pun;
using UnityEngine;

public class VidaCazador : MonoBehaviourPun
{
    public static VidaCazador Local { get; private set; }
    public static event Action AlQuedarseSinVida;

    public int vidaMaxima = 40;

    public int VidaActual { get; private set; }
    public bool SinVida { get; private set; }

    private HabilidadCazador habilidad;

    private void Awake()
    {
        habilidad = GetComponent<HabilidadCazador>();
    }

    private void Start()
    {
        VidaActual = vidaMaxima;
        if (photonView.IsMine) Local = this;
    }

    private void OnDestroy()
    {
        if (Local == this) Local = null;
    }

    // La va a llamar la bala invisible cuando termine su recorrido
    public void RegistrarDisparo(bool acerto)
    {
        if (SinVida) return;

        bool protegido = habilidad != null && habilidad.ConsumirDisparo();
        if (acerto || protegido) return;

        VidaActual--;
        Debug.Log("[Cazador] Disparo fallado. Vida: " + VidaActual + "/" + vidaMaxima);

        if (VidaActual <= 0)
        {
            SinVida = true;
            Debug.Log("[Cazador] Se quedó sin vida: pierde y ganan los Props que siguen con vida");
            AlQuedarseSinVida?.Invoke();
        }
    }
}