using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

public class Weapon : MonoBehaviourPun
{
    [Header("Arma en primera persona (solo la ve el dueño)")]
    [Tooltip("objeto_arma hija de la cámara, apuntando recto hacia adelante. " +
             "Reemplaza al cubo 'Weapon'.")]
    public GameObject weaponVisual;

    [Header("Arma en tercera persona (la ven los demás)")]
    [Tooltip("Prefab de objeto_arma que se pega a la mano derecha de Gero.")]
    public GameObject worldWeaponPrefab;

    [Tooltip("Tamaño del arma en tercera persona (1 = tamaño del prefab).")]
    public float weaponScale = 0.75f;

    [Tooltip("Desplazamiento extra respecto al punto medio entre las dos manos, " +
             "en ejes del jugador (X derecha, Y arriba, Z adelante).")]
    public Vector3 positionOffset = Vector3.zero;

    [Tooltip("Giro extra sobre la dirección hacia adelante (grados). Normalmente 0. " +
             "Si el cañón apunta para atrás, poné Y = 180.")]
    public Vector3 aimAlignedEuler = Vector3.zero;

    [Tooltip("Solo se usa si el arma NO tiene un objeto 'Muzzle'. " +
             "Rotación que lleva el cañón hacia adelante (probá Y = -90 o 90).")]
    public Vector3 manualBarrelEuler = new Vector3(0f, -90f, 0f);

    [Tooltip("Marcalo si el cañón apunta para el lado contrario de donde mira Gero.")]
    public bool invertirDireccion = true;

    [Tooltip("Dónde agarran las manos, medido desde la parte trasera del arma " +
             "(0 = en la culata, 0.5 = en el centro). Más bajo = el arma sobresale " +
             "más hacia adelante y no se mete en el torso.")]
    [Range(0f, 0.5f)]
    public float agarreDesdeAtras = 0.2f;

    [Tooltip("Inclinación del cañón en grados. Positivo = apunta hacia abajo.")]
    public float inclinacion = 15f;

    [Tooltip("Giro en diagonal del cañón en grados. Negativo = hacia la izquierda " +
             "de Gero, positivo = hacia la derecha.")]
    public float giroDiagonal = -20f;

    [Tooltip("Nombre del objeto vacío dentro de objeto_arma por donde sale la bala. " +
             "Si no existe se usa el centro del arma.")]
    public string worldMuzzleName = "Muzzle";

    [Header("Referencias")]
    [Tooltip("Cámara utilizada para apuntar.")]
    public Transform cameraTransform;

    [Tooltip("Punto de salida de la bala EN PRIMERA PERSONA (hijo del arma FPS).")]
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

    // Arma de tercera persona (se crea en runtime sobre Gero).
    private GameObject worldWeaponInstance;
    private Transform worldMuzzle;
    private Transform rightHandBone;
    private Transform leftHandBone;
    private Vector3 weaponLocalCenter;
    private bool hasGrip;
    private bool hasMuzzleDir;
    private Vector3 muzzleDirLocal;
    private float weaponLength;

    private bool IsOwnerAssassin()
    {
        if (photonView.Owner == null)
            return false;

        return GameManager.IsAssassinActor(
            photonView.Owner.ActorNumber
        );
    }

    // Busca el modelo de Gero (lo crea PlayerMovement) y crea el arma de
    // tercera persona. Devuelve false si Gero todavía no existe.
    private bool EnsureWorldWeapon()
    {
        if (worldWeaponInstance != null)
            return true;

        if (worldWeaponPrefab == null)
            return false;

        Transform gero = transform.Find("futuroGero");

        if (gero == null)
            return false;

        Animator animator = gero.GetComponentInChildren<Animator>(true);

        if (animator == null || !animator.isHuman)
            return false;

        rightHandBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
        leftHandBone = animator.GetBoneTransform(HumanBodyBones.LeftHand);

        if (rightHandBone == null)
            return false;

        // Hija del jugador (escala 1), no del hueso: así el tamaño es predecible.
        worldWeaponInstance = Instantiate(worldWeaponPrefab, transform, false);
        worldWeaponInstance.name = "objeto_arma_mundo";
        worldWeaponInstance.transform.localScale =
            worldWeaponPrefab.transform.localScale * weaponScale;

        worldMuzzle = worldWeaponInstance.transform.Find(worldMuzzleName);

        // Centro del modelo en su propio espacio local.
        Transform wt = worldWeaponInstance.transform;
        Renderer[] renderers =
            worldWeaponInstance.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            weaponLocalCenter = wt.InverseTransformPoint(bounds.center);
        }

        // Si el modelo tiene un objeto vacío "Grip", ese punto va sobre las manos.
        Transform grip = wt.Find("Grip");
        hasGrip = grip != null;

