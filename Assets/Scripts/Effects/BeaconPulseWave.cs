using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class BeaconPulseWave : MonoBehaviour
{
    [Header("Wave")]
    public float duration = 1f;
    public float startScale = 0.1f;
    public float endScale = 6f;

    [Header("Buff Check")]
    public LayerMask enemyLayers;

    private SpriteRenderer sr;
    private BeaconEnemy source;
    private bool canBuff;
    private float timer;
    private Color baseColor;

    private readonly HashSet<EnemyBuffTarget> hitTargets =
        new HashSet<EnemyBuffTarget>();

    public void Initialize(BeaconEnemy beacon, bool buffEnabled)
    {
        source = beacon;
        canBuff = buffEnabled;
    }

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        baseColor = sr != null ? sr.color : Color.white;
        duration = Mathf.Max(0.01f, duration);
    }

    private void OnEnable()
    {
        timer = 0f;
        source = null;
        canBuff = false;
        hitTargets.Clear();
        transform.localScale = Vector3.one * startScale;

        if (sr != null)
            sr.color = baseColor;
    }

    private void Update()
    {
        timer += Time.deltaTime;

        float t = Mathf.Clamp01(timer / Mathf.Max(0.01f, duration));
        float currentScale = Mathf.Lerp(startScale, endScale, t);

        transform.localScale = Vector3.one * currentScale;

        if (canBuff && source != null)
        {
            float radius =
                sr != null
                    ? sr.bounds.extents.x + 0.4f
                    : currentScale;

            CheckBuffTargets(radius);
        }

        if (sr != null)
        {
            Color color = baseColor;
            color.a = Mathf.Lerp(baseColor.a, 0f, t);
            sr.color = color;
        }

        if (timer >= duration)
            RuntimeObjectPool.Release(gameObject);
    }

    private void CheckBuffTargets(float radius)
    {
        float radiusSquared = radius * radius;
        Vector2 center = transform.position;

        foreach (EnemyBuffTarget target in EnemyBuffTarget.ActiveTargets)
        {
            if (target == null ||
                target.IsBuffed ||
                !target.CanReceiveBeaconBuff ||
                hitTargets.Contains(target))
            {
                continue;
            }

            Vector2 targetPosition = target.transform.position;

            if ((targetPosition - center).sqrMagnitude > radiusSquared)
                continue;

            hitTargets.Add(target);
            source.ApplyBuffToTarget(target.gameObject);
        }
    }

    private void OnDisable()
    {
        source = null;
        canBuff = false;
        timer = 0f;
        hitTargets.Clear();

        if (sr != null)
            sr.color = baseColor;
    }
}
