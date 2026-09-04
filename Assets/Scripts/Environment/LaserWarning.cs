using System.Collections;
using UnityEngine;

public class LaserWarning : MonoBehaviour
{
    [Header("References")]
    public SpriteRenderer spriteRenderer;

    [Header("Warning")]
    public float blinkDuration = 2f;
    public int blinkCount = 2;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public IEnumerator PlayWarning()
    {
        if (spriteRenderer == null)
            yield break;

        int safeBlinkCount = Mathf.Max(1, blinkCount);
        float safeDuration = Mathf.Max(0.01f, blinkDuration);

        float singleBlinkTime =
            safeDuration / (safeBlinkCount * 2f);

        for (int i = 0; i < safeBlinkCount; i++)
        {
            PlayWarningPulse();
            SetVisible(true);
            yield return new WaitForSeconds(singleBlinkTime);

            SetVisible(false);
            yield return new WaitForSeconds(singleBlinkTime);
        }

        SetVisible(false);
    }

    private void PlayWarningPulse()
    {
        SoundManager soundManager = SoundManager.Instance;

        if (soundManager == null)
            soundManager = FindAnyObjectByType<SoundManager>();

        soundManager?.PlayLaserWarningSound(transform.position);
    }

    private void SetVisible(bool visible)
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.enabled = visible;
    }

    private void OnDisable()
    {
        SetVisible(false);
    }
}