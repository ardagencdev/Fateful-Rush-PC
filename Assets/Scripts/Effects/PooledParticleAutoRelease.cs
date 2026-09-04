using UnityEngine;

/// <summary>
/// Returns pooled particle effects when all child particle systems finish.
/// Added at runtime, so particle prefabs do not need manual setup.
/// </summary>
public sealed class PooledParticleAutoRelease : MonoBehaviour
{
    private ParticleSystem[] systems;
    private bool armed;

    public static void Arm(GameObject particleObject)
    {
        if (particleObject == null)
            return;

        PooledParticleAutoRelease release =
            particleObject.GetComponent<PooledParticleAutoRelease>();

        if (release == null)
            release = particleObject.AddComponent<PooledParticleAutoRelease>();

        release.CacheSystems();
        release.armed = true;
    }

    private void CacheSystems()
    {
        if (systems == null || systems.Length == 0)
        {
            systems = GetComponentsInChildren<ParticleSystem>(true);
        }
    }

    private void OnDisable()
    {
        armed = false;
    }

    private void LateUpdate()
    {
        if (!armed)
            return;

        CacheSystems();

        if (systems == null || systems.Length == 0)
        {
            armed = false;
            RuntimeObjectPool.Release(gameObject);
            return;
        }

        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem system = systems[i];

            if (system != null && system.IsAlive(true))
                return;
        }

        armed = false;
        RuntimeObjectPool.Release(gameObject);
    }
}
