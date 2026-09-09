using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

public class Weapon : MonoBehaviourPun
{
    [Header("Referencias")]
    public GameObject weaponVisual;

    [Tooltip("Cámara utilizada para apuntar.")]
    public Transform cameraTransform;

    [Tooltip("Punto desde donde sale visualmente la bala.")]
    public Transform muzzlePoint;

    [Tooltip("Punto desde donde sale la hitbox invisible.")]
    public Transform hitboxOrigin;

    [Tooltip("Prefab visual de la bala.")]
    public GameObject bulletVisualPrefab;

    [Tooltip("Prefab invisible encargado de detectar el impacto.")]
    public GameObject hitboxBulletPrefab;

    [Header("Configuración")]
    public float range = 100f;

    public float fireRate = 1f;

    [Tooltip("Velocidad de la hitbox invisible.")]
    public float bulletSpeed = 80f;

    private float nextFireTime;

    private bool IsOwnerAssassin()
    {
        if (photonView.Owner == null)
            return false;

        return GameManager.IsAssassinActor(
            photonView.Owner.ActorNumber
        );
    }

    private void Update()
    {
        bool isArmed =
            IsOwnerAssassin();

        if (weaponVisual != null)
        {
            weaponVisual.SetActive(isArmed);
        }

        // Solo el dueño controla el arma.
        if (!photonView.IsMine)
            return;

        // Los escapistas no pueden disparar.
        if (!isArmed)
            return;

        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame &&
            Time.time >= nextFireTime)
        {
            Shoot();

            nextFireTime =
                Time.time + fireRate;
        }
    }

    private void Shoot()
    {
        if (!photonView.IsMine)
            return;

        if (!IsOwnerAssassin())
            return;

        if (cameraTransform == null)
        {
            Debug.LogError(
                "[Weapon] Falta Camera Transform."
            );

            return;
        }

        if (muzzlePoint == null)
        {
            Debug.LogError(
                "[Weapon] Falta Muzzle Point."
            );

            return;
        }

        if (hitboxOrigin == null)
        {
            Debug.LogError(
                "[Weapon] Falta Hitbox Origin."
            );

            return;
        }

        if (hitboxBulletPrefab == null)
        {
            Debug.LogError(
                "[Weapon] Falta Hitbox Bullet Prefab."
            );

            return;
        }

        if (bulletVisualPrefab == null)
        {
            Debug.LogError(
                "[Weapon] Falta Bullet Visual Prefab."
            );

            return;
        }

        Vector3 direction =
            cameraTransform.forward.normalized;

        // -----------------------------
        // 1. HITBOX INVISIBLE
        // -----------------------------

        GameObject hitboxBullet =
            Instantiate(
                hitboxBulletPrefab,
                hitboxOrigin.position,
                Quaternion.LookRotation(direction)
            );

        HitboxBullet hitbox =
            hitboxBullet.GetComponent<HitboxBullet>();

        if (hitbox == null)
        {
            Debug.LogError(
                "[Weapon] El HitboxBullet prefab " +
                "no tiene HitboxBullet."
            );

            Destroy(hitboxBullet);
            return;
        }

        hitbox.Initialize(
            direction,
            bulletSpeed,
            range,
            photonView.Owner.ActorNumber
        );

        // -----------------------------
        // 2. BALA VISUAL
        // -----------------------------

        photonView.RPC(
            nameof(RPC_SpawnVisualBullet),
            RpcTarget.All,
            muzzlePoint.position,
            direction
        );
    }

    [PunRPC]
    private void RPC_SpawnVisualBullet(
        Vector3 startPosition,
        Vector3 direction)
    {
        if (bulletVisualPrefab == null)
            return;

        GameObject bullet =
            Instantiate(
                bulletVisualPrefab,
                startPosition,
                Quaternion.LookRotation(direction)
            );

        StartCoroutine(
            MoveVisualBullet(
                bullet,
                startPosition,
                direction
            )
        );
    }

    private System.Collections.IEnumerator MoveVisualBullet(
        GameObject bullet,
        Vector3 startPosition,
        Vector3 direction)
    {
        float elapsed = 0f;

        float duration =
            range / bulletSpeed;

        while (elapsed < duration)
        {
            if (bullet == null)
                yield break;

            bullet.transform.position =
                startPosition +
                direction *
                (bulletSpeed * elapsed);

            elapsed += Time.deltaTime;

            yield return null;
        }

        if (bullet != null)
        {
            Destroy(bullet);
        }
    }
}