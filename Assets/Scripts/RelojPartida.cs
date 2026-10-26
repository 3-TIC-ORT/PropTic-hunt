using ExitGames.Client.Photon;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class RelojPartida : MonoBehaviour
{
    // Otros scripts lo consultan: durante la espera no se dispara, ni se usa la habilidad, ni se silba
    public static bool EnEspera { get; private set; }

    private const string CLAVE_INICIO = "inicioEspera";

    public TMP_Text textoReloj;
    public float duracionEspera = 30f;
    public float retrasoInicio = 3f;

    [Tooltip("Se ejecuta en todos los jugadores cuando termina la espera (por ejemplo, para abrir la puerta)")]
    public UnityEvent alTerminarLaEspera;

    private double inicio = -1;
    private bool esperaTerminada;

    private static double Ahora => PhotonNetwork.InRoom ? PhotonNetwork.Time : Time.timeAsDouble;

    private void Awake()
    {
        EnEspera = true;
    }

    private void OnDestroy()
    {
        EnEspera = false;
    }

    private void Start()
    {
        if (!PhotonNetwork.InRoom)
        {
            inicio = Ahora + retrasoInicio; // sin sala (pruebas): cuenta local
        }
        else if (PhotonNetwork.IsMasterClient &&
                 !PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(CLAVE_INICIO))
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(
                new Hashtable { { CLAVE_INICIO, PhotonNetwork.Time + retrasoInicio } });
        }
    }

    private void Update()
    {
        if (FinDePartida.Terminada)
        {
            EnEspera = false;
            MostrarTexto("");
            return;
        }

        if (inicio < 0) LeerInicio();
        if (inicio < 0)
        {
            MostrarTexto("Preparando...");
            return;
        }

        double transcurrido = Ahora - inicio;
        float duracionPartida = FinDePartida.Instancia != null ? FinDePartida.Instancia.duracionPartida : 300f;

        if (transcurrido < 0)
        {
            MostrarTexto("Preparando...");
        }
        else if (transcurrido < duracionEspera)
        {
            MostrarTexto("El Cazador sale en " + Formato(duracionEspera - transcurrido));
        }
        else
        {
            if (!esperaTerminada) TerminarEspera();
            double restante = duracionEspera + duracionPartida - transcurrido;
            MostrarTexto("Tiempo restante " + Formato(restante));
        }
    }

    private void LeerInicio()
    {
        if (!PhotonNetwork.InRoom) return;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CLAVE_INICIO, out object valor))
            inicio = (double)valor;
    }

    private void TerminarEspera()
    {
        esperaTerminada = true;
        EnEspera = false;

        if (FinDePartida.Instancia != null) FinDePartida.Instancia.IniciarPartida();
        alTerminarLaEspera.Invoke();
    }

    private static string Formato(double segundos)
    {
        int total = Mathf.Max(0, Mathf.CeilToInt((float)segundos));
        return (total / 60) + ":" + (total % 60).ToString("00");
    }

    private void MostrarTexto(string texto)
    {
        if (textoReloj != null) textoReloj.text = texto;
    }
}