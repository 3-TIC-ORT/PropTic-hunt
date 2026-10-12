using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class VerificarTokenManager : MonoBehaviour
{
    public TMP_InputField campoToken;
    public string escenaSiguiente;

    string url = "https://api-prop-tic-hunt.vercel.app/auth/verificar-token";
    bool enviando = false;

    [System.Serializable]
    public class DatosToken
    {
        public string token;
    }

    // Esta función la llama el botón
    public void VerificarToken()
    {
        if (enviando) return;
        StartCoroutine(HacerPedido());
    }

    IEnumerator HacerPedido()
    {
        enviando = true;

        DatosToken datos = new DatosToken();
        datos.token = campoToken.text.Trim();
        string json = JsonUtility.ToJson(datos);

        UnityWebRequest pedido = UnityWebRequest.Post(url, json, "application/json");
        yield return pedido.SendWebRequest();

        enviando = false;

        if (pedido.result == UnityWebRequest.Result.Success)
        {
            // Guardamos el token para usarlo en la pantalla siguiente
            RecuperacionContrasena.Token = datos.token;
            Debug.Log("Token correcto.");
            SceneManager.LoadScene(escenaSiguiente);
        }
        else
        {
            Debug.Log("Error: " + pedido.downloadHandler.text);
        }
    }
}