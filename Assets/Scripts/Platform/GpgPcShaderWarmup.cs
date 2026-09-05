using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Moves first-use shader preparation to MainMenu in the dedicated Google Play
/// Games on PC build, so Level 1 does not pay the cost while enemies/effects
/// appear for the first time. Ads wait for this one-time warm-up to finish.
/// </summary>
public sealed class GpgPcShaderWarmup : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";

    public static bool IsComplete { get; private set; }

    private static GpgPcShaderWarmup instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (instance != null)
            return;

        GameObject root = new GameObject("[GPG PC] Shader Warmup");
        instance = root.AddComponent<GpgPcShaderWarmup>();
        DontDestroyOnLoad(root);
#else
        IsComplete = true;
#endif
    }

    private IEnumerator Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // This project produces the dedicated x86-64 GPG PC Android artifact.
        // Keep the cost out of Intro and gameplay: prepare once after MainMenu
        // has rendered its first initialization frame.
        while (SceneManager.GetActiveScene().name != MainMenuSceneName)
            yield return null;

        yield return null;

        try
        {
            Shader.WarmupAllShaders();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning(
                "[GPG PC ShaderWarmup] Warmup failed safely: " +
                exception.Message
            );
        }

        IsComplete = true;
        instance = null;
        Destroy(gameObject);
#else
        IsComplete = true;
        yield break;
#endif
    }
}
