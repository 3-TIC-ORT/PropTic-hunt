using System;
using Photon.Pun;
using UnityEngine;

public class HitboxBullet : MonoBehaviour
{
    [Header("Detección del impacto")]
    [Tooltip("Radio del barrido físico del proyectil invisible.")]
    [SerializeField, Min(0.001f)] private float collisionRadius = 0.04f;

    [Tooltip("Capas que puede alcanzar el proyectil. Por defecto, todas.")]
    [SerializeField] private LayerMask collisionMask = Physics.AllLayers;

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
        speed = Mathf.Max(0f, bulletSpeed);
        maxDistance = Mathf.Max(0f, maximumDistance);
        shooterActorNumber = shooterActor;

        startPosition = transform.position;
        initialized = true;

        if (direction.sqrMagnitude > 0.0001f)
            transform.forward = direction;

        if (maxDistance <= 0f || speed <= 0f)
        {
            hasHit = true;
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (!initialized || hasHit)
            return;

        float travelledDistance =
            Vector3.Distance(startPosition, transform.position);

        float remainingDistance = maxDistance - travelledDistance;

        if (remainingDistance <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        float stepDistance = Mathf.Min(
            speed * Time.deltaTime,
            remainingDistance
        );

        if (stepDistance <= 0f)
            return;

        // Barrido entre la posición actual y la siguiente. A diferencia de
        // depender solo de OnTriggerEnter, esto reduce los casos en los que
        // un proyectil rápido atraviesa colliders pequeños entre frames.
        RaycastHit[] hits = Physics.SphereCastAll(
            transform.position,
            collisionRadius,
            direction,
            stepDistance,
            collisionMask,
            QueryTriggerInteraction.Collide
        );

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            Collider other = hit.collider;

            if (other == null)
                continue;

            // Ignoramos cualquier collider perteneciente al propio proyectil.
            if (other.transform == transform || other.transform.IsChildOf(transform))
                continue;

            PhotonView targetView =
                other.GetComponentInParent<PhotonView>();

            // El disparo no puede impactar al propio asesino.
            if (targetView != null &&
                targetView.Owner != null &&
                targetView.Owner.ActorNumber == shooterActorNumber)
            {
                continue;
            }

            PlayerHealth health =
                other.GetComponentInParent<PlayerHealth>();

            if (health != null &&
                targetView != null &&
                targetView.Owner != null)
            {
                hasHit = true;

                // Conservamos la lógica de daño por RPC que ya usa el proyecto.
                targetView.RPC(
                    nameof(PlayerHealth.RPC_TakeDamage),
                    RpcTarget.All
                );

                Debug.Log(
                    "[HitboxBullet] Impacto en jugador: " +
                    targetView.Owner.NickName
                );

                Destroy(gameObject);
                return;
            }

            // Una pared, un mueble u otro collider bloquea el proyectil.
            // Así el disparo no atraviesa objetos sólidos.
            hasHit = true;
            Destroy(gameObject);
            return;
        }

        transform.position += direction * stepDistance;

        if (stepDistance >= remainingDistance)
            Destroy(gameObject);
    }
}
