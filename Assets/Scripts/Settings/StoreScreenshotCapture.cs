using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class StoreScreenshotCapture : MonoBehaviour
{
    private static StoreScreenshotCapture instance;
    private bool isCapturing;

    private void Awake()
    {
        // Scene değişince ikinci bir tane oluşmasını engeller.
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (keyboard.f9Key.wasPressedThisFrame && !isCapturing)
        {
            StartCoroutine(CaptureScreenshot());
        }
#endif
    }

    private IEnumerator CaptureScreenshot()
    {
        isCapturing = true;

        // Frame tamamen render edildikten sonra screenshot al.
        yield return new WaitForEndOfFrame();

        int width = Screen.width;
        int height = Screen.height;

        // Google Play için landscape screenshot alırken
        // Game View'i 1920x1080 yapmanı öneriyorum.
        if (width * 9 != height * 16)
        {
            Debug.LogWarning(
                $"Screenshot çözünürlüğü 16:9 değil: {width}x{height}. " +
                "Store screenshot için Game View'i 1920x1080 yap."
            );
        }

        string folderPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "StoreScreenshots")
        );

        Directory.CreateDirectory(folderPath);

        string sceneName = SceneManager.GetActiveScene().name;
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        string fileName =
            $"FatefulRush_{sceneName}_{width}x{height}_{timestamp}.png";

        string fullPath = Path.Combine(folderPath, fileName);

        ScreenCapture.CaptureScreenshot(fullPath);

        Debug.Log(
            $"STORE SCREENSHOT ALINDI!\n" +
            $"Scene: {sceneName}\n" +
            $"Resolution: {width}x{height}\n" +
            $"Path: {fullPath}"
        );

        // Aynı tuşa yanlışlıkla iki kez basılmasını önler.
        yield return null;

        isCapturing = false;
    }
}