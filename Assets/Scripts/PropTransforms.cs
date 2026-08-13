using UnityEngine;
using UnityEngine.InputSystem;

public class PropTransform : MonoBehaviour
{
    public float detectionRange = 3f;

    private MeshFilter myMeshFilter;
    private MeshRenderer myMeshRenderer;
    private Mesh originalMesh;
    private Material[] originalMaterials;
    private Vector3 originalScale;

    private bool isDisguised = false;
    private GameObject currentTargetProp;

    void Start()
    {
        myMeshFilter = GetComponent<MeshFilter>();
        myMeshRenderer = GetComponent<MeshRenderer>();

        if (myMeshFilter == null) myMeshFilter = gameObject.AddComponent<MeshFilter>();
        if (myMeshRenderer == null) myMeshRenderer = gameObject.AddComponent<MeshRenderer>();

        originalMesh = myMeshFilter.sharedMesh;
        originalMaterials = myMeshRenderer.materials;
        originalScale = transform.localScale;
    }

    void Update()
    {
        DetectNearbyProp();

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (!isDisguised && currentTargetProp != null)
            {
                Disguise(currentTargetProp);
            }
            else if (isDisguised)
            {
                RevertDisguise();
            }
        }
    }

    void DetectNearbyProp()
    {
        currentTargetProp = null;
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRange);

        foreach (var hit in hits)
        {
            if (hit.CompareTag("Propable"))
            {
                currentTargetProp = hit.gameObject;
                break;
            }
        }
    }

    void Disguise(GameObject prop)
    {
        MeshFilter propMesh = prop.GetComponent<MeshFilter>();
        MeshRenderer propRenderer = prop.GetComponent<MeshRenderer>();

        if (propMesh == null || propRenderer == null) return;

        myMeshFilter.mesh = propMesh.sharedMesh;
        myMeshRenderer.materials = propRenderer.sharedMaterials;
        transform.localScale = prop.transform.localScale;

        isDisguised = true;
    }

    void RevertDisguise()
    {
        myMeshFilter.mesh = originalMesh;
        myMeshRenderer.materials = originalMaterials;
        transform.localScale = originalScale;

        isDisguised = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
