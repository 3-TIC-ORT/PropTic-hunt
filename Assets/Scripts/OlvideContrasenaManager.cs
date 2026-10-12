using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class OlvideContrasenaManager : MonoBehaviour
{
    public TMP_InputField campoMail;
    public string escenaSiguiente;

    string url = "https://api-prop-tic-hunt.vercel.app/auth/olvide-contrasena";
    bool enviando = false;

    [System.Serializable]
    public class DatosMail
    {
        public string mail;
    }

    // Esta función la llama el botón
    public void EnviarMail()
    {
        if (enviando) return;
        StartCoroutine(HacerPedido());
    }

    IEnumerator HacerPedido()
    {
        enviando = true;

        DatosMail datos = new DatosMail();
        datos.mail = campoMail.text.Trim();
        string json = JsonUtility.ToJson(datos);

        UnityWebRequest pedido = UnityWebRequest.Post(url, json, "application/json");
        yield return pedido.SendWebRequest();

        enviando = false;

        if (pedido.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Mail enviado. Revisá tu bandeja de entrada.");
            SceneManager.LoadScene(escenaSiguiente);
        }
        else
        {
            Debug.Log("Error: " + pedido.downloadHandler.text);
        }
    }
}