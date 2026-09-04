using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerSkinPanelUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject skinPanel;
    [SerializeField] private UIPanelFadeSwitcher fadeSwitcher;

    [Header("Panel Buttons")]
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;

    [Header("Skin Navigation")]
    [SerializeField] private Button previousSkinButton;
    [SerializeField] private Button nextSkinButton;
    [SerializeField] private Button equipButton;
    [SerializeField] private TMP_Text equipButtonText;

    [Header("Skin Data")]
    [SerializeField] private PlayerSkinCatalog skinCatalog;

    [Header("Single Skin Page")]
    [Tooltip("Sprite, name, status, requirement and equip button should be children of this object.")]
    [SerializeField] private RectTransform skinPageContainer;
    [SerializeField] private Image skinImage;
    [SerializeField] private TMP_Text skinNameText;
    [SerializeField] private TMP_Text skinStatusText;
    [SerializeField] private TMP_Text skinRequirementText;
    [SerializeField] private TMP_Text pageIndicatorText;
    [SerializeField] private GameObject lockOverlay;

    [Header("Locked Visual")]
    [SerializeField] private Color unlockedImageColor = Color.white;
    [SerializeField]
    private Color lockedImageColor =
        new Color(0.28f, 0.28f, 0.28f, 1f);

    [Header("Swipe")]
    [SerializeField, Min(1f)] private float minSwipeDistance = 80f;

    [Header("Page Animation")]
    [SerializeField, Min(0f)] private float pageSlideDistance = 700f;
    [SerializeField, Min(0.01f)] private float pageAnimDuration = 0.22f;

    private int currentSkinIndex;
    private Vector2 dragStartPosition;
    private Vector2 pageStartPosition;
    private bool isDragging;

    private readonly List<RaycastResult> swipeRaycastResults =
        new List<RaycastResult>();

    private CanvasGroup pageCanvasGroup;
    private Coroutine pageRoutine;

    private UIButtonSound previousSkinSound;
    private UIButtonSound nextSkinSound;

    public bool IsOpen =>
        skinPanel != null && skinPanel.activeSelf;

    private void Awake()
    {
        PreparePageContainer();
        PrepareButtons();
    }

    private void OnDestroy()
    {
        StopPageAnimation();

        if (openButton != null)
            openButton.onClick.RemoveListener(OpenPanel);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(ClosePanel);

        if (previousSkinButton != null)
            previousSkinButton.onClick.RemoveListener(PreviousSkin);

        if (nextSkinButton != null)
            nextSkinButton.onClick.RemoveListener(NextSkin);

        if (equipButton != null)
            equipButton.onClick.RemoveListener(EquipCurrentSkin);
    }

    private void Update()
    {
        if (!IsOpen || pageRoutine != null)
            return;

        HandlePcPageNavigation();
    }

    public void OpenPanel()
    {
        if (!ValidateReferences())
            return;

        StopPageAnimation();
        currentSkinIndex = FindSelectedSkinIndex();

        MainMenuStarColorRandomizer.Instance?
            .BeginSkinPreview();

        RefreshCurrentSkin();
        ResetPageVisuals();

        // The Main Menu Skins button owns its own sound through UIButtonSound.
        // Do not force a SoundManager preset here; this lets the Inspector
        // Sound Type / Custom Sound fields work exactly as configured.
        SwitchPanels(mainMenuPanel, skinPanel);
    }

    public void ClosePanel()
    {
        if (mainMenuPanel == null || skinPanel == null)
            return;

        StopPageAnimation();
        ResetPageVisuals();

        MainMenuStarColorRandomizer.Instance?
            .EndSkinPreview();

        SoundManager.Instance?.PlayBackButtonSound(closeButton != null ? closeButton.transform as RectTransform : transform as RectTransform);
        SwitchPanels(skinPanel, mainMenuPanel);
    }

    public void NextSkin()
    {
        CancelSwipeTracking();

        if (!CanNavigate() || skinCatalog.Skins.Count <= 1)
            return;

        int nextSkinIndex =
            (currentSkinIndex + 1) % skinCatalog.Skins.Count;

        nextSkinSound?.PlayConfiguredSound();
        StartSkinTransition(nextSkinIndex, 1);
    }

    public void PreviousSkin()
    {
        CancelSwipeTracking();

        if (!CanNavigate() || skinCatalog.Skins.Count <= 1)
            return;

        int previousSkinIndex =
            (currentSkinIndex - 1 + skinCatalog.Skins.Count) %
            skinCatalog.Skins.Count;

        previousSkinSound?.PlayConfiguredSound();
        StartSkinTransition(previousSkinIndex, -1);
    }

    public void EquipCurrentSkin()
    {
        // A UI button press must never be reused as a page swipe.
        CancelSwipeTracking();

        PlayerSkinCatalog.SkinEntry skin = GetCurrentSkin();

        if (skin == null || skinCatalog == null)
            return;

        if (!skinCatalog.IsUnlocked(skin))
        {
            SoundManager.Instance?.PlayLockedLevelSound(equipButton != null ? equipButton.transform as RectTransform : transform as RectTransform);
            VibrationManager.Instance?.VibrateUI();
            return;
        }

        // Unlocked/equipped skin clicks use the old Next Page SFX,
        // which is now dedicated to Skin Equip.
        SoundManager.Instance?.PlaySkinEquipSound(equipButton != null ? equipButton.transform as RectTransform : transform as RectTransform);
        VibrationManager.Instance?.VibrateUI();

        if (skinCatalog.IsSelected(skin))
            return;

        if (!skinCatalog.TrySelectSkin(skin))
            return;

        RefreshCurrentSkin();
    }

    private void PrepareButtons()
    {
        if (openButton != null)
        {
            // Intentionally leave UIButtonSound automatic playback enabled.
            // The Skins button SFX is configured manually in its UIButtonSound
            // component (including Custom Sound when desired).
            openButton.onClick.RemoveListener(OpenPanel);
            openButton.onClick.AddListener(OpenPanel);
        }

        if (closeButton != null)
        {
            DisableAutomaticButtonSound(closeButton);

            closeButton.onClick.RemoveListener(ClosePanel);
            closeButton.onClick.AddListener(ClosePanel);
        }

        if (previousSkinButton != null)
        {
            previousSkinSound =
                ConfigurePageNavigationSound(previousSkinButton);

            previousSkinButton.onClick.RemoveListener(PreviousSkin);
            previousSkinButton.onClick.AddListener(PreviousSkin);
        }

        if (nextSkinButton != null)
        {
            nextSkinSound =
                ConfigurePageNavigationSound(nextSkinButton);

            nextSkinButton.onClick.RemoveListener(NextSkin);
            nextSkinButton.onClick.AddListener(NextSkin);
        }

        if (equipButton != null)
        {
            // Equip butonunun sesini bu script yönetiyor.
            // Böylece kilitli skinde Option + Locked sesleri
            // aynı anda çalmaz.
            UIButtonSound automaticButtonSound =
                equipButton.GetComponent<UIButtonSound>();

            if (automaticButtonSound != null)
                automaticButtonSound.SetAutomaticPlayback(false);

            equipButton.onClick.RemoveListener(EquipCurrentSkin);
            equipButton.onClick.AddListener(EquipCurrentSkin);

            if (equipButtonText == null)
            {
                equipButtonText =
                    equipButton.GetComponentInChildren<TMP_Text>(true);
            }
        }
    }

    private static UIButtonSound ConfigurePageNavigationSound(Button button)
    {
        if (button == null)
            return null;

        UIButtonSound buttonSound =
            button.GetComponent<UIButtonSound>();

        if (buttonSound == null)
        {
            buttonSound =
                button.gameObject.AddComponent<UIButtonSound>();
        }

        buttonSound.enabled = true;
        buttonSound.ConfigureAsPageNavigation(manualPlayback: true);
        return buttonSound;
    }

    private static void DisableAutomaticButtonSound(Button button)
    {
        if (button == null)
            return;

        UIButtonSound buttonSound =
            button.GetComponent<UIButtonSound>();

        if (buttonSound != null)
            buttonSound.SetAutomaticPlayback(false);
    }

    private void PreparePageContainer()
    {
        if (skinPageContainer == null)
            return;

        pageStartPosition = skinPageContainer.anchoredPosition;
        pageCanvasGroup = skinPageContainer.GetComponent<CanvasGroup>();

        if (pageCanvasGroup == null)
        {
            pageCanvasGroup =
                skinPageContainer.gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void StartSkinTransition(int targetIndex, int direction)
    {
        CancelSwipeTracking();

        if (pageRoutine != null)
            return;

        SetControlsInteractable(false);

        pageRoutine = StartCoroutine(
            SkinTransitionRoutine(targetIndex, direction)
        );
    }

    private IEnumerator SkinTransitionRoutine(int targetIndex, int direction)
    {
        if (skinPageContainer == null || pageCanvasGroup == null)
        {
            currentSkinIndex = targetIndex;
            RefreshCurrentSkin();
            SetControlsInteractable(true);
            pageRoutine = null;
            yield break;
        }

        float duration = Mathf.Max(0.01f, pageAnimDuration);
        Vector2 start = pageStartPosition;
        Vector2 exitTarget =
            start + Vector2.left * pageSlideDistance * direction;

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(timer / duration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);

            skinPageContainer.anchoredPosition =
                Vector2.LerpUnclamped(start, exitTarget, eased);

            pageCanvasGroup.alpha = 1f - eased;
            yield return null;
        }

        currentSkinIndex = targetIndex;
        RefreshCurrentSkin();

        Vector2 enterStart =
            start + Vector2.right * pageSlideDistance * direction;

        skinPageContainer.anchoredPosition = enterStart;
        pageCanvasGroup.alpha = 0f;
        timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(timer / duration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);

            skinPageContainer.anchoredPosition =
                Vector2.LerpUnclamped(enterStart, start, eased);

            pageCanvasGroup.alpha = eased;
            yield return null;
        }

        ResetPageVisuals();
        SetControlsInteractable(true);
        pageRoutine = null;
    }

    private void RefreshCurrentSkin()
    {
        PlayerSkinCatalog.SkinEntry skin = GetCurrentSkin();

        if (skin == null || skinCatalog == null)
            return;

        bool unlocked = skinCatalog.IsUnlocked(skin);
        bool selected = skinCatalog.IsSelected(skin);

        MainMenuStarColorRandomizer.Instance?
            .ShowSkinPreviewSkin(skin);

        if (skinImage != null)
        {
            skinImage.sprite = skin.playerSprite;
            skinImage.preserveAspect = true;
            skinImage.enabled = skin.playerSprite != null;
            skinImage.color = unlocked
                ? unlockedImageColor
                : lockedImageColor;
        }

        if (skinNameText != null)
            skinNameText.text = skin.displayName;

        if (skinStatusText != null)
        {
            skinStatusText.text = selected
                ? "EQUIPPED"
                : unlocked
                    ? "AVAILABLE"
                    : "LOCKED";
        }

        if (skinRequirementText != null)
        {
            skinRequirementText.text = unlocked
                ? string.Empty
                : $"COMPLETE LEVEL {skin.requiredCompletedLevel} TO UNLOCK";
        }
        else if (skinStatusText != null && !unlocked)
        {
            skinStatusText.text =
                $"LOCKED - COMPLETE LEVEL {skin.requiredCompletedLevel}";
        }

        if (lockOverlay != null)
            lockOverlay.SetActive(!unlocked);

        if (equipButton != null)
        {
            equipButton.gameObject.SetActive(true);

            // EQUIPPED ve LOCKED durumlarında da tıklama alınmalı:
            // EQUIPPED -> Option sesi
            // LOCKED   -> Locked Level sesi
            equipButton.interactable = true;
        }

        if (equipButtonText != null)
        {
            equipButtonText.text = selected
                ? "EQUIPPED"
                : unlocked
                    ? "EQUIP"
                    : "LOCKED";
        }

        if (pageIndicatorText != null)
        {
            pageIndicatorText.text =
                $"{currentSkinIndex + 1}/{skinCatalog.Skins.Count}";
        }

        RefreshNavigationButtons();
    }

    private void RefreshNavigationButtons()
    {
        int skinCount = skinCatalog != null
            ? skinCatalog.Skins.Count
            : 0;

        // Sayfa gecis animasyonu devam ederken navigation butonlarini
        // tekrar aktif etme. RefreshCurrentSkin() gecisin ortasinda
        // cagrildigi icin aksi halde SFX calisirken NextSkin/PreviousSkin
        // pageRoutine kontrolune takilip islemi reddedebiliyordu.
        bool canNavigate = skinCount > 1 && pageRoutine == null;

        if (previousSkinButton != null)
        {
            previousSkinButton.gameObject.SetActive(true);
            previousSkinButton.interactable = canNavigate;
        }

        if (nextSkinButton != null)
        {
            nextSkinButton.gameObject.SetActive(true);
            nextSkinButton.interactable = canNavigate;
        }
    }

    private void SetControlsInteractable(bool state)
    {
        bool canNavigate =
            skinCatalog != null && skinCatalog.Skins.Count > 1;

        if (previousSkinButton != null)
            previousSkinButton.interactable = state && canNavigate;

        if (nextSkinButton != null)
            nextSkinButton.interactable = state && canNavigate;

        if (equipButton != null)
        {
            // Sadece sayfa geçiş animasyonu sırasında kapat.
            // Seçili veya kilitli olması butonu devre dışı bırakmaz.
            equipButton.interactable = state;
        }
    }

    private PlayerSkinCatalog.SkinEntry GetCurrentSkin()
    {
        if (skinCatalog == null || skinCatalog.Skins.Count == 0)
            return null;

        currentSkinIndex = Mathf.Clamp(
            currentSkinIndex,
            0,
            skinCatalog.Skins.Count - 1
        );

        return skinCatalog.Skins[currentSkinIndex];
    }

    private int FindSelectedSkinIndex()
    {
        if (skinCatalog == null || skinCatalog.Skins.Count == 0)
            return 0;

        PlayerSkinCatalog.SkinEntry selected =
            skinCatalog.GetSelectedSkin();

        if (selected == null)
            return 0;

        for (int i = 0; i < skinCatalog.Skins.Count; i++)
        {
            PlayerSkinCatalog.SkinEntry skin = skinCatalog.Skins[i];

            if (skin != null && skin.id == selected.id)
                return i;
        }

        return 0;
    }

    private bool CanNavigate()
    {
        return pageRoutine == null &&
               skinCatalog != null &&
               skinCatalog.Skins.Count > 0;
    }

    private void HandlePcPageNavigation()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame ||
                Keyboard.current.dKey.wasPressedThisFrame)
            {
                NextSkin();
                return;
            }

            if (Keyboard.current.leftArrowKey.wasPressedThisFrame ||
                Keyboard.current.aKey.wasPressedThisFrame)
            {
                PreviousSkin();
                return;
            }

            if (Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            {
                EquipCurrentSkin();
                return;
            }

        }

        if (Gamepad.current != null)
        {
            if (Gamepad.current.dpad.right.wasPressedThisFrame)
            {
                NextSkin();
                return;
            }

            if (Gamepad.current.dpad.left.wasPressedThisFrame)
            {
                PreviousSkin();
                return;
            }

            if (Gamepad.current.buttonSouth.wasPressedThisFrame)
            {
                EquipCurrentSkin();
                return;
            }

        }

        if (Mouse.current != null)
        {
            float wheel = Mouse.current.scroll.ReadValue().y;
            if (wheel < -0.01f)
                NextSkin();
            else if (wheel > 0.01f)
                PreviousSkin();
        }

        HandleMouseSwipe();
    }

    private void HandleMouseSwipe()
    {
        if (Mouse.current == null)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            BeginSwipeTracking(
                Mouse.current.position.ReadValue()
            );
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            EndSwipeTracking(
                Mouse.current.position.ReadValue()
            );
        }
    }

    private void BeginSwipeTracking(Vector2 screenPosition)
    {
        isDragging = false;

        // Same protection used by the Options pages:
        // a press that starts on EQUIP / NEXT / PREVIOUS / BACK
        // (or any other interactable Selectable) belongs to the UI,
        // never to page-swipe navigation.
        if (IsPointerOverInteractableSelectable(screenPosition))
            return;

        dragStartPosition = screenPosition;
        isDragging = true;
    }

    private void EndSwipeTracking(Vector2 screenPosition)
    {
        if (!isDragging)
            return;

        isDragging = false;
        TrySwipe(screenPosition);
    }

    private void CancelSwipeTracking()
    {
        isDragging = false;
    }

    private bool IsPointerOverInteractableSelectable(
        Vector2 screenPosition)
    {
        if (EventSystem.current == null)
            return false;

        PointerEventData pointerData =
            new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            };

        swipeRaycastResults.Clear();
        EventSystem.current.RaycastAll(
            pointerData,
            swipeRaycastResults
        );

        foreach (RaycastResult result in swipeRaycastResults)
        {
            if (result.gameObject == null)
                continue;

            Selectable selectable =
                result.gameObject
                    .GetComponentInParent<Selectable>();

            if (selectable != null && selectable.IsInteractable())
                return true;
        }

        return false;
    }

    private void TrySwipe(Vector2 endPosition)
    {
        float swipeX = endPosition.x - dragStartPosition.x;

        if (Mathf.Abs(swipeX) < minSwipeDistance)
            return;

        if (swipeX < 0f)
            NextSkin();
        else
            PreviousSkin();
    }

    private void StopPageAnimation()
    {
        if (pageRoutine != null)
        {
            StopCoroutine(pageRoutine);
            pageRoutine = null;
        }

        isDragging = false;
        ResetPageVisuals();
    }

    private void ResetPageVisuals()
    {
        if (skinPageContainer != null)
            skinPageContainer.anchoredPosition = pageStartPosition;

        if (pageCanvasGroup != null)
            pageCanvasGroup.alpha = 1f;
    }

    private void SwitchPanels(GameObject fromPanel, GameObject toPanel)
    {
        if (fadeSwitcher != null)
        {
            fadeSwitcher.SwitchPanel(fromPanel, toPanel);
            return;
        }

        if (fromPanel != null)
            fromPanel.SetActive(false);

        if (toPanel != null)
            toPanel.SetActive(true);
    }

    private bool ValidateReferences()
    {
        if (mainMenuPanel == null || skinPanel == null)
        {
            Debug.LogError(
                "PlayerSkinPanelUI Main Menu or Skin Panel reference is missing.",
                this
            );
            return false;
        }

        if (skinCatalog == null || skinCatalog.Skins.Count == 0)
        {
            Debug.LogError(
                "PlayerSkinPanelUI skin catalog is missing or empty.",
                this
            );
            return false;
        }

        if (skinImage == null || skinNameText == null ||
            skinStatusText == null)
        {
            Debug.LogError(
                "PlayerSkinPanelUI single skin display references are missing.",
                this
            );
            return false;
        }

        return true;
    }
}