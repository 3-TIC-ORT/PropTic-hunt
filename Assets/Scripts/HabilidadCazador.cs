using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

public class HabilidadCazador : MonoBehaviourPun
{
    public Key tecla = Key.Q;
    public int disparosProtegidos = 3;
    public float tiempoRecarga = 40f;

    [Tooltip("Frases grabadas que suenan al activar la habilidad. Si está vacía, no suena nada.")]
    public AudioClip[] frases;

    private int disparosRestantes;
    private float recargaHasta;

    private AudioSource fuente;

    // Bolsa de frases: salen todas antes de repetir
    private List<int> pendientes = new List<int>();
    private int ultimoIndice = -1;

    public bool Activa => disparosRestantes > 0;
    public bool Lista => !Activa && Time.time >= recargaHasta;
    public int DisparosRestantes => disparosRestantes;
    public float SegundosRecarga => Mathf.Max(0f, recargaHasta - Time.time);

    private void Awake()
    {
        fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 1f; // sonido 3D: se oye más fuerte cuanto más cerca del Cazador
        fuente.rolloffMode = AudioRolloffMode.Logarithmic;
        fuente.minDistance = 2f;
        fuente.maxDistance = 40f;
    }

    private void Update()
    {
        if (!photonView.IsMine || !GameManager.IsLocalAssassin()) return;
        if (ConfirmarSalida.MenuAbierto) return;
        if (RelojPartida.EnEspera) return;

        if (Keyboard.current != null && Keyboard.current[tecla].wasPressedThisFrame && Lista)
        {
            disparosRestantes = disparosProtegidos;
            EnviadorPartida.RegistrarHabilidad();
            Debug.Log("[Habilidad] Activada: " + disparosProtegidos + " disparos protegidos");

            // Solo la compu del Cazador elige la frase y manda el número a todos
            int indice = ElegirFrase();
            if (indice >= 0)
                photonView.RPC(nameof(RPC_DecirFraseHabilidad), RpcTarget.All, indice);
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

    // Devuelve el número de una frase, o -1 si no hay frases cargadas
    private int ElegirFrase()
    {
        if (frases == null || frases.Length == 0) return -1;
        if (frases.Length == 1) return 0;

        // Cuando la bolsa se vacía, se vuelve a llenar con todas
        if (pendientes.Count == 0)
        {
            for (int i = 0; i < frases.Length; i++) pendientes.Add(i);
        }

        int pos = Random.Range(0, pendientes.Count);

        // Al rellenar, evitamos repetir la última que sonó
        if (pendientes[pos] == ultimoIndice) pos = (pos + 1) % pendientes.Count;

        int indice = pendientes[pos];
        pendientes.RemoveAt(pos);
        ultimoIndice = indice;
        return indice;
    }

    [PunRPC]
    private void RPC_DecirFraseHabilidad(int indice)
    {
        if (frases == null || indice < 0 || indice >= frases.Length) return;
        if (frases[indice] != null) fuente.PlayOneShot(frases[indice]);
    }
}