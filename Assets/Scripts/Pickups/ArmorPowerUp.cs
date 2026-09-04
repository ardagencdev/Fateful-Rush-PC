using UnityEngine;

public class ArmorPowerUp : MonoBehaviour
{
    private Collider2D pickupCollider;
    private SoundManager soundManager;
    private SpawnScaleEffect spawnEffect;

    private bool collected;

    private void Awake()
    {
        pickupCollider = GetComponent<Collider2D>();
        spawnEffect = GetComponentInChildren<SpawnScaleEffect>();

        soundManager =
            FindAnyObjectByType<SoundManager>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected)
            return;

        if (!other.CompareTag("Player"))
            return;

        PlayerArmor armor =
            other.GetComponentInParent<PlayerArmor>();

        if (armor == null)
        {
            Debug.LogWarning(
                "[ArmorPowerUp] PlayerArmor component bulunamadı.",
                this
            );

            return;
        }

        collected = true;

        if (pickupCollider != null)
            pickupCollider.enabled = false;

        armor.ActivateArmor();

        VibrationManager.Instance?.VibratePowerUp();
        StatsManager.AddArmorBuffUse();

        if (soundManager != null)
            soundManager.PlayArmorCollectSound(transform.position);

        if (spawnEffect != null)
            spawnEffect.Collect();
        else
            Destroy(gameObject);
    }

    private void OnValidate()
    {
        if (pickupCollider == null)
            pickupCollider = GetComponent<Collider2D>();
    }
}