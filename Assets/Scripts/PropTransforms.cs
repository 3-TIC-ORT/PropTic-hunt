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

    private MeshFilter visualMeshFilter;
    private MeshRenderer visualMeshRenderer;

    private Vector3 originalVisualPosition;
    private Vector3 originalVisualScale;
    private Quaternion originalVisualRotation;

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

        // Necesitamos saber el rol antes de permitir
        // utilizar la mecánica de props.
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
            Transform body =
                transform.Find("Body");

            if (body != null)
            {
                visualTarget = body;
            }
            else
            {
                visualTarget = transform;
            }
        }

        visualMeshFilter =
            visualTarget.GetComponent<MeshFilter>();

        visualMeshRenderer =
            visualTarget.GetComponent<MeshRenderer>();

        if (visualMeshFilter == null)
        {
            visualMeshFilter =
                visualTarget.gameObject.AddComponent<MeshFilter>();
        }

        if (visualMeshRenderer == null)
        {
            visualMeshRenderer =
                visualTarget.gameObject.AddComponent<MeshRenderer>();
        }

        originalVisualPosition =
            visualTarget.localPosition;

        originalVisualScale =
            visualTarget.localScale;

        originalVisualRotation =
            visualTarget.localRotation;
    }

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

        float closestDistance =
            float.MaxValue;

        foreach (Collider hit in hits)
        {
            GameObject prop =
                FindPropObject(hit.transform);

            if (prop == null)
                continue;

            // Si ya somos ese prop, no queremos
            // volver a seleccionarlo.
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

    private GameObject FindPropObject(
        Transform hitTransform)
    {
        Transform current =
            hitTransform;

        while (current != null)
        {
            if (current.CompareTag(propTag))
                return current.gameObject;

            current = current.parent;
        }

        return null;
    }

    private void TryTransform()
    {
        // Tiene que haber un prop realmente cercano.
        if (currentTargetProp == null)
            return;

        int propIndex =
            GetPropIndex(currentTargetProp);

        if (propIndex < 0)
        {
            Debug.LogWarning(
                "[PropTransform] No se pudo encontrar " +
                "el índice del prop."
            );

            return;
        }

        // El servidor de decisión acá es el propio dueño
        // del Player, pero el resultado visual se comunica
        // a TODOS mediante Photon.
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
            .OrderBy(
                prop => prop.name,
                StringComparer.Ordinal
            )
            .ThenBy(
                prop => prop.transform.position.x
            )
            .ThenBy(
                prop => prop.transform.position.y
            )
            .ThenBy(
                prop => prop.transform.position.z
            )
            .ToArray();
    }

    private int GetPropIndex(GameObject prop)
    {
        GameObject[] props =
            GetOrderedProps();

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
        GameObject[] props =
            GetOrderedProps();

        if (propIndex < 0 ||
            propIndex >= props.Length)
        {
            Debug.LogError(
                "[PropTransform] Índice de prop inválido: " +
                propIndex
            );

            return;
        }

        GameObject prop =
            props[propIndex];

        ApplyPropVisual(prop);
    }

    private void ApplyPropVisual(
        GameObject prop)
    {
        MeshFilter propMesh =
            prop.GetComponentInChildren<MeshFilter>();

        MeshRenderer propRenderer =
            prop.GetComponentInChildren<MeshRenderer>();

        if (propMesh == null ||
            propRenderer == null)
        {
            Debug.LogWarning(
                "[PropTransform] El objeto " +
                prop.name +
                " no tiene MeshFilter/MeshRenderer."
            );

            return;
        }

        if (visualMeshFilter == null ||
            visualMeshRenderer == null)
        {
            InitializeVisual();
        }

        // Copiamos la apariencia del prop.
        visualMeshFilter.sharedMesh =
            propMesh.sharedMesh;

        visualMeshRenderer.sharedMaterials =
            propRenderer.sharedMaterials;

        Vector3 propScale =
            prop.transform.lossyScale;

        Bounds bounds =
            propMesh.sharedMesh.bounds;

        Vector3 scaledCenter =
            Vector3.Scale(
                bounds.center,
                propScale
            );

        Vector3 scaledMin =
            Vector3.Scale(
                bounds.min,
                propScale
            );

        // Centramos visualmente el objeto
        // y hacemos que toque el suelo.
        visualTarget.localPosition =
            new Vector3(
                -scaledCenter.x,
                -scaledMin.y,
                -scaledCenter.z
            );

        visualTarget.localScale =
            propScale;

        // Conservamos la orientación horizontal
        // del prop.
        visualTarget.localRotation =
            Quaternion.Euler(
                0f,
                prop.transform.eulerAngles.y,
                0f
            );

        currentProp =
            prop;

        isDisguised = true;

        Debug.Log(
            "[PropTransform] " +
            PhotonNetwork.NickName +
            " se transformó en " +
            prop.name
        );
    }

    public bool IsDisguised()
    {
        return isDisguised;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color =
            Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            detectionRange
        );
    }
}