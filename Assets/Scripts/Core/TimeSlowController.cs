using System.Collections;
using UnityEngine;

public class TimeSlowController : MonoBehaviour
{
    public static TimeSlowController Instance { get; private set; }

    private float originalFixedDeltaTime;
    private Coroutine slowRoutine;

    private PlayerMovement player;
    private GameQuit pauseMenu;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        originalFixedDeltaTime = Time.fixedDeltaTime;

        player = FindAnyObjectByType<PlayerMovement>();
        pauseMenu = FindAnyObjectByType<GameQuit>();
    }

    public void StartSlow(float multiplier, float duration)
    {
        if (IsGameOver())
            return;

        multiplier = Mathf.Clamp(multiplier, 0.01f, 1f);
        duration = Mathf.Max(0f, duration);

        if (slowRoutine != null)
        {
            StopCoroutine(slowRoutine);
            slowRoutine = null;
        }

        SlowPowerUp.isSlowActive = true;
        SlowPowerUp.currentSlowMultiplier = multiplier;
        GameAudioMixerController.SetSlowMotion(true);

        slowRoutine = StartCoroutine(
            SlowRoutine(multiplier, duration)
        );
    }

    private IEnumerator SlowRoutine(
        float multiplier,
        float duration)
    {
        ApplySlow(multiplier);

        float timer = 0f;

        while (timer < duration)
        {
            if (IsGameOver())
            {
                ForceStopForGameEnd();
                yield break;
            }

            if (pauseMenu == null)
            {
                pauseMenu =
                    FindAnyObjectByType<GameQuit>();
            }

            if (pauseMenu == null ||
                !pauseMenu.IsPaused)
            {
                timer += Time.unscaledDeltaTime;
            }

            yield return null;
        }

        ResetTime();
    }

    public void ResetTime()
    {
        if (slowRoutine != null)
        {
            StopCoroutine(slowRoutine);
            slowRoutine = null;
        }

        SlowPowerUp.isSlowActive = false;
        SlowPowerUp.currentSlowMultiplier = 1f;
        GameAudioMixerController.SetSlowMotion(false);

        Time.fixedDeltaTime =
            originalFixedDeltaTime;

        if (IsGameOver())
            return;

        if (pauseMenu == null)
        {
            pauseMenu =
                FindAnyObjectByType<GameQuit>();
        }

        Time.timeScale =
            pauseMenu != null &&
            pauseMenu.IsPaused
                ? 0f
                : 1f;
    }

    public void ResumeAfterPause()
    {
        if (IsGameOver())
            return;

        if (SlowPowerUp.isSlowActive)
        {
            float multiplier =
                Mathf.Clamp(
                    SlowPowerUp.currentSlowMultiplier,
                    0.01f,
                    1f
                );

            GameAudioMixerController.SetSlowMotion(true);
            ApplySlow(multiplier);
        }
        else
        {
            GameAudioMixerController.SetSlowMotion(false);
            Time.timeScale = 1f;
            Time.fixedDeltaTime =
                originalFixedDeltaTime;
        }
    }

    public void ForceStopForGameEnd()
    {
        if (slowRoutine != null)
        {
            StopCoroutine(slowRoutine);
            slowRoutine = null;
        }

        SlowPowerUp.isSlowActive = false;
        SlowPowerUp.currentSlowMultiplier = 1f;
        GameAudioMixerController.SetSlowMotion(false);

        Time.timeScale = 1f;
        Time.fixedDeltaTime =
            originalFixedDeltaTime;
    }

    private void ApplySlow(float multiplier)
    {
        Time.timeScale = multiplier;

        Time.fixedDeltaTime =
            originalFixedDeltaTime * multiplier;
    }

    private bool IsGameOver()
    {
        if (player == null)
        {
            player =
                FindAnyObjectByType<PlayerMovement>();
        }

        return player != null &&
               player.IsGameOver;
    }

    private void OnDisable()
    {
        CleanupTimeState();
    }

    private void OnDestroy()
    {
        CleanupTimeState();

        if (Instance == this)
            Instance = null;
    }

    private void CleanupTimeState()
    {
        if (slowRoutine != null)
        {
            StopCoroutine(slowRoutine);
            slowRoutine = null;
        }

        SlowPowerUp.isSlowActive = false;
        SlowPowerUp.currentSlowMultiplier = 1f;
        GameAudioMixerController.SetSlowMotion(false);

        Time.timeScale = 1f;
        Time.fixedDeltaTime =
            originalFixedDeltaTime;
    }
}