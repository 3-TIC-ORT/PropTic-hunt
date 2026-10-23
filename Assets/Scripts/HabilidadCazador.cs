using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

public class HabilidadCazador : MonoBehaviourPun
{
    public Key tecla = Key.Q;
    public int disparosProtegidos = 3;
    public float tiempoRecarga = 40f;

    private int disparosRestantes;
    private float recargaHasta;

    public bool Activa => disparosRestantes > 0;
    public bool Lista => !Activa && Time.time >= recargaHasta;

    private void Update()
    {
        if (!photonView.IsMine || !GameManager.IsLocalAssassin()) return;
        if (ConfirmarSalida.MenuAbierto) return;

        if (Keyboard.current != null && Keyboard.current[tecla].wasPressedThisFrame && Lista)
        {
            disparosRestantes = disparosProtegidos;
            Debug.Log("[Habilidad] Activada: " + disparosProtegidos + " disparos protegidos");
        }
    }

    // Devuelve true si este disparo estaba protegido, y gasta uno de los protegidos
    public bool ConsumirDisparo()
    {
        if (!Activa) return false;

        disparosRestantes--;
        Debug.Log("[Habilidad] Disparos protegidos restantes: " + disparosRestantes);

        if (disparosRestantes == 0)
        {
            recargaHasta = Time.time + tiempoRecarga;
            Debug.Log("[Habilidad] En recarga por " + tiempoRecarga + " segundos");
        }
        return true;
    }
}