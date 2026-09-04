using TMPro;
using UnityEngine;

public class CurrentLevelHUD : MonoBehaviour
{
    private const float FontSize = 18f;
    private const float TopOffset = 3f;

    private TextMeshProUGUI levelText;
    private Color nearStarsColor = Color.white;

    public static CurrentLevelHUD Create(
        LevelConfig level,
        Color appliedNearStarsColor,
        Canvas targetCanvas,
        int siblingIndex)
    {
        if (level == null ||
            level.levelNumber <= 0 ||
            targetCanvas == null)
        {
            return null;
        }

        TMP_FontAsset sceneFont =
            FindMichromaFont(targetCanvas);

        GameObject levelHudObject =
            new GameObject(
                "Current Level HUD",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI),
                typeof(CurrentLevelHUD)
            );

        levelHudObject.transform.SetParent(
            targetCanvas.transform,
            false
        );

        int safeSiblingIndex =
            Mathf.Clamp(
                siblingIndex,
                0,
                targetCanvas.transform.childCount - 1
            );

        levelHudObject.transform.SetSiblingIndex(
            safeSiblingIndex
        );

        RectTransform rectTransform =
            levelHudObject.GetComponent<RectTransform>();

        rectTransform.anchorMin =
            new Vector2(0.5f, 1f);

        rectTransform.anchorMax =
            new Vector2(0.5f, 1f);

        rectTransform.pivot =
            new Vector2(0.5f, 1f);

        rectTransform.anchoredPosition =
            new Vector2(0f, -TopOffset);

        rectTransform.sizeDelta =
            new Vector2(420f, 48f);

        TextMeshProUGUI text =
            levelHudObject.GetComponent<TextMeshProUGUI>();

        if (sceneFont != null)
            text.font = sceneFont;

        text.text =
            $"LEVEL {level.levelNumber}";

        text.fontSize = FontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.characterSpacing = 2f;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Overflow;

        CurrentLevelHUD levelHud =
            levelHudObject.GetComponent<CurrentLevelHUD>();

        levelHud.Configure(
            text,
            appliedNearStarsColor
        );

        levelHud.SetVisible(false);

        return levelHud;
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
    }

    private void Configure(
        TextMeshProUGUI text,
        Color appliedNearStarsColor)
    {
        levelText = text;
        nearStarsColor = ForceOpaque(appliedNearStarsColor);

        ApplyNearStarsColor();
    }

    private void OnEnable()
    {
        if (levelText == null)
            levelText = GetComponent<TextMeshProUGUI>();

        ApplyNearStarsColor();
        RegisterWithOcclusionController();

        if (levelText != null)
            levelText.SetVerticesDirty();
    }

    private void RegisterWithOcclusionController()
    {
        HUDPlayerOcclusionController controller =
            FindAnyObjectByType<HUDPlayerOcclusionController>();

        if (controller != null)
            controller.RegisterHUDRoot(gameObject);
    }

    private void ApplyNearStarsColor()
    {
        if (levelText == null)
            levelText = GetComponent<TextMeshProUGUI>();

        if (levelText == null)
            return;

        levelText.color = ForceOpaque(nearStarsColor);
    }

    private static Color ForceOpaque(Color color)
    {
        color.a = 1f;
        return color;
    }

    private static TMP_FontAsset FindMichromaFont(
        Canvas targetCanvas)
    {
        if (targetCanvas != null)
        {
            TextMeshProUGUI[] canvasTexts =
                targetCanvas.GetComponentsInChildren
                    <TextMeshProUGUI>(true);

            for (int i = 0;
                 i < canvasTexts.Length;
                 i++)
            {
                TMP_FontAsset font =
                    canvasTexts[i] != null
                        ? canvasTexts[i].font
                        : null;

                if (IsMichroma(font))
                    return font;
            }
        }

        TMP_FontAsset[] loadedFonts =
            Resources.FindObjectsOfTypeAll
                <TMP_FontAsset>();

        for (int i = 0;
             i < loadedFonts.Length;
             i++)
        {
            if (IsMichroma(loadedFonts[i]))
                return loadedFonts[i];
        }

        return TMP_Settings.defaultFontAsset;
    }

    private static bool IsMichroma(
        TMP_FontAsset font)
    {
        return
            font != null &&
            !string.IsNullOrEmpty(font.name) &&
            font.name.IndexOf(
                "Michroma",
                System.StringComparison.OrdinalIgnoreCase
            ) >= 0;
    }
}
