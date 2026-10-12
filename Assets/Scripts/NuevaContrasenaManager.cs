using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class NuevaContrasenaManager : MonoBehaviour
{
    public TMP_InputField campoContrasena;
    public string escenaSiguiente;

    string url = "https://api-prop-tic-hunt.vercel.app/auth/restablecer-contrasena";
    bool enviando = false;

    [System.Serializable]
    public class DatosNuevaContrasena
    {
        public string token;
        public string nuevaContrasena;
    }

    // Esta función la llama el botón
    public void CambiarContrasena()
    {
        if (enviando) return;
        StartCoroutine(HacerPedido());
    }

    IEnumerator HacerPedido()
    {
        enviando = true;

        DatosNuevaContrasena datos = new DatosNuevaContrasena();
        datos.token = RecuperacionContrasena.Token;
        datos.nuevaContrasena = campoContrasena.text;
        string json = JsonUtility.ToJson(datos);

        UnityWebRequest pedido = UnityWebRequest.Post(url, json, "application/json");
        yield return pedido.SendWebRequest();

        enviando = false;

        if (pedido.result == UnityWebRequest.Result.Success)
        {
            RecuperacionContrasena.Token = null;
            Debug.Log("Contraseña cambiada. Ya podés iniciar sesión.");
            SceneManager.LoadScene(escenaSiguiente);
        }
        else
        {
            Debug.Log("Error: " + pedido.downloadHandler.text);
        }
    }
}