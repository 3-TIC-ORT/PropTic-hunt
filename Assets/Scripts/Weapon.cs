using System.Collections;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Vive en la raíz del Prefab "Player".
/// El daño se calcula al instante con Raycast (confiable, sin
/// física de red). El proyectil que se ve volar es puramente
/// visual: se avisa por RPC a todos los clientes con el punto
/// de inicio y de impacto ya calculados, y cada uno lo anima
/// localmente, sin necesidad de PhotonView propio en la bala.
/// </summary>
public class Weapon : MonoBehaviourPun
{
    [Header("Referencias")]
    public GameObject weaponVisual;
    public Transform cameraTransform;
    public Transform muzzlePoint;
    public GameObject bulletVisualPrefab;

    [Header("Config")]
    public float range = 100f;
    public float fireRate = 0.3f;
    public float bulletTravelTime = 0.05f;

    private float nextFireTime;

    void Update()
    {
        bool isArmed = photonView.Owner != null
            && GameManager.ArmedActorNumber == photonView.Owner.ActorNumber;

        if (weaponVisual != null)
            weaponVisual.SetActive(isArmed);

        if (!photonView.IsMine || !isArmed) return;

        if (Mouse.current != null
            && Mouse.current.leftButton.wasPressedThisFrame
            && Time.time >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + fireRate;
        }
    }

    void Shoot()
    {
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        Vector3 hitPoint = cameraTransform.position + cameraTransform.forward * range;

        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            hitPoint = hit.point;

            PhotonView targetView = hit.collider.GetComponentInParent<PhotonView>();

            if (targetView != null && targetView.Owner != photonView.Owner)
            {
                targetView.RPC(nameof(PlayerHealth.RPC_TakeDamage), RpcTarget.All);
            }

            Debug.Log("Disparo pegó en: " + hit.collider.name);
        }

        // Avisamos a TODOS (incluido yo mismo) que muestren la bala visual.
        photonView.RPC(nameof(RPC_ShowBulletVisual), RpcTarget.All, muzzlePoint.position, hitPoint);
    }

    [PunRPC]
    void RPC_ShowBulletVisual(Vector3 startPos, Vector3 endPos)
    {
        if (bulletVisualPrefab == null) return;

        GameObject bullet = Instantiate(bulletVisualPrefab, startPos, Quaternion.identity);
        StartCoroutine(MoveBulletVisual(bullet, startPos, endPos));
    }

    IEnumerator MoveBulletVisual(GameObject bullet, Vector3 start, Vector3 end)
    {
        float elapsed = 0f;

        while (elapsed < bulletTravelTime)
        {
            if (bullet == null) yield break; // por si se destruyó antes

            bullet.transform.position = Vector3.Lerp(start, end, elapsed / bulletTravelTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (bullet != null)
        {
            bullet.transform.position = end;
            Destroy(bullet);
        }
    }
}