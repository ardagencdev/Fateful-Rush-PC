using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance { get; private set; }

    [Header("Fade")]
    [SerializeField] private Image fadeImage;

    [SerializeField, Min(0f)]
    [Tooltip("Mevcut sahnenin siyaha kapanma süresi.")]
    private float fadeOutDuration = 0.4f;

    [SerializeField, Min(0f)]
    [Tooltip("Yeni sahnenin siyahtan açılma süresi.")]
    private float fadeInDuration = 0.5f;

    [SerializeField]
    [Tooltip("Fade animasyonunun easing eğrisi.")]
    private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(
        0f,
        0f,
        1f,
        1f
    );

    [Header("Scene Load Polish")]
    [SerializeField, Min(0f)]
    [Tooltip("Çok hızlı yüklenen sahnelerde siyah ekranın minimum kalma süresi. Flash hissini engeller.")]
    private float minimumBlackDuration = 0.08f;

    [SerializeField, Range(0, 5)]
    [Tooltip("Yeni sahne aktive olduktan sonra fade-in başlamadan önce kaç frame initialize olması beklenecek.")]
    private int postLoadFrameWait = 2;

    private bool isTransitioning;
    private bool isQuitting;

#if UNITY_EDITOR
    // Google Mobile Ads Editor mock'u da Unity UI olarak cizildigi icin
    // normalde short.MaxValue olan siyah transition canvas'i mock reklamin
    // ustunu kapatabiliyor. Reklam preview'i boyunca sadece Editor'da bir
    // sorting step asagi iner; Android/native build davranisi degismez.
    private bool editorAdPreviewMode;
    private const int EditorAdPreviewFadeSortingOrder = short.MaxValue - 1;
#endif

    public bool IsTransitioning => isTransitioning;

#if UNITY_EDITOR
    public void SetEditorAdPreviewMode(bool active)
    {
        editorAdPreviewMode = active;
        PrepareFadeCanvas();
    }
