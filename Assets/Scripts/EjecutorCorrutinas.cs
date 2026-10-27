using System.Collections;
using UnityEngine;

// Corre una corrutina en un objeto que no se destruye al cambiar de escena
public class EjecutorCorrutinas : MonoBehaviour
{
    public static void Ejecutar(IEnumerator rutina)
    {
        GameObject objeto = new GameObject("EjecutorCorrutinas");
        DontDestroyOnLoad(objeto);
        EjecutorCorrutinas ejecutor = objeto.AddComponent<EjecutorCorrutinas>();
        ejecutor.StartCoroutine(ejecutor.Correr(rutina));
    }

    private IEnumerator Correr(IEnumerator rutina)
    {
        yield return StartCoroutine(rutina);
        Destroy(gameObject);
    }
}