using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public class MisionDatos
{
    public string descripcion;
    public int recompensa;
    public string progreso_texto;
    public int porcentaje;
    public bool completada;
}

[System.Serializable]
public class ListaMisiones
{
    public MisionDatos[] misiones;
}

public class MisionesManager : MonoBehaviour
{
    private const string URL = "https://api-prop-tic-hunt.vercel.app/estadisticas/misiones";

    // La barra de la posición 0 se llena con la misión 0 de la API, y así
    public ProgresoObjetivo[] barras;

    private void Start()
    {
        StartCoroutine(CargarMisiones());
    }

    private IEnumerator CargarMisiones()
    {
        UnityWebRequest pedido = UnityWebRequest.Get(URL);
        pedido.SetRequestHeader("Authorization", "Bearer " + SesionUsuario.Token);

        yield return pedido.SendWebRequest();

        if (pedido.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Error al pedir las misiones: " + pedido.error + " " + pedido.downloadHandler.text);
            yield break;
        }

        // JsonUtility no lee un array en la raíz del JSON, por eso lo envolvemos
        string json = "{\"misiones\":" + pedido.downloadHandler.text + "}";
        ListaMisiones lista = JsonUtility.FromJson<ListaMisiones>(json);

        if (barras.Length != lista.misiones.Length)
            Debug.LogWarning("La lista Barras tiene " + barras.Length + " barras y la API mandó " + lista.misiones.Length + " misiones");

        for (int i = 0; i < lista.misiones.Length; i++)
        {
            MisionDatos m = lista.misiones[i];
            Debug.Log(i + " | " + m.descripcion + " | " + m.progreso_texto + " | " + m.porcentaje + "%");

            if (i < barras.Length && barras[i] != null)
                barras[i].Actualizar(m.porcentaje, 100);
        }
    }
}