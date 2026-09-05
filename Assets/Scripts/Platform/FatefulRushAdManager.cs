using System;
using System.Collections;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Fateful Rush reklam akisini gameplay'den izole tutar.
/// - Her gameplay attempt basladiginda sayar.
/// - 5-10 attempt araliginda reklam hakki olusturur.
/// - Attempt reklami sadece gameplay -> MainMenu gecisinde denenir.
/// - MainMenu'de aktif gecirilen toplam 5 dakikada bir reklam dener.
/// - Tum sayaçlar PlayerPrefs ile kalicidir.
/// - Reklam/consent SDK hatalari gameplay veya scene gecisini asla bloklamaz.
/// </summary>
public sealed class FatefulRushAdManager : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";

    private const string AndroidTestInterstitialId =
        "ca-app-pub-3940256099942544/1033173712";

    private const string AndroidProductionInterstitialId =
        "ca-app-pub-4850318886881398/8213391263";

#if UNITY_EDITOR
    // Editor testinde reklami hizlica dogrulamak icin her attempt sonrasi hak olustur.
    // Bu blok build'e GIRMEZ; Android/Google Play Games on PC build'leri 5-10 RNG kullanir.
    private const int MinimumAttemptsPerAd = 1;
    private const int MaximumAttemptsPerAd = 1;
#else
    private const int MinimumAttemptsPerAd = 5;
    private const int MaximumAttemptsPerAd = 10;
#endif

    // Kullanici istegi: MainMenu sahnesinde, panel fark etmeksizin,
    // aktif gecirilen toplam 5 dakikada bir reklam hakki.
    private const float MainMenuAdIntervalSeconds = 5f * 60f;

    private const float ProgressSaveIntervalSeconds = 30f;
    private const float AdsStartupDelaySeconds = 2.0f;
    private const float FailedLoadRetrySeconds = 30f;
    private const float LoadedAdRefreshSeconds = 55f * 60f;

    private const string AttemptsKey = "FR_Ads_Attempts";
    private const string AttemptTargetKey = "FR_Ads_AttemptTarget";
    private const string MainMenuSecondsKey = "FR_Ads_MainMenuSeconds";

    private static FatefulRushAdManager instance;

    private InterstitialAd loadedInterstitial;
    private InterstitialAd activeInterstitial;

    private int attemptCount;
    private int attemptTarget;
    private float mainMenuActiveSeconds;

    private bool previousGameplayStarted;
    private bool consentFlowActive;
    private bool sdkInitializationStarted;
    private bool sdkInitialized;
    private bool adLoadInProgress;
    private bool adShowInProgress;
    private bool adPauseActive;

    private float adPausePreviousTimeScale = 1f;
    private bool adPausePreviousAudioListenerPause;
    private Action activeAdFinishedCallback;

    private float nextAllowedLoadRealtime;
    private float loadedAdRealtime;
    private float nextProgressSaveRealtime;

#if !UNITY_EDITOR
    private float adsStartupNotBeforeRealtime;
    private bool adsStartupPending;
#endif

