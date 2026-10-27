using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public class DisparoDatos
{
    public bool impacto_exitoso;
    public int vida_restante;
    public bool fase_timbre_sonando;
    public int segundo_partida;
}

[System.Serializable]
public class HabilidadUsoDatos
{
    public string nombre_habilidad;
    public int momento_uso;
}

[System.Serializable]
public class AbandonoDatos
{
    public int tiempo_transcurrido;
    public string rol_actual;
    public int vida_cazador_actual;
}

[System.Serializable]
public class PartidaDatos
{
    public string codigo_partida;
    public int duracion;
    public string rol;
    public string resultado;
    public int disparos_acertados;
    public int disparos_totales;
    public int silbidos;
    public int tiempo_sobrevivido;
    public List<DisparoDatos> disparos = new List<DisparoDatos>();
    public List<HabilidadUsoDatos> habilidades = new List<HabilidadUsoDatos>();
    public List<AbandonoDatos> abandonos = new List<AbandonoDatos>();
}

public class EnviadorPartida : MonoBehaviour
{
    private const string URL = "https://api-prop-tic-hunt.vercel.app/juego/partidas";
    private const string NOMBRE_HABILIDAD = "Mano Firme";

    public static EnviadorPartida Instancia { get; private set; }

    [Tooltip("Destildado: modo ensayo, la API valida y responde pero NO guarda nada. Tildar solo cuando se quiera que cuente.")]
    public bool guardarDeVerdad = false;

    private readonly List<DisparoDatos> disparos = new List<DisparoDatos>();
    private readonly List<HabilidadUsoDatos> habilidades = new List<HabilidadUsoDatos>();

    private float inicioPartida;
    private bool inicioRegistrado;
    private float segundoCaptura = -1f;
    private bool yaEnviada;

    private void Awake()
    {
        Instancia = this;
    }

    private void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    // Anota el momento en que empieza la partida de verdad (cuando termina la espera)
    private void Update()
    {
        if (!inicioRegistrado && RelojPartida.PartidaEnCurso)
        {
            inicioRegistrado = true;
            inicioPartida = Time.time;
        }
    }

    private int SegundosDePartida()
    {
        return inicioRegistrado ? Mathf.Max(0, Mathf.RoundToInt(Time.time - inicioPartida)) : 0;
    }

    // ---- Lo que llaman los otros scripts ----

    public static void RegistrarDisparo(bool acerto, int vidaRestante)
    {
        if (Instancia == null) return;

        Instancia.disparos.Add(new DisparoDatos
        {
            impacto_exitoso = acerto,
            vida_restante = Mathf.Max(0, vidaRestante),
            fase_timbre_sonando = TimbreManager.FaseActiva,
            segundo_partida = Instancia.SegundosDePartida()
        });
    }

    public static void RegistrarHabilidad()
    {
        if (Instancia == null) return;

        Instancia.habilidades.Add(new HabilidadUsoDatos
        {
            nombre_habilidad = NOMBRE_HABILIDAD,
            momento_uso = Instancia.SegundosDePartida()
        });
    }

    public static void RegistrarCapturaLocal()
    {
        if (Instancia != null && Instancia.segundoCaptura < 0f)
            Instancia.segundoCaptura = Instancia.SegundosDePartida();
    }

    public static void EnviarFinal()
    {
        if (Instancia != null) Instancia.Enviar(FinDePartida.ResultadoLocal, false);
    }

    public static void EnviarAbandono()
    {
        if (Instancia != null && !FinDePartida.Terminada) Instancia.Enviar("Derrota", true);
    }

    // ---- Armado y envío ----

    private void Enviar(string resultado, bool abandono)
    {
        if (yaEnviada) return;

        if (!inicioRegistrado)
        {
            Debug.Log("[Partida] La partida todavía no empezó (sigue la espera): no se envía nada");
            return;
        }

        if (string.IsNullOrEmpty(SesionUsuario.Token))
        {
            Debug.LogWarning("[Partida] No hay sesión iniciada (falta el token): no se envía nada");
            return;
        }

        yaEnviada = true;

        bool esCazador = GameManager.IsLocalAssassin();
        int duracion = Mathf.Max(1, SegundosDePartida());

        PartidaDatos datos = new PartidaDatos();
        datos.codigo_partida = PhotonNetwork.InRoom
            ? PhotonNetwork.CurrentRoom.Name
            : "sin-sala-" + System.DateTime.Now.Ticks;
        datos.duracion = duracion;
        datos.rol = esCazador ? "Cazador" : "Prop";
        datos.resultado = string.IsNullOrEmpty(resultado) ? "Derrota" : resultado;
        datos.habilidades = new List<HabilidadUsoDatos>(habilidades);

        if (esCazador)
        {
            datos.disparos = new List<DisparoDatos>(disparos);
            datos.disparos_totales = disparos.Count;
            foreach (DisparoDatos d in disparos)
                if (d.impacto_exitoso) datos.disparos_acertados++;
        }
        else
        {
            datos.silbidos = SilbidoProp.Local != null ? SilbidoProp.Local.Cantidad : 0;
            datos.tiempo_sobrevivido = segundoCaptura >= 0f
                ? Mathf.Min(duracion, Mathf.RoundToInt(segundoCaptura))
                : duracion;
        }

        if (abandono)
        {
            datos.abandonos.Add(new AbandonoDatos
            {
                tiempo_transcurrido = duracion,
                rol_actual = datos.rol,
                vida_cazador_actual = esCazador && VidaCazador.Local != null ? VidaCazador.Local.VidaActual : 0
            });
        }

        bool real = guardarDeVerdad && PhotonNetwork.InRoom;
        if (guardarDeVerdad && !PhotonNetwork.InRoom)
            Debug.LogWarning("[Partida] No estás en una sala: se manda en modo ensayo aunque Guardar De Verdad esté tildado");

        string url = real ? URL : URL + "?prueba=true";
        string json = JsonUtility.ToJson(datos);

        Debug.Log("[Partida] Enviando (" + (real ? "DE VERDAD" : "ENSAYO, no se guarda") + "): " + json);
        EjecutorCorrutinas.Ejecutar(Post(url, json, SesionUsuario.Token));
    }

    private static IEnumerator Post(string url, string json, string token)
    {
        UnityWebRequest pedido = UnityWebRequest.Post(url, json, "application/json");
        pedido.SetRequestHeader("Authorization", "Bearer " + token);

        yield return pedido.SendWebRequest();

        if (pedido.result != UnityWebRequest.Result.Success)
            Debug.LogError("[Partida] La API respondió con error: " + pedido.error + " " + pedido.downloadHandler.text);
        else
            Debug.Log("[Partida] Respuesta de la API: " + pedido.downloadHandler.text);
    }
}