using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MenuFloatingText : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text textUI;
    [SerializeField] private RectTransform target;

    [Header("Message Cycle")]
    [SerializeField] private bool cycleMessages;
    [SerializeField] private bool randomOrder;
    [SerializeField] private bool showFirstMessageImmediately = true;

    [TextArea]
    [SerializeField]
    private string[] messages =
    {
        "Fate Is Closing In",
        "The Stars Have Gone Silent",
        "Every Second Seals Your Path",
        "No Signal Reaches Home",
        "Something Is Following You",
        "The Future Is Collapsing",
        "Time Refuses To Wait",
        "Your Next Move Is Final",
        "The Dark Remembers Your Name",
        "Survival Was Never Promised",
        "There Is No Safe Vector",
        "The End Is Gaining Ground",
        "Run Before Fate Finds You",
        "Fate Moves Faster Than You",
        "The Countdown Has Begun",
        "Only Motion Keeps You Alive"
    };

    [Min(0.1f)]
    [SerializeField] private float messageDuration = 6f;

    [Min(0.05f)]
    [SerializeField] private float crossFadeDuration = 0.45f;

    [Header("Ambient Motion")]
    [SerializeField] private bool ambientMotion = true;
    [SerializeField] private Vector2 driftAmount = new Vector2(6f, 3f);

    [Min(0f)]
    [SerializeField] private float driftSpeed = 0.08f;

    [Range(0f, 5f)]
    [SerializeField] private float rotationAmount = 1.25f;

    [Range(0f, 0.08f)]
    [SerializeField] private float breatheAmount = 0.012f;

    [Min(0f)]
    [SerializeField] private float breatheSpeed = 0.55f;

    [Header("Opacity")]
    [Range(0f, 1f)]
    [SerializeField] private float baseOpacity = 0.5f;

    [Range(0f, 0.4f)]
    [SerializeField] private float opacityPulseAmount = 0.06f;

    [Min(0f)]
    [SerializeField] private float opacityPulseSpeed = 0.45f;

    [Header("Rare Signal Glitch")]
    [SerializeField] private bool glitch;

    [Min(0.5f)]
    [SerializeField] private float glitchIntervalMin = 10f;

    [Min(0.5f)]
    [SerializeField] private float glitchIntervalMax = 20f;

    [Range(0.02f, 0.2f)]
    [SerializeField] private float glitchDuration = 0.07f;

    [Range(0f, 4f)]
    [SerializeField] private float glitchPositionAmount = 1.25f;

    [Range(1, 3)]
    [SerializeField] private int glitchCharacterChanges = 1;

    [SerializeField] private string glitchCharacters = "#@$%&!?<>/\\|";

    private readonly List<string> usableMessages = new List<string>();

    // localPosition is intentionally used instead of anchoredPosition.
    // Different RectTransform anchors interpret anchoredPosition differently,
    // while localPosition represents the same physical point under a shared parent.
    private Vector3 baseLocalPosition;
    private Vector3 baseScale;
    private Quaternion baseRotation;
    private Color baseColor;

    private float noiseSeedX;
    private float noiseSeedY;
    private float phase;
    private float messageAlpha = 1f;

    private Vector2 glitchOffset;
    private string stableText = string.Empty;
    private int currentMessageIndex = -1;
    private int previousRandomIndex = -1;

    private bool initialized;
    private bool sharedGlitchControlled;

    private Coroutine initializationRoutine;
    private Coroutine messageRoutine;
    private Coroutine glitchRoutine;

    public bool IsInitialized => initialized;

    public bool IsReadyForSharedGlitch =>
        initialized &&
        isActiveAndEnabled &&
        textUI != null &&
        target != null &&
        messageAlpha >= 0.95f &&
        !string.IsNullOrEmpty(stableText);

    public string StableText => stableText;
    public Vector3 BaseLocalPosition => baseLocalPosition;
    public Transform TargetParent => target != null ? target.parent : null;

    public void SetThemeColor(Color themeColor)
    {
        ResolveReferences();

        if (textUI == null)
            return;

        themeColor.r = Mathf.Clamp01(themeColor.r);
        themeColor.g = Mathf.Clamp01(themeColor.g);
        themeColor.b = Mathf.Clamp01(themeColor.b);

        // MenuFloatingText drives TMP color every frame through baseColor.
        // Updating only textUI.color would therefore be overwritten on the
        // next Update(). Keep both the cached base RGB and current alpha.
        if (initialized)
        {
            baseColor = new Color(
                themeColor.r,
                themeColor.g,
                themeColor.b,
                baseColor.a
            );
        }

        Color currentColor = textUI.color;

        textUI.color = new Color(
            themeColor.r,
            themeColor.g,
            themeColor.b,
            currentColor.a
        );
    }

    private void Reset()
    {
        textUI = GetComponent<TMP_Text>();
        target = transform as RectTransform;
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (initialized)
        {
            RestoreBaseState();
            StartEffects();
            return;
        }

        initializationRoutine = StartCoroutine(InitializeAfterLayout());
    }

    private IEnumerator InitializeAfterLayout()
    {
        // Let the Canvas and anchors finish their first layout pass.
        yield return null;
        Canvas.ForceUpdateCanvases();

        if (!isActiveAndEnabled || textUI == null || target == null)
            yield break;

        CaptureBaseState();
        BuildUsableMessages();

        noiseSeedX = Random.Range(0f, 1000f);
        noiseSeedY = Random.Range(1000f, 2000f);
        phase = Random.Range(0f, Mathf.PI * 2f);

        initialized = true;
        initializationRoutine = null;

        StartEffects();
    }

    private void Update()
    {
        if (!initialized || textUI == null || target == null)
            return;

        float time = Time.unscaledTime;

        ApplyMotion(time);
        ApplyOpacity(time);
    }

    private void ResolveReferences()
    {
        if (textUI == null)
            textUI = GetComponent<TMP_Text>();

        if (target == null)
            target = transform as RectTransform;
    }

    private void CaptureBaseState()
    {
        baseLocalPosition = target.localPosition;
        baseScale = target.localScale;
        baseRotation = target.localRotation;
        baseColor = textUI.color;
        stableText = textUI.text;
    }

    private void BuildUsableMessages()
    {
        usableMessages.Clear();

        if (messages != null)
        {
            for (int i = 0; i < messages.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(messages[i]))
                    usableMessages.Add(messages[i].Trim());
            }
        }

        if (usableMessages.Count == 0 && !string.IsNullOrWhiteSpace(stableText))
            usableMessages.Add(stableText.Trim());
    }

    private void StartEffects()
    {
        StopEffectCoroutines();

        glitchOffset = Vector2.zero;
        messageAlpha = 1f;

        if (cycleMessages && usableMessages.Count > 0)
            messageRoutine = StartCoroutine(MessageCycleRoutine());
        else
            textUI.text = stableText;

        if (glitch && !sharedGlitchControlled)
            glitchRoutine = StartCoroutine(GlitchRoutine());
    }

    private void StopEffectCoroutines()
    {
        if (messageRoutine != null)
        {
            StopCoroutine(messageRoutine);
            messageRoutine = null;
        }

        if (glitchRoutine != null)
        {
            StopCoroutine(glitchRoutine);
            glitchRoutine = null;
        }
    }

    private void ApplyMotion(float time)
    {
        Vector2 offset = Vector2.zero;
        float rotation = 0f;
        float scaleMultiplier = 1f;

        if (ambientMotion)
        {
            float noiseTime = time * driftSpeed;

            float xNoise = Mathf.PerlinNoise(noiseSeedX, noiseTime) * 2f - 1f;
            float yNoise = Mathf.PerlinNoise(noiseSeedY, noiseTime) * 2f - 1f;

            offset = new Vector2(
                xNoise * driftAmount.x,
                yNoise * driftAmount.y
            );

            float rotationNoise =
                Mathf.PerlinNoise(noiseSeedX + 73.1f, noiseTime * 0.8f) * 2f - 1f;

            rotation = rotationNoise * rotationAmount;

            if (breatheAmount > 0f)
            {
                scaleMultiplier += Mathf.Sin(
                    time * breatheSpeed * Mathf.PI * 2f + phase
                ) * breatheAmount;
            }
        }

        Vector2 totalOffset = offset + glitchOffset;
        target.localPosition = baseLocalPosition + new Vector3(totalOffset.x, totalOffset.y, 0f);
        target.localRotation = baseRotation * Quaternion.Euler(0f, 0f, rotation);
        target.localScale = baseScale * scaleMultiplier;
    }

    private void ApplyOpacity(float time)
    {
        float pulse = 0f;

        if (opacityPulseAmount > 0f)
        {
            pulse = Mathf.Sin(
                time * opacityPulseSpeed * Mathf.PI * 2f + phase
            ) * opacityPulseAmount;
        }

        Color color = baseColor;
        color.a = Mathf.Clamp01(baseOpacity + pulse) * messageAlpha;
        textUI.color = color;
    }

    private IEnumerator MessageCycleRoutine()
    {
        bool firstMessage = true;

        while (isActiveAndEnabled)
        {
            string nextMessage = GetNextMessage();

            if (firstMessage && showFirstMessageImmediately)
            {
                stableText = nextMessage;
                textUI.text = stableText;
                messageAlpha = 1f;
            }
            else
            {
                yield return FadeMessage(1f, 0f);

                stableText = nextMessage;
                textUI.text = stableText;

                yield return FadeMessage(0f, 1f);
            }

            firstMessage = false;

            yield return new WaitForSecondsRealtime(messageDuration);
        }
    }

    private IEnumerator FadeMessage(float from, float to)
    {
        float duration = Mathf.Max(0.05f, crossFadeDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);

            messageAlpha = Mathf.LerpUnclamped(from, to, t);
            yield return null;
        }

        messageAlpha = to;
    }

    private string GetNextMessage()
    {
        if (usableMessages.Count == 0)
            return stableText;

        if (usableMessages.Count == 1)
            return usableMessages[0];

        if (randomOrder)
        {
            int selectedIndex;

            do
            {
                selectedIndex = Random.Range(0, usableMessages.Count);
            }
            while (selectedIndex == previousRandomIndex);

            previousRandomIndex = selectedIndex;
            return usableMessages[selectedIndex];
        }

        currentMessageIndex = (currentMessageIndex + 1) % usableMessages.Count;
        return usableMessages[currentMessageIndex];
    }

    private IEnumerator GlitchRoutine()
    {
        while (isActiveAndEnabled)
        {
            float minimum = Mathf.Max(0.5f, glitchIntervalMin);
            float maximum = Mathf.Max(minimum, glitchIntervalMax);

            yield return new WaitForSecondsRealtime(Random.Range(minimum, maximum));

            if (!IsReadyForSharedGlitch)
                continue;

            float elapsed = 0f;

            while (elapsed < glitchDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                ApplySharedGlitchFrame();
                yield return null;
            }

            EndSharedGlitch();
        }
    }

    public void SetSharedGlitchControlled(bool controlled)
    {
        sharedGlitchControlled = controlled;

        if (controlled)
        {
            if (glitchRoutine != null)
            {
                StopCoroutine(glitchRoutine);
                glitchRoutine = null;
            }

            if (initialized)
                EndSharedGlitch();

            return;
        }

        if (
            glitch &&
            initialized &&
            isActiveAndEnabled &&
            glitchRoutine == null
        )
        {
            glitchRoutine = StartCoroutine(GlitchRoutine());
        }
    }

    public void SetStableText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        stableText = value;

        if (textUI != null)
            textUI.text = stableText;
    }

    public void SetBaseLocalPosition(Vector3 position)
    {
        baseLocalPosition = position;

        if (target == null)
            return;

        target.localPosition = baseLocalPosition +
            new Vector3(glitchOffset.x, glitchOffset.y, 0f);
    }

    public void ApplySharedGlitchFrame()
    {
        if (!IsReadyForSharedGlitch)
            return;

        glitchOffset = Random.insideUnitCircle * glitchPositionAmount;
        textUI.text = CreateGlitchedText(stableText);
    }

    public void EndSharedGlitch()
    {
        glitchOffset = Vector2.zero;

        if (textUI != null && initialized)
            textUI.text = stableText;
    }

    private string CreateGlitchedText(string source)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(glitchCharacters))
            return source;

        char[] characters = source.ToCharArray();
        int appliedChanges = 0;
        int safety = characters.Length * 2;

        while (appliedChanges < glitchCharacterChanges && safety-- > 0)
        {
            int index = Random.Range(0, characters.Length);

            if (char.IsWhiteSpace(characters[index]))
                continue;

            characters[index] = glitchCharacters[
                Random.Range(0, glitchCharacters.Length)
            ];

            appliedChanges++;
        }

        return new string(characters);
    }

    private void RestoreBaseState()
    {
        if (!initialized || textUI == null || target == null)
            return;

        target.localPosition = baseLocalPosition;
        target.localScale = baseScale;
        target.localRotation = baseRotation;
        textUI.color = baseColor;
        textUI.text = stableText;

        messageAlpha = 1f;
        glitchOffset = Vector2.zero;
    }

    private void OnDisable()
    {
        if (initializationRoutine != null)
        {
            StopCoroutine(initializationRoutine);
            initializationRoutine = null;
        }

        StopEffectCoroutines();
        RestoreBaseState();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        glitchIntervalMin = Mathf.Max(0.5f, glitchIntervalMin);
        glitchIntervalMax = Mathf.Max(glitchIntervalMin, glitchIntervalMax);
        messageDuration = Mathf.Max(0.1f, messageDuration);
        crossFadeDuration = Mathf.Max(0.05f, crossFadeDuration);
    }
#endif
}
