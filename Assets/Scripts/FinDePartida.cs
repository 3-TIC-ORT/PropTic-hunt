using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

public class FinDePartida : MonoBehaviour, IOnEventCallback
{
    public enum Motivo : byte { TodosCapturados = 0, SinVida = 1, TiempoAgotado = 2 }

    private const byte CODIGO_EVENTO = 77;

    public static FinDePartida Instancia { get; private set; }
    public static bool Terminada { get; private set; }
    public static string ResultadoLocal { get; private set; } = "";

    public TMP_Text textoResultado;
    public float duracionPartida = 300f;

    private readonly HashSet<int> capturados = new HashSet<int>();
    private float tiempoInicio;
    private bool relojActivo;

    private void Awake()
    {
        Instancia = this;
        Terminada = false;
        ResultadoLocal = "";
        if (textoResultado != null) textoResultado.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
        VidaCazador.AlQuedarseSinVida += AlQuedarseSinVida;
    }

    private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
        VidaCazador.AlQuedarseSinVida -= AlQuedarseSinVida;
    }

    private void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    // Hay que llamarlo cuando termina la espera del Cazador y empieza la partida de verdad
    public void IniciarPartida()
    {
        tiempoInicio = Time.time;
        relojActivo = true;
    }

    private void Update()
    {
        if (!relojActivo || Terminada) return;

        if (Time.time - tiempoInicio >= duracionPartida)
        {
            relojActivo = false;
            if (PhotonNetwork.IsMasterClient) Enviar(Motivo.TiempoAgotado);
        }
    }

    // La llama PlayerHealth cuando un Prop es capturado (corre en todos los jugadores)
    public static void RegistrarCaptura(int actorNumber)
    {
        if (Instancia != null) Instancia.Capturar(actorNumber);
    }

    private void Capturar(int actorNumber)
    {
        if (Terminada || PhotonNetwork.CurrentRoom == null) return;

        capturados.Add(actorNumber);
        int props = PhotonNetwork.CurrentRoom.PlayerCount - 1;

        if (props > 0 && capturados.Count >= props && PhotonNetwork.IsMasterClient)
            Enviar(Motivo.TodosCapturados);
    }

    private void AlQuedarseSinVida()
    {
        Enviar(Motivo.SinVida);
    }

    private void Enviar(Motivo motivo)
    {
        if (!PhotonNetwork.InRoom)
        {
            Terminar(motivo); // sin sala (pruebas): se resuelve solo en esta computadora
            return;
        }

        PhotonNetwork.RaiseEvent(
            CODIGO_EVENTO,
            (byte)motivo,
            new RaiseEventOptions { Receivers = ReceiverGroup.All },
            SendOptions.SendReliable);
    }

    public void OnEvent(EventData evento)
    {
        if (evento.Code != CODIGO_EVENTO) return;
        Terminar((Motivo)(byte)evento.CustomData);
    }

    private void Terminar(Motivo motivo)
    {
        if (Terminada) return;
        Terminada = true;
        relojActivo = false;

        bool soyCazador = GameManager.IsLocalAssassin();
        bool soyCapturado = capturados.Contains(PhotonNetwork.LocalPlayer.ActorNumber);

        bool gane = motivo == Motivo.TodosCapturados
            ? soyCazador
            : !soyCazador && !soyCapturado;

        ResultadoLocal = gane ? "Victoria" : "Derrota";
        Debug.Log("[Fin] Motivo: " + motivo + " | Resultado: " + ResultadoLocal);

        if (textoResultado != null)
        {
            textoResultado.text = gane ? "¡GANASTE!" : "PERDISTE";
            textoResultado.gameObject.SetActive(true);
        }
    }

    [ContextMenu("Probar: todos capturados")]
    private void ProbarCapturados() { Terminar(Motivo.TodosCapturados); }

    [ContextMenu("Probar: Cazador sin vida")]
    private void ProbarSinVida() { Terminar(Motivo.SinVida); }

    [ContextMenu("Probar: se acabó el tiempo")]
    private void ProbarTiempo() { Terminar(Motivo.TiempoAgotado); }

    [ContextMenu("Iniciar partida (prueba)")]
    private void ProbarInicio() { IniciarPartida(); }
}