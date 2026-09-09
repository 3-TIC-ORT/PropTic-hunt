using Photon.Pun;
using UnityEngine;

public class BulletProjectile : MonoBehaviour
{
    private Vector3 direction;
    private float speed;
    private float maxDistance;
    private int shooterActorNumber;
    private bool isAuthority;

    private Vector3 startPosition;
    private bool initialized;

    public void Initialize(
        Vector3 shootDirection,
        float bulletSpeed,
        float bulletMaxDistance,
        int shooterActor,
        bool authority)
    {
        direction = shootDirection.normalized;
        speed = bulletSpeed;
        maxDistance = bulletMaxDistance;
        shooterActorNumber = shooterActor;
        isAuthority = authority;

        startPosition = transform.position;
        initialized = true;

        if (direction.sqrMagnitude > 0.001f)
        {
            transform.forward = direction;
        }
    }

    private void Update()
    {
        if (!initialized)
            return;

        float movementDistance = speed * Time.deltaTime;

        if (movementDistance <= 0f)
            movementDistance = 0.01f;

        if (isAuthority)
        {
            CheckCollisionAndMove(movementDistance);
        }
        else
        {
            transform.position += direction * movementDistance;

            if (Vector3.Distance(
                    startPosition,
                    transform.position) >= maxDistance)
            {
                Destroy(gameObject);
            }
        }
    }

    private void CheckCollisionAndMove(float movementDistance)
    {
        Vector3 currentPosition = transform.position;
        Vector3 nextPosition =
            currentPosition + direction * movementDistance;

        RaycastHit[] hits = Physics.RaycastAll(
            currentPosition,
            direction,
            movementDistance
        );

        if (hits.Length > 0)
        {
            System.Array.Sort(
                hits,
                (a, b) => a.distance.CompareTo(b.distance)
            );

            foreach (RaycastHit hit in hits)
            {
                PhotonView targetView =
                    hit.collider.GetComponentInParent<PhotonView>();

                // Ignoramos cualquier collider perteneciente
                // al jugador que disparó.
                if (targetView != null &&
                    targetView.Owner != null &&
                    targetView.Owner.ActorNumber ==
                    shooterActorNumber)
                {
                    continue;
                }

                transform.position = hit.point;

                // Si la bala tocó a otro jugador,
                // recién en ESTE MOMENTO hacemos el daño.
                if (targetView != null &&
                    targetView.Owner != null &&
                    targetView.Owner.ActorNumber !=
                    shooterActorNumber)
                {
                    PlayerHealth health =
                        targetView.GetComponent<PlayerHealth>();

                    if (health != null)
                    {
                        targetView.RPC(
                            nameof(PlayerHealth.RPC_TakeDamage),
                            RpcTarget.All
                        );

                        Debug.Log(
                            "[Bala] Impacto en jugador: " +
                            targetView.Owner.NickName
                        );
                    }
                }
                else
                {
                    Debug.Log(
                        "[Bala] Impacto contra: " +
                        hit.collider.name
                    );
                }

                Destroy(gameObject);
                return;
            }
        }

        transform.position = nextPosition;

        if (Vector3.Distance(
                startPosition,
                transform.position) >= maxDistance)
        {
            Destroy(gameObject);
        }
    }
}