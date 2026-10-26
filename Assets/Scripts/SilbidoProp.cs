using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

public class SilbidoProp : MonoBehaviourPun
{
    public static SilbidoProp Local { get; private set; }

    public Key tecla = Key.F;
    public float tiempoEspera = 10f;
    public AudioClip sonido;

    [Tooltip("Solo para pruebas: deja silbar también al Cazador. Dejar desactivado en el juego real.")]
    public bool permitirAlCazadorEnPruebas = false;

    // Cuántas veces silbó este jugador en la partida (después se manda a la API)
    public int Cantidad { get; private set; }
    public float SegundosEspera => Mathf.Max(0f, proximoSilbido - Time.time);

    private AudioSource fuente;
    private float proximoSilbido;

    private void Awake()
    {
        fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 1f; // sonido 3D: se oye más fuerte cuanto más cerca
        fuente.rolloffMode = AudioRolloffMode.Linear;
        fuente.minDistance = 3f;
        fuente.maxDistance = 40f;
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
            proximoSilbido = Time.time + tiempoEspera;
            Cantidad++;
            Debug.Log("[Silbido] Silbaste (" + Cantidad + ")");
            photonView.RPC(nameof(RPC_Silbar), RpcTarget.All);
        }
    }

    [PunRPC]
    private void RPC_Silbar()
    {
        if (sonido != null) fuente.PlayOneShot(sonido);
    }
}