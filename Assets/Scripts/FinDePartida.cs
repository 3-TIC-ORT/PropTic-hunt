using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FinDePartida : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public enum Motivo : byte { TodosCapturados = 0, SinVida = 1, TiempoAgotado = 2, CazadorAbandono = 3 }

    private const byte CODIGO_EVENTO = 77;

    public static FinDePartida Instancia { get; private set; }
    public static bool Terminada { get; private set; }
    public static string ResultadoLocal { get; private set; } = "";

    public TMP_Text textoResultado;
    public GameObject panelResultado;
    public Image fondoResultado;
    public Color colorGane = new Color(0.12f, 0.55f, 0.25f, 0.92f);
    public Color colorPerdi = new Color(0.7f, 0.12f, 0.12f, 0.92f);
    public float duracionPartida = 300f;

    private readonly HashSet<int> capturados = new HashSet<int>();
    private float tiempoInicio;
    private bool relojActivo;

    private void Awake()
    {
        Instancia = this;
        Terminada = false;
        ResultadoLocal = "";
        if (panelResultado != null) panelResultado.SetActive(false);
    }

    public override void OnEnable()
    {
        base.OnEnable();
        VidaCazador.AlQuedarseSinVida += AlQuedarseSinVida;
    }

    public override void OnDisable()
    {
        base.OnDisable();
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
            if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient) Enviar(Motivo.TiempoAgotado);
        }
    }

    // Cuenta los Props que siguen en la sala y no fueron capturados
    private int PropsVivos(int ignorarActor)
    {
        int vivos = 0;
        foreach (Player jugador in PhotonNetwork.PlayerList)
        {
            if (jugador.ActorNumber == ignorarActor) continue;
            if (jugador.ActorNumber == GameManager.ArmedActorNumber) continue;
            if (capturados.Contains(jugador.ActorNumber)) continue;
            vivos++;
        }
        return vivos;
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
        if (actorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
            EnviadorPartida.RegistrarCapturaLocal();

        if (PropsVivos(-1) == 0 && PhotonNetwork.IsMasterClient)
            Enviar(Motivo.TodosCapturados);
    }

    // Photon avisa a los que quedan cuando alguien se va de la sala
    public override void OnPlayerLeftRoom(Player otroJugador)
    {
        if (Terminada) return;

        // Se fue el Cazador: ganan los Props que siguen vivos
        if (otroJugador.ActorNumber == GameManager.ArmedActorNumber)
        {
            Terminar(Motivo.CazadorAbandono);
            return;
        }

        // Se fue un Prop: si no queda ninguno vivo, gana el Cazador
        if (PropsVivos(otroJugador.ActorNumber) == 0)
            Terminar(Motivo.TodosCapturados);
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
            textoResultado.text = gane ? "¡GANASTE!" : "PERDISTE";

        if (fondoResultado != null)
            fondoResultado.color = gane ? colorGane : colorPerdi;

        if (panelResultado != null)
            panelResultado.SetActive(true);

        // Libera el mouse para poder apretar el botón
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        EnviadorPartida.EnviarFinal();
    }

    [ContextMenu("Probar: todos capturados")]
    private void ProbarCapturados() { Terminar(Motivo.TodosCapturados); }

    [ContextMenu("Probar: Cazador sin vida")]
    private void ProbarSinVida() { Terminar(Motivo.SinVida); }

    [ContextMenu("Probar: se acabó el tiempo")]
    private void ProbarTiempo() { Terminar(Motivo.TiempoAgotado); }

    [ContextMenu("Probar: el Cazador abandonó")]
    private void ProbarAbandono() { Terminar(Motivo.CazadorAbandono); }

    [ContextMenu("Iniciar partida (prueba)")]
    private void ProbarInicio() { IniciarPartida(); }
}