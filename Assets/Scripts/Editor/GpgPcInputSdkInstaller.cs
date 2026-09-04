#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// One-click installer for Google's official Input SDK 1.1.1-beta Unity
/// package (the archive build that includes its required dependencies).
/// </summary>
public static class GpgPcInputSdkInstaller
{
    private const string InputSdkUrl =
        "https://dl.google.com/games/registry/unity/" +
        "com.google.android.libraries.play.games.inputmapping/" +
        "com.google.android.libraries.play.games.inputmapping-1.1.1-beta.unitypackage";

    private static UnityWebRequest activeRequest;
    private static string downloadPath;

    [MenuItem("Fateful Rush/PC/Install Google Input SDK 1.1.1-beta")]
    public static void Install()
    {
        if (IsInstalled())
        {
            Debug.Log("[GPG PC] Google Input SDK is already installed.");
            GpgPcInputSdkDefineWatcher.SyncDefine();
            return;
        }

        if (activeRequest != null)
        {
            Debug.Log("[GPG PC] Input SDK download is already running.");
            return;
        }

        string folder = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Library",
            "FatefulRushPC"
        );
        Directory.CreateDirectory(folder);

        downloadPath = Path.Combine(
            folder,
            "GoogleInputSDK-1.1.1-beta.unitypackage"
        );

        activeRequest = UnityWebRequest.Get(InputSdkUrl);
        activeRequest.downloadHandler =
            new DownloadHandlerFile(downloadPath);
        activeRequest.SendWebRequest();

        EditorApplication.update += PollDownload;
        Debug.Log("[GPG PC] Downloading official Google Input SDK 1.1.1-beta...");
    }

    private static void PollDownload()
    {
        if (activeRequest == null || !activeRequest.isDone)
            return;

        EditorApplication.update -= PollDownload;

        bool success =
            activeRequest.result == UnityWebRequest.Result.Success;
        string error = activeRequest.error;
        activeRequest.Dispose();
        activeRequest = null;

        if (!success)
        {
            Debug.LogError(
                "[GPG PC] Input SDK download failed: " + error +
                "\nUse Google's Input SDK archive and import version " +
                "1.1.1-beta manually."
            );
            return;
        }

        Debug.Log(
            "[GPG PC] Input SDK downloaded. Importing package and dependencies..."
        );

        AssetDatabase.ImportPackage(downloadPath, false);
        // ImportPackage triggers the required refresh/recompile itself.
        // Do not force another AssetDatabase.Refresh here.
        EditorApplication.delayCall +=
            GpgPcInputSdkDefineWatcher.SyncDefine;
    }

    private static bool IsInstalled()
    {
        const string providerType =
            "Google.Android.Libraries.Play.Games.Inputmapping." +
            "InputMappingProviderCallbackHelper";

        foreach (System.Reflection.Assembly assembly in
                 System.AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetType(providerType, false) != null)
                return true;
        }

        return false;
    }
}
#endif
