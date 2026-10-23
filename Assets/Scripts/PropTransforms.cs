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

    [Header("Rotación del prop (mantener R)")]
    [Tooltip("Grados por segundo que gira el prop mientras se mantiene R.")]
    [SerializeField] private float rotationSpeed = 120f;

    [Tooltip("Cada cuántos segundos se envía el ángulo a los demás jugadores.")]
    [SerializeField] private float rotationSyncInterval = 0.05f;

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

    // Rotación del disfraz con la tecla R.
    // Punto del root (en coordenadas locales) sobre el que gira el prop.
    private Vector3 disguisePivotLocal;
    private float appliedYaw;
    private float targetYaw;
    private float lastSentYaw;
    private float rotationSyncTimer;
    private bool wasRotating;

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
        // Todos los clientes aplican la rotación del disfraz
        // (los remotos la reciben por red).
        StepDisguiseRotation();

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

        HandleRotateInput();
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
    // ROTACIÓN DEL DISFRAZ (TECLA R)
    // =========================================================

    // Solo el dueño: lee la tecla y acumula el ángulo objetivo.
    private void HandleRotateInput()
    {
        if (!isDisguised || disguiseRoot == null)
            return;

        if (Keyboard.current == null)
            return;

        bool holding =
            Keyboard.current.rKey.isPressed &&
            !ConfirmarSalida.MenuAbierto;

        if (holding)
        {
            targetYaw = Mathf.Repeat(
                targetYaw + rotationSpeed * Time.deltaTime,
                360f
            );

            rotationSyncTimer += Time.deltaTime;

            if (rotationSyncTimer >= rotationSyncInterval)
            {
                rotationSyncTimer = 0f;
                SendYawToOthers();
            }

            wasRotating = true;
        }
        else if (wasRotating)
        {
            // Al soltar R mandamos el ángulo final exacto.
            wasRotating = false;
            rotationSyncTimer = 0f;
            SendYawToOthers();
        }
    }

    private void SendYawToOthers()
    {
        if (Mathf.Approximately(lastSentYaw, targetYaw))
            return;

        lastSentYaw = targetYaw;

        photonView.RPC(
            nameof(RPC_SetDisguiseYaw),
            RpcTarget.Others,
            targetYaw
        );
    }

    // Se envía el ángulo absoluto, no el incremento, para que
    // todos los clientes terminen con la misma orientación.
    [PunRPC]
    private void RPC_SetDisguiseYaw(float yaw)
    {
        targetYaw = yaw;
    }

    // Acerca el ángulo aplicado al objetivo.
    // El dueño lo aplica al instante; los remotos lo siguen
    // a una velocidad parecida para que se vea continuo.
    private void StepDisguiseRotation()
    {
        if (!isDisguised || disguiseRoot == null)
            return;

        float delta = Mathf.DeltaAngle(appliedYaw, targetYaw);

        if (Mathf.Abs(delta) < 0.001f)
            return;

        float step = delta;

        if (!photonView.IsMine)
        {
            float maxStep =
                rotationSpeed * 1.25f * Time.deltaTime;

            step = Mathf.Clamp(delta, -maxStep, maxStep);
        }

        appliedYaw = Mathf.Repeat(appliedYaw + step, 360f);

        RotateDisguise(step);
    }

    // Gira el disfraz sobre un eje vertical que pasa por el
    // centro de su malla, para que gire sobre sí mismo
    // y no orbite alrededor de otro punto.
    private void RotateDisguise(float degrees)
    {
        Vector3 pivot =
            disguiseRoot.TransformPoint(disguisePivotLocal);

        disguiseRoot.RotateAround(
            pivot,
            Vector3.up,
            degrees
        );
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

        Bounds bounds = CalculateRendererBounds(renderers);

        Vector3 playerPosition = transform.position;

        // Alineamos la base del objeto con la posición del jugador.
        newRoot.position += new Vector3(
            playerPosition.x - bounds.center.x,
            playerPosition.y - bounds.min.y,
            playerPosition.z - bounds.center.z
        );

        // Recalculamos los bounds después de mover la malla.
        // Las dimensiones del controlador se calculan sobre el disfraz real.
        bounds = CalculateRendererBounds(
            newRoot.GetComponentsInChildren<Renderer>()
        );

        // La colisión de movimiento sigue pudiendo ser pequeña para
        // permitir esconderse debajo de objetos grandes.
        FitControllerToDisguise(bounds);

        // Esta hitbox independiente sigue la malla visible del disfraz.
        // Es Trigger: detecta proyectiles, pero no bloquea el movimiento.
        AddDisguiseHitbox(newRoot);

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

        // Al transformarse en un prop nuevo se reinicia la rotación de R.
        appliedYaw = 0f;
        targetYaw = 0f;
        lastSentYaw = 0f;
        rotationSyncTimer = 0f;
        wasRotating = false;

        Debug.Log(
            "[PropTransform] " +
            PhotonNetwork.NickName +
            " se transformó en " +
            prop.name +
            " (" + pieces + " piezas)"
        );
    }

    // Calcula los bounds mundiales a partir de los renderers.
    private Bounds CalculateRendererBounds(Renderer[] renderers)
    {
        if (renderers == null || renderers.Length == 0)
            return new Bounds(transform.position, Vector3.one * 0.1f);

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    // Agrega una hitbox de disparo al disfraz, sin cambiar el collider
    // pequeño que usa el CharacterController para moverse.
    private void AddDisguiseHitbox(Transform root)
    {
        if (root == null)
            return;

        Bounds localBounds = CalculateLocalMeshBounds(root);

        // Guardamos el centro de la malla como punto de giro (tecla R).
        disguisePivotLocal = localBounds.center;

        BoxCollider hitbox = root.gameObject.AddComponent<BoxCollider>();
        hitbox.isTrigger = true;
        hitbox.center = localBounds.center;

        // Los meshes planos o muy finos necesitan un grosor mínimo para
        // que el proyectil pueda detectarlos de forma fiable.
        Vector3 scale = root.lossyScale;
        float minX = 0.05f / Mathf.Max(0.0001f, Mathf.Abs(scale.x));
        float minY = 0.05f / Mathf.Max(0.0001f, Mathf.Abs(scale.y));
        float minZ = 0.05f / Mathf.Max(0.0001f, Mathf.Abs(scale.z));

        hitbox.size = new Vector3(
            Mathf.Max(localBounds.size.x, minX),
            Mathf.Max(localBounds.size.y, minY),
            Mathf.Max(localBounds.size.z, minZ)
        );

        Debug.Log(
            "[PropTransform] Hitbox de disparo creada para el disfraz. " +
            "Centro: " + hitbox.center + " | Tamaño: " + hitbox.size
        );
    }

    // Calcula los límites de los meshes en coordenadas locales del root.
    // Esto hace que el collider acompañe correctamente la rotación y escala
    // del objeto transformado.
    private Bounds CalculateLocalMeshBounds(Transform root)
    {
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>();

        bool hasBounds = false;
        Bounds localBounds = new Bounds(Vector3.zero, Vector3.one * 0.1f);

        foreach (MeshFilter filter in filters)
        {
            if (filter == null || filter.sharedMesh == null)
                continue;

            Bounds meshBounds = filter.sharedMesh.bounds;
            Vector3 min = meshBounds.min;
            Vector3 max = meshBounds.max;

            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        Vector3 meshCorner = new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z
                        );

                        Vector3 worldCorner =
                            filter.transform.TransformPoint(meshCorner);

                        Vector3 rootLocalCorner =
                            root.InverseTransformPoint(worldCorner);

                        if (!hasBounds)
                        {
                            localBounds = new Bounds(
                                rootLocalCorner,
                                Vector3.zero
                            );

                            hasBounds = true;
                        }
                        else
                        {
                            localBounds.Encapsulate(rootLocalCorner);
                        }
                    }
                }
            }
        }

        if (!hasBounds)
            return new Bounds(Vector3.zero, Vector3.one * 0.1f);

        return localBounds;
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