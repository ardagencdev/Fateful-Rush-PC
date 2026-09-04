using TMPro;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(TMP_Text))]
public sealed class MenuVersionText : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private string prefix = "v";

    private TMP_Text versionText;

    private void OnEnable()
    {
        RefreshVersionText();
    }

#if UNITY_EDITOR
    private void Update()
    {
        // Project Settings > Player > Version değiştiğinde
        // Play Mode'a girmeden Inspector/Game View üzerinde günceller.
        if (!Application.isPlaying)
            RefreshVersionText();
    }
#endif

    private void RefreshVersionText()
    {
        if (versionText == null)
            versionText = GetComponent<TMP_Text>();

        if (versionText == null)
            return;

        string value = prefix + Application.version;

        if (versionText.text != value)
            versionText.text = value;
    }
}
