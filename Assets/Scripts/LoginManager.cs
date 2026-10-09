using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class LoginManager : MonoBehaviour
{
    public TMP_InputField campoMail;
    public TMP_InputField campoContrasena;

    string url = "https://api-prop-tic-hunt.vercel.app/login";

    [System.Serializable]
    public class DatosLogin
    {
        public string mail;
        public string contrasena;
    }

    [System.Serializable]
    public class Usuario
    {
        public string nombre;
        public string mail;
        public int puntos_totales;
    }

    [System.Serializable]
    public class RespuestaLogin
    {
        public string token;
        public Usuario usuario;
    }

    public void IniciarLogin()
    {
        StartCoroutine(HacerLogin());
    }

    IEnumerator HacerLogin()
    {
        DatosLogin datos = new DatosLogin();
        datos.mail = campoMail.text;
        datos.contrasena = campoContrasena.text;
        string json = JsonUtility.ToJson(datos);

        UnityWebRequest pedido = UnityWebRequest.Post(url, json, "application/json");
        yield return pedido.SendWebRequest();

        if (pedido.result == UnityWebRequest.Result.Success)
        {
            RespuestaLogin respuesta = JsonUtility.FromJson<RespuestaLogin>(pedido.downloadHandler.text);

            SesionUsuario.Token = respuesta.token;
            SesionUsuario.Nombre = respuesta.usuario.nombre;
            SesionUsuario.Mail = respuesta.usuario.mail;
            SesionUsuario.PuntosTotales = respuesta.usuario.puntos_totales;

            Debug.Log("Login correcto. Bienvenido " + SesionUsuario.Nombre);
        }
        else
        {
            Debug.Log("Error: " + pedido.downloadHandler.text);
        }
    }
}