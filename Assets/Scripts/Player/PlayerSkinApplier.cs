using UnityEngine;

[DefaultExecutionOrder(100)]
public class PlayerSkinApplier : MonoBehaviour
{
    [Header("Skin Data")]
    [SerializeField]
    private PlayerSkinCatalog skinCatalog;

    [Header("References")]
    [SerializeField]
    private SpriteRenderer playerSpriteRenderer;

    [SerializeField]
    private PlayerDash playerDash;

    [SerializeField]
    private PlayerArmor playerArmor;

    [SerializeField]
    private SpecialSkinVisuals specialSkinVisuals;

    public PlayerSkinCatalog.SkinEntry CurrentSkin
    {
        get;
        private set;
    }

    public Sprite CurrentSprite =>
        CurrentSkin != null
            ? CurrentSkin.playerSprite
            : playerSpriteRenderer != null
                ? playerSpriteRenderer.sprite
                : null;

    public Color CurrentDashTrailColor =>
        CurrentSkin != null
            ? CurrentSkin.dashTrailColor
            : Color.white;

    public Color CurrentArmorVisualColor =>
        CurrentSkin != null
            ? CurrentSkin.armorVisualColor
            : Color.white;

    private void Awake()
    {
        FindMissingReferences();
        ApplySelectedSkin();
    }

    private void OnEnable()
    {
        ApplySelectedSkin();
    }

    public void ApplySelectedSkin()
    {
        if (skinCatalog == null)
        {
            Debug.LogWarning(
                "PlayerSkinApplier skinCatalog is missing.",
                this
            );
            return;
        }

        ApplySkin(skinCatalog.GetSelectedSkin());
    }

    public void ApplySkin(
        PlayerSkinCatalog.SkinEntry skin)
    {
        if (skin == null)
            return;

        FindMissingReferences();

        CurrentSkin = skin;

        if (playerSpriteRenderer != null &&
            skin.playerSprite != null)
        {
            playerSpriteRenderer.sprite =
                skin.playerSprite;
        }

        if (playerDash != null)
        {
            playerDash.ApplyTrailColor(
                skin.dashTrailColor
            );
        }

        if (playerArmor != null)
        {
            playerArmor.ApplyVisualColor(
                skin.armorVisualColor
            );
        }

        if (specialSkinVisuals != null)
        {
            specialSkinVisuals.ApplySkin(skin);
        }
    }

    private void FindMissingReferences()
    {
        if (playerSpriteRenderer == null)
        {
            playerSpriteRenderer =
                GetComponentInChildren<SpriteRenderer>(true);
        }

        if (playerDash == null)
            playerDash = GetComponent<PlayerDash>();

        if (playerArmor == null)
            playerArmor = GetComponent<PlayerArmor>();

        if (specialSkinVisuals == null)
        {
            specialSkinVisuals =
                GetComponent<SpecialSkinVisuals>();

            if (specialSkinVisuals == null)
            {
                specialSkinVisuals =
                    gameObject.AddComponent<SpecialSkinVisuals>();
            }
        }
    }
}
