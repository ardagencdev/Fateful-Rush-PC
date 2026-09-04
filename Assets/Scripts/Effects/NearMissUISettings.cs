using TMPro;
using UnityEngine;

public sealed class NearMissUISettings : ScriptableObject
{
    [SerializeField]
    private TMP_FontAsset nearMissFont;

    public TMP_FontAsset NearMissFont => nearMissFont;
}