#if UNITY_EDITOR
    // Editor mock reklam bazen transition canvas'inin arkasinda kalabiliyor
    // veya tek frame gorunup kapanabiliyor. Bu preview yalnizca Editor'da
    // deterministic bir tam-ekran reklam testi gosterir; build'e GIRMEZ.
    private const float EditorAdPreviewMinimumSeconds = 1.25f;
    private GameObject editorAdPreviewRoot;
    private float editorAdPreviewShownRealtime;
    private bool editorFinishDelayActive;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    /// <summary>
    /// Gameplay'den MainMenu'ye cikarken cagrilir.
    /// Reklam hazir/uygun degilse false doner. Callback gerektirmeyen
    /// eski/harici cagrilar icin bu overload korunur.
    /// </summary>
    public static bool TryShowAttemptAdBeforeReturningToMenu()
    {
        return TryShowAttemptAdBeforeReturningToMenu(null);
    }

    /// <summary>
    /// Gameplay -> MainMenu gecisinde reklam hazirsa once reklami acar.
    /// Reklam kapandiginda/fail oldugunda onFinished cagrilir; caller ancak
    /// o noktada scene gecisini devam ettirebilir. Reklam acilamazsa false
    /// doner ve callback cagrilmaz.
    /// </summary>
    public static bool TryShowAttemptAdBeforeReturningToMenu(
        Action onFinished)
    {
        try
        {
            FatefulRushAdManager manager = EnsureInstance();

            return manager != null &&
                   manager.TryShowAttemptAdInternal(onFinished);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[Ads] Main Menu gecis reklami guvenli sekilde atlandi. " +
                exception.Message
            );

            return false;
        }
    }

    /// <summary>
    /// Privacy options formunu acmayi dener. Eski/harici OnClick baglantilari
    /// bozulmasin diye bu wrapper korunur.
    /// </summary>
    public static void ShowPrivacyOptions()
    {
        TryShowPrivacyOptions();
    }

    /// <summary>
    /// UMP privacy options gerekiyorsa formu acmayi dener.
    /// - Form baslatilamazsa false doner; caller kendi fallback'ini calistirir.
    /// - Form asenkron hata ile donerse onFailure guvenli sekilde cagrilir.
    /// - Form basariyla kapanirsa onClosed cagrilir.
    /// Reklam/UMP hatalari UI akisini kilitleyemez.
    /// </summary>
    public static bool TryShowPrivacyOptions(
        Action onFailure = null,
        Action onClosed = null)
    {
        if (!IsPrivacyOptionsRequired)
            return false;

        try
        {
            ConsentForm.ShowPrivacyOptionsForm(
                error =>
                {
                    RunOnUnityThread(
                        () =>
                        {
                            if (error != null)
                            {
                                Debug.LogWarning(
                                    "[Ads] Privacy options form acilamadi: " +
                                    error.Message
                                );

                                InvokeSafely(onFailure);
                            }

                            InvokeSafely(onClosed);
                        }
                    );
                }
            );

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[Ads] Privacy options form guvenli sekilde atlandi: " +
                exception.Message
            );

            return false;
        }
    }

    public static bool IsPrivacyOptionsRequired
    {
        get
        {
            try
            {
                return ConsentInformation.PrivacyOptionsRequirementStatus ==
                       PrivacyOptionsRequirementStatus.Required;
            }
            catch
            {
                return false;
            }
        }
    }

    private static FatefulRushAdManager EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = FindAnyObjectByType<FatefulRushAdManager>();

        if (instance != null)
            return instance;

        GameObject managerObject =
            new GameObject("FatefulRushAdManager");

        instance =
            managerObject.AddComponent<FatefulRushAdManager>();

        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // UMP callbacks arrive before MobileAds.Initialize(). Our callback flow
        // marshals work through MobileAdsEventExecutor, so the executor must
        // already exist on the Unity main thread. MobileAds.Initialize() also
        // initializes it later, but that is too late for the consent callbacks.
        try
        {
            GoogleMobileAds.Common.MobileAdsEventExecutor.Initialize();
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[Ads] MobileAdsEventExecutor baslatilamadi: " +
                exception.Message
            );
        }

        LoadProgress();

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        if (!IsAdsRuntimeSupported())
            return;

#if UNITY_EDITOR
        // Editor mock ads stay immediate for deterministic testing.
        TryInitializeAdsSafely(true);
#else
        // On GPG PC, do not compete with first-scene shader warm-up / gameplay
        // startup. Ads are initialized only while the player is safely in the
        // Main Menu, after the one-time shader warm-up has completed.
        adsStartupPending = true;
        adsStartupNotBeforeRealtime =
            Time.realtimeSinceStartup + AdsStartupDelaySeconds;
