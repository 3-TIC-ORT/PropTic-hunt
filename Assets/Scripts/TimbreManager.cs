using UnityEngine;

public class TimbreManager : MonoBehaviour
{
    // Otros scripts la leen así: TimbreManager.FaseActiva
    public static bool FaseActiva { get; private set; }

    public AudioClip sonido;

    [Tooltip("Cuántos segundos antes del final suena el timbre (y empieza la fase)")]
    public float duracionFase = 60f;

    private AudioSource fuente;
    private bool yaSono;

    private void Awake()
    {
        FaseActiva = false;

        fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 0f; // sonido 2D: todos lo escuchan igual
    }

    private void Update()
    {
        if (yaSono || FinDePartida.Terminada || !RelojPartida.PartidaEnCurso) return;

        if (RelojPartida.SegundosRestantes <= duracionFase)
        {
            yaSono = true;
            FaseActiva = true;

            if (sonido != null) fuente.PlayOneShot(sonido);
            else Debug.LogWarning("[Timbre] Falta asignar el sonido");

            Debug.Log("[Timbre] Empezó la fase del timbre");
        }
    }

    private void OnDestroy()
    {
        FaseActiva = false;
    }
}