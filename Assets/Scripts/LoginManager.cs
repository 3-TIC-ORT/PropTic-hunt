using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class LoginManager : MonoBehaviour
{
    public TMP_InputField campoMail;
    public TMP_InputField campoContrasena;
    public string escenaSiguiente;
    public TMP_Text textoError;
    public GameObject panelError;

    string url = "https://api-prop-tic-hunt.vercel.app/auth/login";

    // Lo que le mandamos a la API
    [System.Serializable]
    public class DatosLogin
    {
        public string mail;
        public string contrasena;
    }

    // Lo que nos contesta la API
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

    // Esta función la llama el botón
    void Start()
    {
    panelError.SetActive(false);
    }
    
    public void IniciarLogin()
    {
        StartCoroutine(HacerLogin());
    }

    IEnumerator HacerLogin()
    {
        textoError.text = "";
        panelError.SetActive(false);
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
            SceneManager.LoadScene(escenaSiguiente);
        }
        else
        {
            Debug.Log("Error: " + pedido.downloadHandler.text);
            panelError.SetActive(true);
            if (pedido.result == UnityWebRequest.Result.ConnectionError)
                textoError.text = "No se pudo conectar. Revisá tu internet.";
            else
                textoError.text = "Mail o contraseña incorrectos.";
        }
    }
}