#endif
    }

    private void Update()
    {
        TryStartAdsWhenSafe();
        TrackGameplayAttempt();
        TrackMainMenuTime();

        // Network/SDK maintenance and disk flushes are intentionally kept out
        // of active gameplay. They can safely happen in MainMenu instead.
        if (IsSafeForAdBackgroundWork())
        {
            RefreshExpiredAdIfNeeded();
            RetryAdLoadIfNeeded();
            SaveProgressPeriodically();
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            SaveProgress();
    }

    private void OnApplicationQuit()
    {
        SaveProgress();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        EndAdPause();
        activeAdFinishedCallback = null;

#if UNITY_EDITOR
        EndEditorAdPreview();
#endif

        DestroyAdSafely(ref loadedInterstitial);
        DestroyAdSafely(ref activeInterstitial);

        if (instance == this)
            instance = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        previousGameplayStarted = false;

        if (scene.name == MainMenuSceneName)
        {
            // MainMenu is a safe checkpoint for the ad pacing counters.
            SaveProgress();
            TryStartAdsWhenSafe();

            // MainMenu timeri panel degisikliklerinden etkilenmez.
            // Bir onceki session'dan kalan sure de PlayerPrefs'ten devam eder.
            TryShowMainMenuTimedAdIfDue();
        }
    }

    private void TrackGameplayAttempt()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        bool gameplayStarted =
            activeScene.name != MainMenuSceneName &&
            GameStateManager.IsGameplayStarted;

        if (gameplayStarted && !previousGameplayStarted)
            RegisterGameplayAttempt();

        previousGameplayStarted = gameplayStarted;
    }

    private void RegisterGameplayAttempt()
    {
        attemptCount = Mathf.Max(0, attemptCount) + 1;

        PlayerPrefs.SetInt(
            AttemptsKey,
            attemptCount
        );

        // Do not force a disk flush at the exact moment gameplay starts.
        // PlayerPrefs.SetInt updates memory immediately; persistence happens at
        // result/menu/pause checkpoints instead, avoiding a possible frame hitch.

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[Ads TEST] Gameplay attempt: {attemptCount}/{attemptTarget}"
        );
#endif
    }

    private void TrackMainMenuTime()
    {
        if (SceneManager.GetActiveScene().name != MainMenuSceneName)
            return;

        // Consent/reklam ekrandayken veya uygulama focus disindayken
        // kullanicinin menu suresini ilerletme.
        if (!Application.isFocused ||
            consentFlowActive ||
            adShowInProgress)
        {
            return;
        }

        mainMenuActiveSeconds += Time.unscaledDeltaTime;

        if (mainMenuActiveSeconds >= MainMenuAdIntervalSeconds)
            TryShowMainMenuTimedAdIfDue();
    }

    private bool TryShowAttemptAdInternal(Action onFinished)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[Ads TEST] MainMenu ad check. Attempts={attemptCount}/{attemptTarget}, " +
            $"SDK={sdkInitialized}, Loaded={(loadedInterstitial != null)}, " +
            $"Showing={adShowInProgress}"
        );
#endif

        if (attemptCount < attemptTarget)
            return false;

        return TryShowLoadedInterstitial(onFinished);
    }

    private void TryShowMainMenuTimedAdIfDue()
    {
        if (mainMenuActiveSeconds < MainMenuAdIntervalSeconds)
            return;

        // Kullanici tam o anda bir scene gecisi baslattiysa reklam yeni
        // gameplay sahnesinin ustune tasmasin; hak kaybolmaz, sonraki uygun
        // MainMenu aninda tekrar denenir.
        if (SceneTransition.Instance != null &&
            SceneTransition.Instance.IsTransitioning)
        {
            return;
        }

        // Attempt reklami sadece gameplay -> menu gecis noktasina aittir.
        // Burada yalnizca 5 dakikalik MainMenu hakki reklam acar.
        TryShowLoadedInterstitial();
    }

    private bool TryShowLoadedInterstitial(Action onFinished = null)
    {
        if (!IsAdsRuntimeSupported() ||
            consentFlowActive ||
            !sdkInitialized ||
            adShowInProgress)
        {
            return false;
        }

        InterstitialAd ad = loadedInterstitial;

        if (ad == null)
        {
            LoadInterstitialSafely();
            return false;
        }

        bool canShow;

        try
        {
            canShow = ad.CanShowAd();
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[Ads] CanShowAd kontrolu basarisiz; reklam atlandi: " +
                exception.Message
            );

            DestroyAdSafely(ref loadedInterstitial);
            ScheduleLoadRetry();
            return false;
        }

        if (!canShow)
        {
            DestroyAdSafely(ref loadedInterstitial);
            LoadInterstitialSafely();
            return false;
        }

        // Reklam ekrandayken oyun/menunun arkada ilerlememesi icin
        // Show() isteginden hemen once global oyun akisini dondur.
        // Gameplay -> MainMenu yolunda caller, onFinished callback'i gelene
        // kadar scene gecisini de baslatmaz.
        loadedInterstitial = null;
        activeInterstitial = ad;
        activeAdFinishedCallback = onFinished;
        adShowInProgress = true;
        BeginAdPause();