        if (hasGrip)
            weaponLocalCenter = wt.InverseTransformPoint(grip.position);

        // Dirección del cañón según el Muzzle (en espacio local del arma).
        hasMuzzleDir = false;

        if (worldMuzzle != null)
        {
            Vector3 dir =
                wt.InverseTransformPoint(worldMuzzle.position) - weaponLocalCenter;

            dir.y = 0f;

            if (dir.sqrMagnitude > 0.0001f)
            {
                weaponLength = dir.magnitude * 2f;
                muzzleDirLocal = dir.normalized;
                hasMuzzleDir = true;
            }
            else
            {
                worldMuzzle = null;
            }
        }

        if (worldMuzzle == null)
            worldMuzzle = wt;

        PlaceWorldWeapon();
        return true;
    }

    // Coloca el arma sobre las manos de Gero, apuntando hacia adelante
    // y en diagonal.
    private void PlaceWorldWeapon()
    {
        if (worldWeaponInstance == null || rightHandBone == null)
            return;

        Transform wt = worldWeaponInstance.transform;

        wt.localScale =
            worldWeaponPrefab.transform.localScale * weaponScale;

        // 1) Rotación que lleva el cañón hacia adelante.
        Quaternion barrelRot;
        Vector3 dir = Vector3.zero;

        if (hasMuzzleDir)
        {
            dir = invertirDireccion ? -muzzleDirLocal : muzzleDirLocal;
            barrelRot = Quaternion.FromToRotation(dir, Vector3.forward);
        }
        else
        {
            barrelRot = Quaternion.Euler(manualBarrelEuler);

            if (invertirDireccion)
                barrelRot = Quaternion.Euler(0f, 180f, 0f) * barrelRot;
        }

        // 2) Diagonal + ajustes manuales, en ejes del jugador.
        Quaternion rot =
            transform.rotation *
            Quaternion.Euler(0f, giroDiagonal, 0f) *
            Quaternion.Euler(inclinacion, 0f, 0f) *
            Quaternion.Euler(aimAlignedEuler) *
            barrelRot;

        // 3) Punto del arma que va sobre las manos: algo detrás del centro,
        //    para que el arma sobresalga hacia adelante.
        Vector3 gripLocal = weaponLocalCenter;

        if (!hasGrip && hasMuzzleDir)
            gripLocal -= dir * weaponLength * agarreDesdeAtras;

        Vector3 handsCenter = leftHandBone != null
            ? (rightHandBone.position + leftHandBone.position) * 0.5f
            : rightHandBone.position;

        wt.rotation = rot;

        wt.position =
            handsCenter +
            transform.rotation *
                Vector3.Scale(positionOffset, transform.lossyScale) -
            rot * Vector3.Scale(gripLocal, wt.lossyScale);
    }

    // Después de la animación, para que el arma siga a las manos.
    private void LateUpdate()
    {
        if (worldWeaponInstance != null &&
            worldWeaponInstance.activeSelf)
        {
            PlaceWorldWeapon();
        }
    }

    private void Update()
    {
        bool isArmed = IsOwnerAssassin();

        // Primera persona: solo el dueño y solo si es el asesino.
        if (weaponVisual != null)
            weaponVisual.SetActive(isArmed && photonView.IsMine);

        // Tercera persona: solo los demás jugadores.
        if (isArmed && !photonView.IsMine)
            EnsureWorldWeapon();

        if (worldWeaponInstance != null)
            worldWeaponInstance.SetActive(isArmed && !photonView.IsMine);

        // Solo el dueño controla el arma.
        if (!photonView.IsMine)
            return;

        if (!isArmed)
            return;

        if (ConfirmarSalida.MenuAbierto)
            return;

        if (FinDePartida.Terminada)
            return;

        if (RelojPartida.EnEspera)
            return;

        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame &&
            Time.time >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + fireRate;
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
            direction
        );
    }

    [PunRPC]
    private void RPC_SpawnVisualBullet(Vector3 direction)
    {
        if (bulletVisualPrefab == null)
            return;

        // Cada cliente elige su propio punto de salida:
        // el dueño dispara desde el arma FPS, los demás desde la mano de Gero.
        Vector3 startPosition;

        if (photonView.IsMine && muzzlePoint != null)
            startPosition = muzzlePoint.position;
        else if (worldMuzzle != null)
            startPosition = worldMuzzle.position;
        else
            startPosition = transform.position + Vector3.up * 1.5f;

        GameObject bullet =
            Instantiate(
                bulletVisualPrefab,
                startPosition,
                Quaternion.LookRotation(direction) *
                Quaternion.Euler(90f, 0f, 0f)
            );

        StartCoroutine(MoveVisualBullet(bullet, startPosition, direction));
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