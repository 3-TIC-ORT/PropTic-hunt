using Photon.Pun;
using UnityEngine;

public class HitboxBullet : MonoBehaviour
{
    private Vector3 direction;
    private float speed;
    private float maxDistance;
    private int shooterActorNumber;

    private Vector3 startPosition;
    private bool initialized;
    private bool hasHit;

    public void Initialize(
        Vector3 shootDirection,
        float bulletSpeed,
        float maximumDistance,
        int shooterActor)
    {
        direction = shootDirection.normalized;
        speed = bulletSpeed;
        maxDistance = maximumDistance;
        shooterActorNumber = shooterActor;

        startPosition = transform.position;
        initialized = true;

        transform.forward = direction;
    }

    private void Update()
    {
        if (!initialized || hasHit)
            return;

        float movement =
            speed * Time.deltaTime;

        transform.position +=
            direction * movement;

        float distance =
            Vector3.Distance(
                startPosition,
                transform.position
            );

        if (distance >= maxDistance)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!initialized || hasHit)
            return;

        PhotonView targetView =
            other.GetComponentInParent<PhotonView>();

        // No golpeamos al que disparó.
        if (targetView != null &&
            targetView.Owner != null &&
            targetView.Owner.ActorNumber ==
            shooterActorNumber)
        {
            return;
        }

        PlayerHealth health =
            other.GetComponentInParent<PlayerHealth>();

        if (health == null)
        {
            // Golpeó una pared u otro objeto.
            hasHit = true;
            Destroy(gameObject);
            return;
        }

        if (targetView == null)
        {
            hasHit = true;
            Destroy(gameObject);
            return;
        }

        hasHit = true;

        // El daño ocurre SOLAMENTE en el impacto.
        targetView.RPC(
            nameof(PlayerHealth.RPC_TakeDamage),
            RpcTarget.All
        );

        Debug.Log(
            "[HitboxBullet] Impacto en jugador: " +
            targetView.Owner.NickName
        );

        Destroy(gameObject);
    }
}
