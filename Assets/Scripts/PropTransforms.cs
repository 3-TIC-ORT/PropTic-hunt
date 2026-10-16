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
    [SerializeField] private Transform visualTarget;

    [Header("Props")]
    [SerializeField] private string propTag = "Propable";

    [Header("Cápsula de colisión")]
    [SerializeField] private float maxColliderHeight = 0.42f;

    [SerializeField, Range(0.25f, 1f)]
    private float footprintScale = 0.70f;

    [Header("Props chicos")]
    [SerializeField]
    private Vector3 smallPropMaxDimensions =
        new Vector3(1.25f, 1.25f, 1.25f);

    [SerializeField, Range(0.25f, 1f)]
    private float smallPropHeightScale = 0.65f;

    [SerializeField]
    private float smallPropMinRadius = 0.035f;

    private CharacterController controller;

    private float originalHeight;
    private float originalRadius;
    private float originalStepOffset;
    private float originalBottom;
    private float originalSkinWidth;
    private Vector3 originalCenter;

    private Renderer bodyRenderer;
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
        if (!photonView.IsMine)
            return;

        if (!RoleManager.HasKiller)
            return;

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

        if (controller == null)
        {
            controller = GetComponent<CharacterController>();

            if (controller != null)
            {
                originalHeight = controller.height;
                originalRadius = controller.radius;
                originalStepOffset = controller.stepOffset;
                originalCenter = controller.center;
                originalSkinWidth = controller.skinWidth;

                originalBottom =
                    originalCenter.y - originalHeight * 0.5f;
            }
        }
    }

    // =========================================================
    // AJUSTE DE COLISIONES
    // =========================================================

    private void FitControllerToDisguise(Bounds bounds)
    {
        if (controller == null)
            return;

        Vector3 size = bounds.size;

        bool isSmallProp =
            size.x <= smallPropMaxDimensions.x &&
            size.y <= smallPropMaxDimensions.y &&
            size.z <= smallPropMaxDimensions.z;

        if (!isSmallProp)
        {
            RestoreOriginalController();
            return;
        }

        // Calculamos un radio reducido según el ancho del objeto.
        float smallestHorizontalSide = Mathf.Min(size.x, size.z);

        float radius =
            smallestHorizontalSide * 0.5f * footprintScale;

        // Evitamos que el diámetro impida reducir la altura.
        float maxAllowedRadius =
            Mathf.Max(smallPropMinRadius, maxColliderHeight * 0.45f);

        radius = Mathf.Clamp(
            radius,
            smallPropMinRadius,
            Mathf.Min(originalRadius, maxAllowedRadius)
        );

        // Reducimos la altura de la colisión.
        float height = Mathf.Min(
            size.y * smallPropHeightScale,
            maxColliderHeight,
            originalHeight
        );

        // La altura de una cápsula debe ser al menos su diámetro.
        height = Mathf.Max(height, radius * 2f);
        height = Mathf.Min(height, originalHeight);

        // Mantenemos la base de la cápsula a la altura original
        // para evitar que el jugador quede flotando.
        Vector3 center = new Vector3(
            originalCenter.x,
            originalBottom + height * 0.5f,
            originalCenter.z
        );

        float stepOffset = Mathf.Min(
            originalStepOffset,
            Mathf.Max(0.01f, height * 0.2f)
        );

        float skinWidth = Mathf.Min(
            originalSkinWidth,
            Mathf.Max(0.005f, radius * 0.1f)
        );

        SetControllerDimensions(
            height,
            radius,
            center,
            stepOffset,
            skinWidth
        );

        Debug.Log(
            "[PropTransform] Colisión reducida. " +
            "Objeto: " + currentTargetProp?.name +
            " | Alto: " + height +
            " | Radio: " + radius
        );
    }

    private void SetControllerDimensions(
        float height,
        float radius,
        Vector3 center,
        float stepOffset,
        float skinWidth)
    {
        if (controller == null)
            return;

        radius = Mathf.Max(0.01f, radius);
        height = Mathf.Max(height, radius * 2f);

        bool wasEnabled = controller.enabled;

        // Desactivamos temporalmente el controlador para actualizar
        // su forma de colisión sin conservar las dimensiones anteriores.
        controller.enabled = false;

        controller.height = height;
        controller.radius = radius;
        controller.center = center;
        controller.stepOffset = Mathf.Min(stepOffset, height);
        controller.skinWidth = Mathf.Min(skinWidth, radius);

        controller.enabled = wasEnabled;
    }

    private void RestoreOriginalController()
    {
        SetControllerDimensions(
            originalHeight,
            originalRadius,
            originalCenter,
            originalStepOffset,
            originalSkinWidth
        );
    }

    // =========================================================
    // DETECCIÓN
    // =========================================================

    private void DetectNearbyProp()
    {
        currentTargetProp = null;

        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            detectionRange,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore
        );

        float closestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            GameObject prop = FindPropObject(hit.transform);

            if (prop == null || prop == currentProp)
                continue;

            float distance = Vector3.Distance(
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
    // TRANSFORMACIÓN Y RED
    // =========================================================

    private void TryTransform()
    {
        if (currentTargetProp == null)
            return;

        int propIndex = GetPropIndex(currentTargetProp);

        if (propIndex < 0)
        {
            Debug.LogWarning(
                "[PropTransform] No se pudo encontrar el índice del prop."
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
                "[PropTransform] Índice de prop inválido: " + propIndex
            );

            return;
        }

        ApplyPropVisual(props[propIndex]);
    }

    // =========================================================
    // VISUAL DEL DISFRAZ
    // =========================================================

    private void ApplyPropVisual(GameObject prop)
    {
        InitializeVisual();

        MeshFilter[] sourceFilters =
            prop.GetComponentsInChildren<MeshFilter>();

        Transform newRoot =
            new GameObject("DisguiseRoot").transform;

        newRoot.SetParent(transform, false);

        newRoot.rotation =
            Quaternion.Euler(
                0f,
                prop.transform.eulerAngles.y,
                0f
            );

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
                " no tiene mallas válidas."
            );

            Destroy(newRoot.gameObject);
            return;
        }

        Renderer[] renderers =
            newRoot.GetComponentsInChildren<Renderer>();

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 playerPosition = transform.position;

        newRoot.position += new Vector3(
            playerPosition.x - bounds.center.x,
            playerPosition.y - bounds.min.y,
            playerPosition.z - bounds.center.z
        );

        // Actualizamos la colisión del jugador según el disfraz.
        FitControllerToDisguise(bounds);

        if (disguiseRoot != null)
        {
            disguiseRoot.gameObject.SetActive(false);
            Destroy(disguiseRoot.gameObject);
        }

        disguiseRoot = newRoot;

        if (bodyRenderer != null)
            bodyRenderer.enabled = false;

        currentProp = prop;
        currentTargetProp = null;
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