#if UNITY_EDITOR
        BeginEditorAdPreview();
#endif

        try
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[Ads TEST] Interstitial Show() requested.");
#endif

            ad.Show();

            // Show() exception atmadan kabul edildiyse hakki tuket.
            // Callback kaybolsa dahi ikinci bir reklam ust uste cikmasin.
            ConsumeAdOpportunity();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[Ads] Reklam acilamadi; oyun akisina devam ediliyor: " +
                exception.Message
            );

            adShowInProgress = false;
            activeInterstitial = null;
            activeAdFinishedCallback = null;
#if UNITY_EDITOR
            EndEditorAdPreview();
#endif
            EndAdPause();
            DestroySpecificAdSafely(ad);
            ScheduleLoadRetry();
            return false;
        }
    }

    private void ConsumeAdOpportunity()
    {
        attemptCount = 0;
        attemptTarget = RollNextAttemptTarget();
        mainMenuActiveSeconds = 0f;

        PlayerPrefs.SetInt(
            AttemptsKey,
            attemptCount
        );

        PlayerPrefs.SetInt(
            AttemptTargetKey,
            attemptTarget
        );

        PlayerPrefs.SetFloat(
            MainMenuSecondsKey,
            mainMenuActiveSeconds
        );

        PlayerPrefs.Save();
    }

    private void TryStartAdsWhenSafe()
    {
#if UNITY_EDITOR
        return;
#else
        if (!adsStartupPending ||
            sdkInitialized ||
            sdkInitializationStarted ||
            consentFlowActive ||
            Time.realtimeSinceStartup < adsStartupNotBeforeRealtime ||
            !GpgPcShaderWarmup.IsComplete ||
            !IsSafeForAdBackgroundWork())
        {
            return;
        }

        adsStartupPending = false;
        BeginConsentFlowSafely();
#endif
    }

    private bool IsSafeForAdBackgroundWork()
    {
        if (!Application.isFocused || adShowInProgress)
            return false;

        Scene activeScene = SceneManager.GetActiveScene();

        return activeScene.IsValid() &&
               activeScene.name == MainMenuSceneName &&
               !GameStateManager.IsGameplayStarted;
    }

    private void BeginConsentFlowSafely()
    {
        if (consentFlowActive || sdkInitializationStarted)
            return;

        consentFlowActive = true;

        try
        {
            ConsentRequestParameters requestParameters =
                new ConsentRequestParameters();

            ConsentInformation.Update(
                requestParameters,
                error =>
                {
                    RunOnUnityThread(
                        () => HandleConsentInfoUpdated(error)
                    );
                }
            );
        }
        catch (Exception exception)
        {
            consentFlowActive = false;

            Debug.LogWarning(
                "[Ads] Consent update baslatilamadi; reklam sistemi " +
                "oyunu etkilemeden devre disi kalabilir: " +
                exception.Message
            );

            TryInitializeAdsSafely();
        }
    }

    private void HandleConsentInfoUpdated(FormError error)
    {
        if (this == null)
            return;

        if (error != null)
        {
            consentFlowActive = false;

            Debug.LogWarning(
                "[Ads] Consent bilgisi guncellenemedi: " +
                error.Message
            );

            // Onceki session'dan gecerli consent varsa SDK yine baslayabilir.
            TryInitializeAdsSafely();
            return;
        }

        try
        {
            ConsentForm.LoadAndShowConsentFormIfRequired(
                formError =>
                {
                    RunOnUnityThread(
                        () => HandleConsentFormFinished(formError)
                    );
                }
            );
        }
        catch (Exception exception)
        {
            consentFlowActive = false;

            Debug.LogWarning(
                "[Ads] Consent form akisi baslatilamadi: " +
                exception.Message
            );

            TryInitializeAdsSafely();
        }
    }

    private void HandleConsentFormFinished(FormError error)
    {
        if (this == null)
            return;

        consentFlowActive = false;

        if (error != null)
        {
            Debug.LogWarning(
                "[Ads] Consent form tamamlanamadi: " +
                error.Message
            );
        }

        TryInitializeAdsSafely();
    }

    private void TryInitializeAdsSafely(bool skipConsentCheck = false)
    {
#if !UNITY_EDITOR
        if (!IsSafeForAdBackgroundWork())
        {
            adsStartupPending = true;
            return;
        }
#endif

        if (!IsAdsRuntimeSupported() ||
            sdkInitialized ||
            sdkInitializationStarted)
        {
            return;
        }

        if (!skipConsentCheck)
        {
            bool canRequestAds;

            try
            {
                canRequestAds =
                    ConsentInformation.CanRequestAds();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[Ads] Consent durumu okunamadi; reklamlar atlandi: " +
                    exception.Message
                );

                return;
            }

            if (!canRequestAds)
            {
                Debug.LogWarning(
                    "[Ads] UMP CanRequestAds=false; Mobile Ads SDK baslatilmadi."
                );
                return;
            }
        }

        sdkInitializationStarted = true;

        try
        {
            MobileAds.Initialize(
                initializationStatus =>
                {
                    RunOnUnityThread(
                        () => HandleAdsInitialized(
                            initializationStatus
                        )
                    );
                }
            );
        }
        catch (Exception exception)
        {
            sdkInitializationStarted = false;

            Debug.LogWarning(
                "[Ads] Mobile Ads SDK baslatilamadi; oyun normal devam edecek: " +
                exception.Message
            );
        }
    }

    private void HandleAdsInitialized(
        InitializationStatus initializationStatus)
    {
        if (this == null)
            return;

        sdkInitializationStarted = false;

        if (initializationStatus == null)
        {
            Debug.LogWarning(
                "[Ads] Mobile Ads SDK initialization sonucu bos geldi."
            );

            return;
        }

        sdkInitialized = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[Ads TEST] Google Mobile Ads initialized.");
#endif

        LoadInterstitialSafely();
    }

    private void LoadInterstitialSafely()
    {
#if !UNITY_EDITOR
        if (!IsSafeForAdBackgroundWork())
            return;
#endif

        if (!IsAdsRuntimeSupported() ||
            !sdkInitialized ||
            adLoadInProgress ||
            adShowInProgress ||
            loadedInterstitial != null ||
            Time.realtimeSinceStartup < nextAllowedLoadRealtime)
        {
            return;
        }

        adLoadInProgress = true;

        try
        {
            AdRequest request = new AdRequest();

            InterstitialAd.Load(
                GetInterstitialAdUnitId(),
                request,
                (ad, error) =>
                {
                    RunOnUnityThread(
                        () => HandleInterstitialLoaded(
                            ad,
                            error
                        )
                    );
                }
            );
        }
        catch (Exception exception)
        {
            adLoadInProgress = false;
            ScheduleLoadRetry();

            Debug.LogWarning(
                "[Ads] Interstitial load baslatilamadi: " +
                exception.Message
            );
        }
    }

    private void HandleInterstitialLoaded(
        InterstitialAd ad,
        LoadAdError error)
    {
        if (this == null)
        {
            DestroySpecificAdSafely(ad);
            return;
        }

        adLoadInProgress = false;

        if (error != null || ad == null)
        {
            if (error != null)
            {
                Debug.LogWarning(
                    "[Ads] Interstitial yuklenemedi: " + error
                );
            }
            else
            {
                Debug.LogWarning(
                    "[Ads] Interstitial load callback null reklam dondurdu."
                );
            }

            DestroySpecificAdSafely(ad);
            ScheduleLoadRetry();
            return;
        }

        DestroyAdSafely(ref loadedInterstitial);

        loadedInterstitial = ad;
        loadedAdRealtime = Time.realtimeSinceStartup;
        nextAllowedLoadRealtime = 0f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[Ads TEST] Interstitial loaded and ready.");
#endif

        RegisterInterstitialCallbacks(ad);

        // 5 dakika daha once dolduysa ve reklam ancak simdi yuklendiyse
        // MainMenu'de guvenli sekilde hemen denenebilir.
        if (SceneManager.GetActiveScene().name == MainMenuSceneName)
            TryShowMainMenuTimedAdIfDue();
    }

    private void RegisterInterstitialCallbacks(InterstitialAd ad)
    {
        ad.OnAdFullScreenContentOpened +=
            () =>
            {
                RunOnUnityThread(
                    () =>
                    {
                        if (this != null)
                        {
                            adShowInProgress = true;
                            BeginAdPause();
                        }
                    }
                );
            };

        ad.OnAdFullScreenContentClosed +=
            () =>
            {
                RunOnUnityThread(
                    () => FinishInterstitial(ad)
                );
            };

        ad.OnAdFullScreenContentFailed +=
            error =>
            {
                RunOnUnityThread(
                    () => FinishInterstitial(ad)
                );
            };
    }

    private void FinishInterstitial(InterstitialAd ad)
    {
        if (this == null)
        {
            DestroySpecificAdSafely(ad);
            return;
        }

#if UNITY_EDITOR
        // Google'in Editor placeholder'i bazi surumlerde tek frame icinde
        // kapanabiliyor. Scene transition'in hemen devam edip preview'i
        // yutmamasi icin Editor'da minimum gorunme suresini garanti et.
        if (editorFinishDelayActive)
            return;

        if (editorAdPreviewRoot != null &&
            editorAdPreviewRoot.activeSelf)
        {
            float elapsed =
                Time.realtimeSinceStartup - editorAdPreviewShownRealtime;

            float remaining =
                EditorAdPreviewMinimumSeconds - elapsed;

            if (remaining > 0f)
            {
                editorFinishDelayActive = true;
                StartCoroutine(
                    FinishInterstitialAfterEditorPreviewDelay(
                        ad,
                        remaining
                    )
                );
                return;
            }
        }

        EndEditorAdPreview();
#endif

        if (activeInterstitial == ad)
            activeInterstitial = null;

        adShowInProgress = false;

        Action finishedCallback = activeAdFinishedCallback;
        activeAdFinishedCallback = null;

        // Once oyunu/sesi geri getir, sonra gameplay -> MainMenu gibi
        // bekleyen akisin devam etmesine izin ver.
        EndAdPause();
        DestroySpecificAdSafely(ad);

        // Full-screen reklam tek kullanimliktir; sonrakini preload et.
        nextAllowedLoadRealtime = Time.realtimeSinceStartup + 0.5f;

        InvokeSafely(finishedCallback);
    }

