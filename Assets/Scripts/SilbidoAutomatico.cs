using Photon.Pun;
using UnityEngine;

public class SilbidoAutomatico : MonoBehaviourPun
{
    public AudioClip sonido;
    public float cadaSegundos = 20f;

    [Header("Tono segun la distancia")]
    public float pitchCerca = 1f;
    public float pitchLejos = 0.7f;
    public float distanciaMinima = 3f;
    public float distanciaMaxima = 40f;

    private AudioSource fuente;
    private int ultimaMarca = -1;
    public bool EstaSonando => fuente != null && fuente.isPlaying;

    private void Awake()
    {
        fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 1f; // 3D: mas fuerte cuanto mas cerca
        fuente.rolloffMode = AudioRolloffMode.Linear;
        fuente.minDistance = distanciaMinima;
        fuente.maxDistance = distanciaMaxima;
    }

    private void Update()
    {
        if (!PhotonNetwork.InRoom || !RoleManager.HasKiller) return;

        // El Cazador no silba
        if (photonView.Owner != null &&
            photonView.Owner.ActorNumber == RoleManager.GetKillerActorNumber()) return;

        // Durante la espera o con la partida terminada no silba
        if (FinDePartida.Terminada || RelojPartida.EnEspera)
        {
            ultimaMarca = -1;
            return;
        }

        // Reloj de Photon: es el mismo en todas las compus
        int marca = Mathf.FloorToInt((float)(PhotonNetwork.Time / Mathf.Max(1f, cadaSegundos)));

        // La primera vez solo anotamos la marca, asi no silba de golpe al arrancar
        if (ultimaMarca < 0)
        {
            ultimaMarca = marca;
            return;
        }

        if (marca > ultimaMarca)
        {
            ultimaMarca = marca;
            Silbar();
        }
    }

    private void Silbar()
    {
        if (sonido == null) return;

        float t = 0f;
        Camera cam = Camera.main;
        if (cam != null)
        {
            float distancia = Vector3.Distance(transform.position, cam.transform.position);
            t = Mathf.InverseLerp(distanciaMinima, distanciaMaxima, distancia);
        }

        // Mas lejos = mas grave
        fuente.pitch = Mathf.Lerp(pitchCerca, pitchLejos, t);
        fuente.PlayOneShot(sonido);
    }
}