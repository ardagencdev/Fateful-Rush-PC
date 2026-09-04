using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyBuffTarget : MonoBehaviour
{
    private static readonly HashSet<EnemyBuffTarget> activeTargets =
        new HashSet<EnemyBuffTarget>();

    public static IEnumerable<EnemyBuffTarget> ActiveTargets => activeTargets;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        activeTargets.Clear();
    }

    public GameObject buffAura;

    [Header("Buff Duration")]
    public float buffDuration = 15f;

    private bool buffed;
    private Coroutine buffRoutine;
    private BeaconEnemy buffSource;

    private EnemyFollow normal;
    private ProjectileEnemyFollow projectile;
    private HunterEnemyFollow hunter;

    private bool canReceiveBeaconBuff = true;
    private Vector3 scaleBeforeBuff;

    private float pMoveSpeed;
    private float pShotSpeed;
    private float pFireRate;
    private float hRep;
    private float hWarn;
    private float hCharge;
    private float hStun;

    public bool IsBuffed => buffed;
    public bool CanReceiveBeaconBuff => canReceiveBeaconBuff;
    public BeaconEnemy BuffSource => buffSource;

    private void Awake()
    {
        FindChildAura();
        SetAura(false);
        RefreshBaseValues();
    }

    private void OnEnable()
    {
        activeTargets.Add(this);
    }

    private void OnDisable()
    {
        activeTargets.Remove(this);
        RemoveBeaconBuff();
    }

    public void RefreshBaseValues()
    {
        normal = GetComponent<EnemyFollow>();
        projectile = GetComponent<ProjectileEnemyFollow>();
        hunter = GetComponent<HunterEnemyFollow>();

        canReceiveBeaconBuff =
            GetComponent<BeaconEnemy>() == null &&
            GetComponent<BossEnemyFollow>() == null &&
            GetComponent<MiniBossFollow>() == null &&
            GetComponentInParent<BeaconEnemy>() == null &&
            GetComponentInParent<BossEnemyFollow>() == null &&
            GetComponentInParent<MiniBossFollow>() == null;
    }

    private void FindChildAura()
    {
        Transform aura = transform.Find("BuffAura");
        if (aura != null)
            buffAura = aura.gameObject;
    }

    private void SetAura(bool state)
    {
        if (buffAura != null && buffAura.activeSelf != state)
            buffAura.SetActive(state);
    }

    public void ApplyBeaconBuff(
        BeaconEnemy source,
        float sizeMult,
        float nSpeedMult,
        float ignoredNormalMaxMult,
        float pMoveMult,
        float pShotMult,
        float pFireMult,
        float hRepMult,
        float hWarnMult,
        float hChargeMult,
        float hStunMult)
    {
        if (source == null || !canReceiveBeaconBuff || buffed)
            return;

        RefreshBaseValues();
        if (!canReceiveBeaconBuff)
            return;

        buffed = true;
        buffSource = source;
        scaleBeforeBuff = transform.localScale;

        if (buffRoutine != null)
            StopCoroutine(buffRoutine);

        SetAura(true);
        transform.localScale = scaleBeforeBuff * Mathf.Max(0.1f, sizeMult);

        // Beacon can temporarily accelerate a normal enemy, but its effective speed
        // is clamped by EnemyFollow.maxSpeed and the max speed itself is never raised.
        if (normal != null)
            normal.SetBeaconSpeedMultiplier(Mathf.Max(1f, nSpeedMult));

        if (projectile != null)
        {
            pMoveSpeed = projectile.moveSpeed;
            pShotSpeed = projectile.projectileSpeed;
            pFireRate = projectile.fireRate;

            projectile.moveSpeed = pMoveSpeed * Mathf.Max(0.1f, pMoveMult);
            projectile.projectileSpeed = pShotSpeed * Mathf.Max(0.1f, pShotMult);
            projectile.fireRate = pFireRate / Mathf.Max(0.1f, pFireMult);
        }

        if (hunter != null)
        {
            hRep = hunter.repositionTime;
            hWarn = hunter.warningDuration;
            hCharge = hunter.chargeSpeed;
            hStun = hunter.stunDuration;

            hunter.repositionTime = hRep * Mathf.Max(0.1f, hRepMult);
            hunter.warningDuration = hWarn * Mathf.Max(0.1f, hWarnMult);
            hunter.chargeSpeed = hCharge * Mathf.Max(0.1f, hChargeMult);
            hunter.stunDuration = hStun * Mathf.Max(0.1f, hStunMult);
        }

        buffRoutine = StartCoroutine(BuffDurationRoutine());
    }

    private IEnumerator BuffDurationRoutine()
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, buffDuration));
        buffRoutine = null;
        RemoveBeaconBuff();
    }

    public void RemoveBeaconBuff(BeaconEnemy requester = null)
    {
        if (!buffed)
            return;

        // A different Beacon must not remove a buff it did not create.
        if (requester != null && buffSource != requester)
            return;

        if (buffRoutine != null)
        {
            StopCoroutine(buffRoutine);
            buffRoutine = null;
        }

        buffed = false;
        buffSource = null;
        SetAura(false);
        transform.localScale = scaleBeforeBuff;

        if (normal != null)
            normal.SetBeaconSpeedMultiplier(1f);

        if (projectile != null)
        {
            projectile.moveSpeed = pMoveSpeed;
            projectile.projectileSpeed = pShotSpeed;
            projectile.fireRate = pFireRate;
        }

        if (hunter != null)
        {
            hunter.repositionTime = hRep;
            hunter.warningDuration = hWarn;
            hunter.chargeSpeed = hCharge;
            hunter.stunDuration = hStun;
        }
    }
}