#if UNITY_EDITOR
    private IEnumerator FinishInterstitialAfterEditorPreviewDelay(
        InterstitialAd ad,
        float delay)
    {
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        editorFinishDelayActive = false;
        FinishInterstitial(ad);
    }

    private void BeginEditorAdPreview()
    {
        EnsureEditorAdPreviewOverlay();

        editorAdPreviewShownRealtime =
            Time.realtimeSinceStartup;

        editorFinishDelayActive = false;

        if (SceneTransition.Instance != null)
            SceneTransition.Instance.SetEditorAdPreviewMode(true);

        if (editorAdPreviewRoot != null)
        {
            editorAdPreviewRoot.SetActive(true);
            editorAdPreviewRoot.transform.SetAsLastSibling();
        }
    }

    private void EndEditorAdPreview()
    {
        editorFinishDelayActive = false;

        if (editorAdPreviewRoot != null)
            editorAdPreviewRoot.SetActive(false);

        if (SceneTransition.Instance != null)
            SceneTransition.Instance.SetEditorAdPreviewMode(false);
    }

    private void EnsureEditorAdPreviewOverlay()
    {
        if (editorAdPreviewRoot != null)
            return;

        editorAdPreviewRoot =
            new GameObject(
                "EditorAdPreviewCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)
            );

        editorAdPreviewRoot.transform.SetParent(
            transform,
            false
        );

        Canvas canvas =
            editorAdPreviewRoot.GetComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue;
        canvas.targetDisplay = 0;

        CanvasScaler scaler =
            editorAdPreviewRoot.GetComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster =
            editorAdPreviewRoot.GetComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        GameObject panelObject =
            new GameObject(
                "EditorTestInterstitial",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

        panelObject.transform.SetParent(
            editorAdPreviewRoot.transform,
            false
        );

        RectTransform panelRect =
            panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(760f, 500f);

        Image panelImage =
            panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        panelImage.raycastTarget = false;

        Outline outline = panelObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        outline.effectDistance = new Vector2(2f, -2f);

        CreateEditorPreviewText(
            panelObject.transform,
            "TEST AD",
            48,
            new Vector2(0f, 145f),
            new Vector2(680f, 80f),
            FontStyle.Bold
        );

        CreateEditorPreviewText(
            panelObject.transform,
            "GOOGLE MOBILE ADS - EDITOR PREVIEW",
            24,
            new Vector2(0f, 75f),
            new Vector2(680f, 50f),
            FontStyle.Normal
        );

        CreateEditorPreviewText(
            panelObject.transform,
            "Interstitial is being shown here.\nAndroid build uses the native full-screen ad.",
            28,
            new Vector2(0f, -20f),
            new Vector2(660f, 120f),
            FontStyle.Normal
        );

        CreateEditorPreviewText(
            panelObject.transform,
            "EDITOR ONLY",
            22,
            new Vector2(0f, -165f),
            new Vector2(680f, 50f),
            FontStyle.Bold
        );

        editorAdPreviewRoot.SetActive(false);
    }

    private static void CreateEditorPreviewText(
        Transform parent,
        string value,
        int fontSize,
        Vector2 anchoredPosition,
        Vector2 size,
        FontStyle fontStyle)
    {
        GameObject textObject =
            new GameObject(
                "Text",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text)
            );

        textObject.transform.SetParent(parent, false);

        RectTransform rect =
            textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Text text = textObject.GetComponent<Text>();
        text.text = value;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        try
        {
            text.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf"
            );
        }
        catch
        {
            // Unity default font fallback'i yeterli; Editor testini bloklama.
        }
    }
