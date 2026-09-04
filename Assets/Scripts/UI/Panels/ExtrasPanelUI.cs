using UnityEngine;

/// <summary>
/// Main Menu'deki Extras panelini yonetir.
///
/// Extras icerigi:
/// - Achievements
/// - Leaderboards
/// - Privacy (butonun ustunde PrivacyPolicyButton component'i kullanilir)
/// - Credits
///
/// Bu component'i MainMenuPanel veya ExtrasPanel yerine, her zaman aktif kalan
/// Canvas / MenuControllers gibi bir objeye eklemek daha guvenlidir.
/// </summary>
public sealed class ExtrasPanelUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject extrasPanel;

    [Header("Transition")]
    [SerializeField] private UIPanelFadeSwitcher fadeSwitcher;

    public bool IsOpen =>
        extrasPanel != null &&
        extrasPanel.activeSelf;

    private void Awake()
    {
        if (fadeSwitcher == null)
        {
            fadeSwitcher =
                FindAnyObjectByType<UIPanelFadeSwitcher>();
        }

        // Scene acilirken Extras gorunmesin.
        if (extrasPanel != null)
        {
            if (fadeSwitcher != null)
                fadeSwitcher.SetInstant(extrasPanel, false);
            else
                extrasPanel.SetActive(false);
        }
    }

    public void OpenExtras()
    {
        if (mainMenuPanel == null ||
            extrasPanel == null)
        {
            Debug.LogError(
                "[ExtrasPanelUI] MainMenuPanel veya ExtrasPanel reference eksik.",
                this
            );

            return;
        }

        // Extras da Main Menu'nin secili skin temasini kullanir.
        MainMenuStarColorRandomizer.Instance?
            .ShowMainMenuColor();

        SwitchPanels(
            mainMenuPanel,
            extrasPanel
        );
    }

    public void CloseExtras()
    {
        if (mainMenuPanel == null ||
            extrasPanel == null)
        {
            return;
        }

        MainMenuStarColorRandomizer.Instance?
            .ShowMainMenuColor();

        SwitchPanels(
            extrasPanel,
            mainMenuPanel
        );
    }

    public void OpenAchievements()
    {
        GooglePlayGamesManager.ShowAchievementsUI();
    }

    public void OpenLeaderboards()
    {
        GooglePlayGamesLeaderboards.ShowAllLeaderboardsUI();
    }

    public void OpenCredits()
    {
        if (MainMenu.Instance == null)
        {
            Debug.LogError(
                "[ExtrasPanelUI] MainMenu instance bulunamadi. Credits acilamadi.",
                this
            );

            return;
        }

        MainMenu.Instance.OpenCreditsFromExtras();
    }

    /// <summary>
    /// Credits Extras'tan acildiysa MainMenu sahnesi yeniden yuklendikten sonra
    /// paneli fade oynatmadan dogrudan eski konumuna getirir. SceneTransition
    /// zaten siyah ekrandayken sahneyi yukledigi icin oyuncu Main Menu flash'i gormez.
    /// </summary>
    public void RestoreAfterCredits()
    {
        if (mainMenuPanel == null ||
            extrasPanel == null)
        {
            Debug.LogError(
                "[ExtrasPanelUI] Credits sonrasi Extras restore edilemedi: panel reference eksik.",
                this
            );

            return;
        }

        MainMenuStarColorRandomizer.Instance?
            .ShowMainMenuColor();

        if (fadeSwitcher != null)
        {
            fadeSwitcher.SetInstant(
                mainMenuPanel,
                false
            );

            fadeSwitcher.SetInstant(
                extrasPanel,
                true
            );
        }
        else
        {
            mainMenuPanel.SetActive(false);
            extrasPanel.SetActive(true);
        }
    }

    public bool HandleBack()
    {
        if (!IsOpen)
            return false;

        CloseExtras();
        return true;
    }

    private void SwitchPanels(
        GameObject fromPanel,
        GameObject toPanel)
    {
        if (fadeSwitcher != null)
        {
            fadeSwitcher.SwitchPanel(
                fromPanel,
                toPanel
            );

            return;
        }

        if (fromPanel != null)
            fromPanel.SetActive(false);

        if (toPanel != null)
            toPanel.SetActive(true);
    }
}
