using System;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

public class PropTransform : MonoBehaviourPun
{
    [Header("Detección")]
    [SerializeField] private float detectionRange = 1.25f;

    [Header("Visual del jugador")]
    [Tooltip("Objeto 'Body' del Player (la cápsula que se oculta al disfrazarse).")]
    [SerializeField] private Transform visualTarget;

    [Header("Props")]
    [SerializeField] private string propTag = "Propable";

    private Renderer bodyRenderer;

    // Contenedor con copias visuales de todas las piezas del prop.
    private Transform disguiseRoot;

    private GameObject currentTargetProp;
    private GameObject currentProp;

    private bool isDisguised;

    private void Awake()
    {
        InitializeVisual();
    }

    private void Start()
    {
        InitializeVisual();
    }

    private void Update()
    {
        // Solamente el dueño procesa input.
        if (!photonView.IsMine)
            return;

        if (!RoleManager.HasKiller)
            return;

        // El Hunter no puede transformarse.
        if (GameManager.IsLocalAssassin())
            return;

        DetectNearbyProp();

        if (Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryTransform();
        }
    }

    private void InitializeVisual()
    {
        if (visualTarget == null)
        {
            Transform body = transform.Find("Body");
            visualTarget = body != null ? body : transform;
        }

        if (bodyRenderer == null)
            bodyRenderer = visualTarget.GetComponent<Renderer>();
    }

    // =========================================================
    // DETECCIÓN
    // =========================================================

    private void DetectNearbyProp()
    {
        currentTargetProp = null;

        Collider[] hits =
            Physics.OverlapSphere(
                transform.position,
                detectionRange,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore
            );

        float closestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            GameObject prop = FindPropObject(hit.transform);

            if (prop == null)
                continue;

            // Si ya somos ese prop, no lo volvemos a seleccionar.
            if (prop == currentProp)
                continue;

            float distance =
                Vector3.Distance(
                    transform.position,
                    hit.ClosestPoint(transform.position)
                );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                currentTargetProp = prop;
            }
        }
    }

    private GameObject FindPropObject(Transform hitTransform)
    {
        Transform current = hitTransform;

        while (current != null)
        {
            if (current.CompareTag(propTag))
                return current.gameObject;

            current = current.parent;
        }

        return null;
    }

    // =========================================================
    // TRANSFORMACIÓN (RED)
    // =========================================================

    private void TryTransform()
    {
        if (currentTargetProp == null)
            return;

        int propIndex = GetPropIndex(currentTargetProp);

        if (propIndex < 0)
        {
            Debug.LogWarning(
                "[PropTransform] No se pudo encontrar " +
                "el índice del prop."
            );

            return;
        }

        photonView.RPC(
            nameof(RPC_ApplyProp),
            RpcTarget.All,
            propIndex
        );
    }

    private GameObject[] GetOrderedProps()
    {
        return GameObject
            .FindGameObjectsWithTag(propTag)
            .OrderBy(prop => prop.name, StringComparer.Ordinal)
            .ThenBy(prop => prop.transform.position.x)
            .ThenBy(prop => prop.transform.position.y)
            .ThenBy(prop => prop.transform.position.z)
            .ToArray();
    }

    private int GetPropIndex(GameObject prop)
    {
        GameObject[] props = GetOrderedProps();

        for (int i = 0; i < props.Length; i++)
        {
            if (props[i] == prop)
                return i;
        }

        return -1;
    }

    [PunRPC]
    private void RPC_ApplyProp(int propIndex)
    {
        GameObject[] props = GetOrderedProps();

        if (propIndex < 0 || propIndex >= props.Length)
        {
            Debug.LogError(
                "[PropTransform] Índice de prop inválido: " +
                propIndex
            );

            return;
        }

        ApplyPropVisual(props[propIndex]);
    }

    // =========================================================
    // VISUAL DEL DISFRAZ
    // =========================================================

    /// <summary>
    /// En vez de fusionar todas las mallas en una sola (CombineMeshes),
    /// copiamos cada pieza del prop (MeshFilter + MeshRenderer) como hija
    /// de un contenedor. Así no dependemos de que las mallas de Blender
    /// tengan "Read/Write" activado, ni del límite de 65.535 vértices,
    /// ni de que coincidan submallas con materiales.
    /// </summary>
    private void ApplyPropVisual(GameObject prop)
    {
        InitializeVisual();

        MeshFilter[] sourceFilters =
            prop.GetComponentsInChildren<MeshFilter>();

        // Armamos el contenedor nuevo primero. Si no hay nada
        // utilizable, dejamos el aspecto actual sin tocar.
        Transform newRoot =
            new GameObject("DisguiseRoot").transform;

        newRoot.SetParent(transform, false);

        // Misma orientación horizontal y escala que el prop original.
        newRoot.rotation =
            Quaternion.Euler(0f, prop.transform.eulerAngles.y, 0f);

        newRoot.localScale = prop.transform.lossyScale;

        int pieces = 0;

        foreach (MeshFilter mf in sourceFilters)
        {
            if (mf.sharedMesh == null)
                continue;

            MeshRenderer sourceRenderer =
                mf.GetComponent<MeshRenderer>();

            if (sourceRenderer == null || !sourceRenderer.enabled)
                continue;

            // Pose de la pieza relativa a la raíz del prop.
            Matrix4x4 relative =
                prop.transform.worldToLocalMatrix *
                mf.transform.localToWorldMatrix;

            GameObject piece = new GameObject(mf.name);

            piece.transform.SetParent(newRoot, false);
            piece.transform.localPosition = relative.GetPosition();
            piece.transform.localRotation = relative.rotation;
            piece.transform.localScale = relative.lossyScale;

            piece.AddComponent<MeshFilter>().sharedMesh =
                mf.sharedMesh;

            piece.AddComponent<MeshRenderer>().sharedMaterials =
                sourceRenderer.sharedMaterials;

            pieces++;
        }

        if (pieces == 0)
        {
            Debug.LogWarning(
                "[PropTransform] El objeto " + prop.name +
                " no tiene MeshFilter/MeshRenderer activos " +
                "en sus hijos."
            );

            Destroy(newRoot.gameObject);
            return;
        }

        // Centramos el disfraz sobre el jugador y apoyamos su base
        // en el suelo. Se calcula con los bounds reales de los
        // renderers, así funciona aunque la malla no sea legible
        // y aunque el prop esté rotado.
        Renderer[] renderers =
            newRoot.GetComponentsInChildren<Renderer>();

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 playerPosition = transform.position;

        newRoot.position +=
            new Vector3(
                playerPosition.x - bounds.center.x,
                playerPosition.y - bounds.min.y,
                playerPosition.z - bounds.center.z
            );

        // Recién ahora reemplazamos el disfraz anterior.
        if (disguiseRoot != null)
        {
            disguiseRoot.gameObject.SetActive(false);
            Destroy(disguiseRoot.gameObject);
        }

        disguiseRoot = newRoot;

        // Ocultamos el cuerpo original (la cápsula).
        if (bodyRenderer != null)
            bodyRenderer.enabled = false;

        currentProp = prop;
        isDisguised = true;

        Debug.Log(
            "[PropTransform] " +
            PhotonNetwork.NickName +
            " se transformó en " +
            prop.name +
            " (" + pieces + " piezas)"
        );
    }

    public bool IsDisguised()
    {
        return isDisguised;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            detectionRange
        );
    }
}