#endif

    private void BeginAdPause()
    {
        if (adPauseActive)
            return;

        adPauseActive = true;
        adPausePreviousTimeScale = Time.timeScale;
        adPausePreviousAudioListenerPause = AudioListener.pause;

        Time.timeScale = 0f;
        AudioListener.pause = true;
    }

    private void EndAdPause()
    {
        if (!adPauseActive)
            return;

        Time.timeScale = adPausePreviousTimeScale;
        AudioListener.pause = adPausePreviousAudioListenerPause;
        adPauseActive = false;
    }


    private void RefreshExpiredAdIfNeeded()
    {
        if (loadedInterstitial == null)
            return;

        if (Time.realtimeSinceStartup - loadedAdRealtime <
            LoadedAdRefreshSeconds)
        {
            return;
        }

        DestroyAdSafely(ref loadedInterstitial);
        nextAllowedLoadRealtime = 0f;
        LoadInterstitialSafely();
    }

    private void RetryAdLoadIfNeeded()
    {
        if (!sdkInitialized ||
            adShowInProgress ||
            adLoadInProgress ||
            loadedInterstitial != null ||
            Time.realtimeSinceStartup < nextAllowedLoadRealtime)
        {
            return;
        }

        LoadInterstitialSafely();
    }

    private void ScheduleLoadRetry()
    {
        nextAllowedLoadRealtime =
            Time.realtimeSinceStartup +
            FailedLoadRetrySeconds;
    }

    private void LoadProgress()
    {
        attemptCount = Mathf.Max(
            0,
            PlayerPrefs.GetInt(
                AttemptsKey,
                0
            )
        );

        attemptTarget =
            PlayerPrefs.GetInt(
                AttemptTargetKey,
                0
            );

        if (attemptTarget < MinimumAttemptsPerAd ||
            attemptTarget > MaximumAttemptsPerAd)
        {
            attemptTarget = RollNextAttemptTarget();

            PlayerPrefs.SetInt(
                AttemptTargetKey,
                attemptTarget
            );
        }

        mainMenuActiveSeconds = Mathf.Max(
            0f,
            PlayerPrefs.GetFloat(
                MainMenuSecondsKey,
                0f
            )
        );

        nextProgressSaveRealtime =
            Time.realtimeSinceStartup +
            ProgressSaveIntervalSeconds;
    }

    private void SaveProgressPeriodically()
    {
        if (Time.realtimeSinceStartup < nextProgressSaveRealtime)
            return;

        nextProgressSaveRealtime =
            Time.realtimeSinceStartup +
            ProgressSaveIntervalSeconds;

        SaveProgress();
    }

    private void SaveProgress()
    {
        try
        {
            PlayerPrefs.SetInt(
                AttemptsKey,
                Mathf.Max(0, attemptCount)
            );

            PlayerPrefs.SetInt(
                AttemptTargetKey,
                attemptTarget
            );

            PlayerPrefs.SetFloat(
                MainMenuSecondsKey,
                Mathf.Max(0f, mainMenuActiveSeconds)
            );

            PlayerPrefs.Save();
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[Ads] Reklam sayaçlari kaydedilemedi; oyun etkilenmeyecek: " +
                exception.Message
            );
        }
    }

    private static int RollNextAttemptTarget()
    {
        return UnityEngine.Random.Range(
            MinimumAttemptsPerAd,
            MaximumAttemptsPerAd + 1
        );
    }

    private static string GetInterstitialAdUnitId()
    {
#if UNITY_EDITOR
        // Play Mode must never request production inventory.
        return AndroidTestInterstitialId;
#else
        // Android player builds use the real AdMob unit. Google Play Games on
        // PC runs the Android build, so it uses this production ID as well.
        return AndroidProductionInterstitialId;
#endif
    }

    private static bool IsAdsRuntimeSupported()
    {
#if UNITY_ANDROID || UNITY_EDITOR
        return true;
#else
        return false;
#endif
    }

    private static void InvokeSafely(Action action)
    {
        if (action == null)
            return;

        try
        {
            action.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[Ads] Privacy callback hatasi oyun akisindan izole edildi: " +
                exception.Message
            );
        }
    }

    private static void RunOnUnityThread(Action action)
    {
        if (action == null)
            return;

        try
        {
            GoogleMobileAds.Common.MobileAdsEventExecutor
                .ExecuteInUpdate(action);
        }
        catch
        {
            // Callback'i background thread'de zorla calistirmiyoruz.
            // Reklam devre disi kalabilir ama Unity state'i riske atilmaz.
        }
    }

    private static void DestroyAdSafely(ref InterstitialAd ad)
    {
        InterstitialAd adToDestroy = ad;
        ad = null;
        DestroySpecificAdSafely(adToDestroy);
    }

    private static void DestroySpecificAdSafely(InterstitialAd ad)
    {
        if (ad == null)
            return;

        try
        {
            ad.Destroy();
        }
        catch
        {
            // Reklam objesi cleanup hatasi gameplay'i etkileyemez.
        }
    }
}
