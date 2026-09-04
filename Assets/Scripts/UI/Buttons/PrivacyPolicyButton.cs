using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class PrivacyPolicyButton : MonoBehaviour
{
    private const string DefaultPrivacyPolicyUrl =
        "https://ardagencdev.github.io/fateful-rush-privacy/";

    [SerializeField]
    private string privacyPolicyUrl = DefaultPrivacyPolicyUrl;

    private Button button;
    private bool privacyActionInProgress;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        if (button == null)
            button = GetComponent<Button>();

        privacyActionInProgress = false;

        button.onClick.RemoveListener(OpenPrivacyPolicy);
        button.onClick.AddListener(OpenPrivacyPolicy);
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(OpenPrivacyPolicy);

        privacyActionInProgress = false;
    }

    /// <summary>
    /// Tek Privacy butonunu iki amac icin guvenli sekilde kullanir:
    /// - UMP privacy options gerekiyorsa Google consent tercihlerini acar.
    /// - Gerekmiyorsa mevcut privacy policy web sayfasini acar.
    /// UMP hata verirse de web sayfasina fallback yapar.
    /// </summary>
    public void OpenPrivacyPolicy()
    {
        if (privacyActionInProgress)
            return;

        if (FatefulRushAdManager.IsPrivacyOptionsRequired)
        {
            privacyActionInProgress = true;

            bool formStarted =
                FatefulRushAdManager.TryShowPrivacyOptions(
                    onFailure: OpenPrivacyPolicyUrlSafely,
                    onClosed: () => privacyActionInProgress = false
                );

            if (formStarted)
                return;

            // UMP formu daha baslatma asamasinda hata verdiyse
            // buton yine islevsiz kalmasin.
            privacyActionInProgress = false;
        }

        OpenPrivacyPolicyUrlSafely();
    }

    private void OpenPrivacyPolicyUrlSafely()
    {
        privacyActionInProgress = false;

        try
        {
            if (!Uri.TryCreate(
                    privacyPolicyUrl,
                    UriKind.Absolute,
                    out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttp &&
                 uri.Scheme != Uri.UriSchemeHttps))
            {
                Debug.LogError(
                    "PrivacyPolicyButton has an invalid HTTP/HTTPS URL.",
                    this
                );
                return;
            }

            Application.OpenURL(uri.AbsoluteUri);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "Privacy link could not be opened, but the UI remains usable: " +
                exception.Message,
                this
            );
        }
    }
}
