using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

public class SilbidoProp : MonoBehaviourPun
{
    public static SilbidoProp Local { get; private set; }

    public Key tecla = Key.F;
    public float tiempoEspera = 10f;

    [Tooltip("Frases grabadas. La F reproduce una al azar.")]
    public AudioClip[] frases;

    [Tooltip("Solo para pruebas: deja usar la F también al Cazador. Dejar desactivado en el juego real.")]
    public bool permitirAlCazadorEnPruebas = false;

    // Cuántas veces usó la F en la partida (después se manda a la API)
    public int Cantidad { get; private set; }
    public float SegundosEspera => Mathf.Max(0f, proximoSilbido - Time.time);

    private AudioSource fuente;
    private SilbidoAutomatico silbidoAuto;
    private float proximoSilbido;

    // Bolsa de frases: salen todas antes de repetir
    private List<int> pendientes = new List<int>();
    private int ultimoIndice = -1;

    private void Awake()
    {
        fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 1f; // sonido 3D: se oye más fuerte cuanto más cerca
        fuente.rolloffMode = AudioRolloffMode.Linear;
        fuente.minDistance = 3f;
        fuente.maxDistance = 40f;

        silbidoAuto = GetComponent<SilbidoAutomatico>();
    }

    private void Start()
    {
        if (photonView.IsMine) Local = this;
    }

    private void OnDestroy()
    {
        if (Local == this) Local = null;
    }

    private void Update()
    {
        if (!photonView.IsMine || !RoleManager.HasKiller) return;
        if (GameManager.IsLocalAssassin() && !permitirAlCazadorEnPruebas) return;
        if (ConfirmarSalida.MenuAbierto || FinDePartida.Terminada || RelojPartida.EnEspera) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current[tecla].wasPressedThisFrame && Time.time >= proximoSilbido)
        {
            // No se puede usar mientras suena el silbido automático
            if (silbidoAuto != null && silbidoAuto.EstaSonando) return;

            // Sin frases no hace nada
            if (frases == null || frases.Length == 0)
            {
                Debug.LogWarning("[Frases] La lista Frases está vacía.");
                return;
            }

            proximoSilbido = Time.time + tiempoEspera;
            Cantidad++;
            Debug.Log("[Frases] Usaste la F (" + Cantidad + ")");

            int indice = ElegirFrase();
            photonView.RPC(nameof(RPC_DecirFrase), RpcTarget.All, indice);
        }
    }

    // Solo la compu dueña elige la frase y manda el número a todos
    private int ElegirFrase()
    {
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
    private void RPC_DecirFrase(int indice)
    {
        if (frases == null || indice < 0 || indice >= frases.Length) return;
        if (frases[indice] != null) fuente.PlayOneShot(frases[indice]);
    }
}