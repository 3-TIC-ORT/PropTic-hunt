using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class RegistroManager : MonoBehaviour
{
    public TMP_InputField campoMail;
    public TMP_InputField campoNombre;
    public TMP_InputField campoContrasena;
    public string escenaSiguiente;

    string urlRegistro = "https://api-prop-tic-hunt.vercel.app/auth/registro";
    string urlLogin = "https://api-prop-tic-hunt.vercel.app/auth/login";

    [System.Serializable]
    public class DatosRegistro
    {
        public string nombre;
        public string mail;
        public string contrasena;
    }

    public void IniciarRegistro()
    {
        StartCoroutine(HacerRegistro());
    }

    IEnumerator HacerRegistro()
    {
        DatosRegistro datos = new DatosRegistro();
        datos.nombre = campoNombre.text;
        datos.mail = campoMail.text;
        datos.contrasena = campoContrasena.text;
        string json = JsonUtility.ToJson(datos);

        UnityWebRequest pedido = UnityWebRequest.Post(urlRegistro, json, "application/json");
        yield return pedido.SendWebRequest();

        if (pedido.result != UnityWebRequest.Result.Success)
        {
            Debug.Log("Error: " + pedido.downloadHandler.text);
            yield break;
        }

        Debug.Log("Registro correcto. Iniciando sesion...");

        // Login automatico con los mismos datos
        LoginManager.DatosLogin login = new LoginManager.DatosLogin();
        login.mail = datos.mail;
        login.contrasena = datos.contrasena;
        string jsonLogin = JsonUtility.ToJson(login);

        UnityWebRequest pedidoLogin = UnityWebRequest.Post(urlLogin, jsonLogin, "application/json");
        yield return pedidoLogin.SendWebRequest();

        if (pedidoLogin.result == UnityWebRequest.Result.Success)
        {
            LoginManager.RespuestaLogin respuesta = JsonUtility.FromJson<LoginManager.RespuestaLogin>(pedidoLogin.downloadHandler.text);

            SesionUsuario.Token = respuesta.token;
            SesionUsuario.Nombre = respuesta.usuario.nombre;
            SesionUsuario.Mail = respuesta.usuario.mail;
            SesionUsuario.PuntosTotales = respuesta.usuario.puntos_totales;

            SceneManager.LoadScene(escenaSiguiente);
        }
        else
        {
            Debug.Log("Error al iniciar sesion: " + pedidoLogin.downloadHandler.text);
        }
    }
}