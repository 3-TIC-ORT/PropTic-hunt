using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public class PuntosDatos
{
    public int puntos_totales;
}

public class PuntosHUD : MonoBehaviour
{
    private const string URL = "https://api-prop-tic-hunt.vercel.app/estadisticas/puntos";

    public TMP_Text texto;

    private void Start()
    {
        StartCoroutine(PedirPuntos());
    }

    private IEnumerator PedirPuntos()
    {
        if (string.IsNullOrEmpty(SesionUsuario.Token)) yield break;

        UnityWebRequest pedido = UnityWebRequest.Get(URL);
        pedido.SetRequestHeader("Authorization", "Bearer " + SesionUsuario.Token);
        yield return pedido.SendWebRequest();

        if (pedido.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Error al pedir los puntos: " + pedido.error + " " + pedido.downloadHandler.text);
            yield break;
        }

        PuntosDatos datos = JsonUtility.FromJson<PuntosDatos>(pedido.downloadHandler.text);
        texto.text = datos.puntos_totales + " PTS";
    }
}