#endif

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceExists()
    {
        if (Instance != null)
            return;

        SceneTransition existing =
            FindAnyObjectByType<SceneTransition>();

        if (existing != null)
            return;

        GameObject transitionObject =
            new GameObject("SceneTransition");

        transitionObject.AddComponent<SceneTransition>();
    }

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        EnsureFadeImage();
        PrepareFadeCanvas();
        SetAlpha(0f);

        fadeImage.raycastTarget = false;
        fadeImage.gameObject.SetActive(false);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(
        Scene scene,
        LoadSceneMode mode)
    {
        PrepareFadeCanvas();

        if (isTransitioning && fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            fadeImage.raycastTarget = true;
            SetAlpha(1f);
        }
    }

    public void LoadSceneWithFade(string sceneName)
    {
        LoadSceneWithFade(sceneName, null);
    }

    /// <summary>
    /// Normal fade akisini korur; ekran tamamen siyaha ulastiginda istege
    /// bagli bir gate baslatir. Gate true donerse continueTransition callback'i
    /// gelene kadar ekran siyah kalir ve sahne yuklenmez. Reklam gibi tam ekran
    /// gecisler icin kullanilir. Gate false donerse yukleme hemen devam eder.
    /// </summary>
    public void LoadSceneWithFade(
        string sceneName,
        Func<Action, bool> blackScreenGate)
    {
        if (isTransitioning)
            return;

        if (string.IsNullOrWhiteSpace(sceneName) ||
            !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"[SceneTransition] Sahne bulunamadı veya " +
                $"Build Profiles'a eklenmemiş: '{sceneName}'",
                this
            );

            return;
        }

        BeginTransition();

        StartCoroutine(
            TransitionRoutine(
                sceneName,
                blackScreenGate
            )
        );
    }

    public void LoadSceneWithFade(int sceneIndex)
    {
        if (isTransitioning)
            return;

        if (sceneIndex < 0 ||
            sceneIndex >=
            SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogError(
                $"[SceneTransition] Geçersiz sahne indexi: " +
                $"{sceneIndex}",
                this
            );

            return;
        }

        BeginTransition();

        StartCoroutine(
            TransitionRoutine(sceneIndex)
        );
    }

    public void QuitGameWithFade()
    {
        if (isTransitioning)
            return;

        BeginTransition();
        StartCoroutine(QuitRoutine());
    }

    private void BeginTransition()
    {
        isTransitioning = true;

        EnsureFadeImage();
        PrepareFadeCanvas();

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            fadeImage.raycastTarget = true;
        }
    }

    private IEnumerator TransitionRoutine(
        string sceneName,
        Func<Action, bool> blackScreenGate)
    {
        Time.timeScale = 0f;

        yield return FadeOutEverything();

        // Buraya geldigimiz frame'de fade alpha tam 1: ekran tamamen siyah.
        // Reklam gate'i varsa reklami ancak simdi ac. Reklam kapanana kadar
        // mevcut sahne siyah perdenin arkasinda kalir; MainMenu henuz yuklenmez.
        if (blackScreenGate != null)
        {
            yield return WaitForBlackScreenGate(
                blackScreenGate
            );
        }

        float blackStartTime = Time.realtimeSinceStartup;

        AsyncOperation loadOperation =
            SceneManager.LoadSceneAsync(sceneName);

        if (loadOperation == null)
        {
            Debug.LogError(
                $"[SceneTransition] Sahne yükleme işlemi " +
                $"başlatılamadı: '{sceneName}'",
                this
            );

            yield return RecoverFromFailedLoad();
            yield break;
        }

        yield return CompleteAsyncLoad(
            loadOperation,
            blackStartTime
        );
    }

    private IEnumerator WaitForBlackScreenGate(
        Func<Action, bool> blackScreenGate)
    {
        bool gateCompleted = false;

        Action continueTransition = () =>
        {
            gateCompleted = true;
        };

        bool gateStarted = false;

        try
        {
            gateStarted =
                blackScreenGate.Invoke(
                    continueTransition
                );
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[SceneTransition] Siyah ekran gecis aksiyonu " +
                "baslatilamadi; sahne gecisi devam ediyor. " +
                exception.Message,
                this
            );
        }

        if (!gateStarted)
            yield break;

        while (!gateCompleted)
        {
            // Reklam callback'i gelene kadar siyah perdeyi ve pause durumunu
            // garanti et. Unscaled UI/reklam callback'leri calismaya devam eder.
            Time.timeScale = 0f;

            PrepareFadeCanvas();

            if (fadeImage != null)
            {
                fadeImage.gameObject.SetActive(true);
                fadeImage.raycastTarget = true;
                SetAlpha(1f);
            }

            yield return null;
        }

        // AdManager reklam kapanisinda kendi onceki timeScale degerini geri
        // yukler. Gate'e girerken o deger 0 oldugu icin burada da siyah gecis
        // pause'unu net tutuyoruz.
        Time.timeScale = 0f;
    }

    private IEnumerator TransitionRoutine(
        int sceneIndex)
    {
        Time.timeScale = 0f;

        yield return FadeOutEverything();

        float blackStartTime = Time.realtimeSinceStartup;

        AsyncOperation loadOperation =
            SceneManager.LoadSceneAsync(sceneIndex);

        if (loadOperation == null)
        {
            Debug.LogError(
                $"[SceneTransition] Sahne yükleme işlemi " +
                $"başlatılamadı. Index: {sceneIndex}",
                this
            );

            yield return RecoverFromFailedLoad();
            yield break;
        }

        yield return CompleteAsyncLoad(
            loadOperation,
            blackStartTime
        );
    }

    private IEnumerator CompleteAsyncLoad(
        AsyncOperation loadOperation,
        float blackStartTime)
    {
        // Sahneyi arka planda %90'a kadar hazırla ama ekran siyah olmadan
        // aktive etme. Böylece yeni sahnenin tek frame parlaması engellenir.
        loadOperation.allowSceneActivation = false;

        while (loadOperation.progress < 0.9f)
        {
            Time.timeScale = 0f;
            yield return null;
        }

        float blackElapsed =
            Time.realtimeSinceStartup - blackStartTime;

        float remainingBlackTime =
            Mathf.Max(
                0f,
                minimumBlackDuration - blackElapsed
            );

        if (remainingBlackTime > 0f)
        {
            yield return new WaitForSecondsRealtime(
                remainingBlackTime
            );
        }

        loadOperation.allowSceneActivation = true;

        while (!loadOperation.isDone)
        {
            Time.timeScale = 0f;
            yield return null;
        }

        yield return FinishTransition();
    }

    private IEnumerator FinishTransition()
    {
        PrepareFadeCanvas();

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            fadeImage.raycastTarget = true;
            SetAlpha(1f);
        }

        int framesToWait = Mathf.Max(
            0,
            postLoadFrameWait
        );

        for (int i = 0; i < framesToWait; i++)
        {
            // Yeni sahnedeki Awake / OnEnable / Start işlemlerine görünmeden
            // birkaç frame alan bırak.
            Time.timeScale = 0f;
            yield return null;
        }

        Time.timeScale = 1f;

        yield return FadeIn();

        isTransitioning = false;
    }

    private IEnumerator RecoverFromFailedLoad()
    {
        Time.timeScale = 1f;

        yield return FadeIn();

        isTransitioning = false;
    }

    private IEnumerator QuitRoutine()
    {
        isQuitting = true;
        Time.timeScale = 0f;

        yield return FadeOutEverything();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator FadeOutEverything()
    {
        Coroutine musicFade =
            StartCoroutine(FadeOutMusic());

        Coroutine screenFade =
            StartCoroutine(FadeOut());

        yield return musicFade;
        yield return screenFade;
    }

    private IEnumerator FadeOutMusic()
    {
        MenuMusicApply menuMusic =
            FindAnyObjectByType<MenuMusicApply>();

        if (menuMusic != null)
        {
            menuMusic.FadeOutMusic();

            float duration = Mathf.Max(
                0f,
                menuMusic.FadeOutDuration
            );

            if (duration > 0f)
            {
                yield return new WaitForSecondsRealtime(
                    duration
                );
            }

            yield break;
        }

        GameplayMusicFade gameplayMusic =
            FindAnyObjectByType<GameplayMusicFade>();

        if (gameplayMusic != null)
        {
            gameplayMusic.FadeOut();

            float duration = Mathf.Max(
                0f,
                gameplayMusic.FadeOutDuration
            );

            if (duration > 0f)
            {
                yield return new WaitForSecondsRealtime(
                    duration
                );
            }
        }
    }

    private IEnumerator FadeOut()
    {
        PrepareFadeCanvas();

        fadeImage.gameObject.SetActive(true);
        fadeImage.raycastTarget = true;

        yield return Fade(
            fadeImage.color.a,
            1f,
            fadeOutDuration
        );
    }

    private IEnumerator FadeIn()
    {
        PrepareFadeCanvas();

        fadeImage.gameObject.SetActive(true);
        fadeImage.raycastTarget = true;

        SetAlpha(1f);

        yield return Fade(
            1f,
            0f,
            fadeInDuration
        );

        SetAlpha(0f);

        fadeImage.raycastTarget = false;
        fadeImage.gameObject.SetActive(false);
    }

    private IEnumerator Fade(
        float from,
        float to,
        float duration)
    {
        if (duration <= 0f)
        {
            SetAlpha(to);
            yield break;
        }

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                timer / duration
            );

            float easedProgress =
                EvaluateFadeCurve(progress);

            SetAlpha(
                Mathf.Lerp(
                    from,
                    to,
                    easedProgress
                )
            );

            yield return null;
        }

        SetAlpha(to);
    }

    private int GetFadeSortingOrder()
    {
#if UNITY_EDITOR
        if (editorAdPreviewMode)
            return EditorAdPreviewFadeSortingOrder;
#endif

        return short.MaxValue;
    }

    private float EvaluateFadeCurve(float progress)
    {
        progress = Mathf.Clamp01(progress);

        if (fadeCurve == null ||
            fadeCurve.length == 0)
        {
            return progress * progress * progress *
                   (progress *
                    (progress * 6f - 15f) +
                    10f);
        }

        return Mathf.Clamp01(
            fadeCurve.Evaluate(progress)
        );
    }

    private void EnsureFadeImage()
    {
        if (fadeImage == null)
        {
            CreateFadeImage();
        }
    }

    private void PrepareFadeCanvas()
    {
        EnsureFadeImage();

        if (fadeImage == null)
            return;

        Canvas canvas =
            fadeImage.GetComponentInParent<Canvas>();

        if (canvas != null)
        {
            canvas.renderMode =
                RenderMode.ScreenSpaceOverlay;

            canvas.overrideSorting = true;
            canvas.sortingOrder =
                GetFadeSortingOrder();

            canvas.targetDisplay = 0;
            canvas.gameObject.SetActive(true);
        }

        RectTransform rectTransform =
            fadeImage.rectTransform;

        rectTransform.anchorMin =
            Vector2.zero;

        rectTransform.anchorMax =
            Vector2.one;

        rectTransform.pivot =
            new Vector2(0.5f, 0.5f);

        rectTransform.offsetMin =
            Vector2.zero;

        rectTransform.offsetMax =
            Vector2.zero;

        Color color = fadeImage.color;

        color.r = 0f;
        color.g = 0f;
        color.b = 0f;

        fadeImage.color = color;

        fadeImage.transform.SetAsLastSibling();
    }

    private void SetAlpha(float alpha)
    {
        if (fadeImage == null)
            return;

        Color color = fadeImage.color;

        color.a = Mathf.Clamp01(alpha);

        fadeImage.color = color;
    }

    private void CreateFadeImage()
    {
        GameObject canvasObject =
            new GameObject(
                "TransitionCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)
            );

        canvasObject.transform.SetParent(
            transform,
            false
        );

        Canvas canvas =
            canvasObject.GetComponent<Canvas>();

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        canvas.overrideSorting = true;
        canvas.sortingOrder =
            GetFadeSortingOrder();

        canvas.targetDisplay = 0;

        CanvasScaler scaler =
            canvasObject.GetComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(1080f, 1920f);

        scaler.matchWidthOrHeight = 0.5f;

        GameObject imageObject =
            new GameObject(
                "FadeImage",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

        imageObject.transform.SetParent(
            canvasObject.transform,
            false
        );

        RectTransform rectTransform =
            imageObject.GetComponent<RectTransform>();

        rectTransform.anchorMin =
            Vector2.zero;

        rectTransform.anchorMax =
            Vector2.one;

        rectTransform.pivot =
            new Vector2(0.5f, 0.5f);

        rectTransform.offsetMin =
            Vector2.zero;

        rectTransform.offsetMax =
            Vector2.zero;

        fadeImage =
            imageObject.GetComponent<Image>();

        fadeImage.sprite = null;
        fadeImage.type =
            Image.Type.Simple;

        fadeImage.color =
            new Color(0f, 0f, 0f, 0f);

        fadeImage.raycastTarget = false;
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        SceneManager.sceneLoaded -=
            OnSceneLoaded;

        Instance = null;

        if (!isQuitting)
        {
            Time.timeScale = 1f;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        fadeOutDuration = Mathf.Max(
            0f,
            fadeOutDuration
        );

        fadeInDuration = Mathf.Max(
            0f,
            fadeInDuration
        );

        minimumBlackDuration = Mathf.Max(
            0f,
            minimumBlackDuration
        );

        postLoadFrameWait = Mathf.Clamp(
            postLoadFrameWait,
            0,
            5
        );
    }
#endif
}
