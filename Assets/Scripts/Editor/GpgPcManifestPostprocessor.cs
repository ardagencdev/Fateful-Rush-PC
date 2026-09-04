#if UNITY_EDITOR && UNITY_ANDROID
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// Final Google Play Games on PC manifest cleanup for the dedicated PC build.
/// Removes mobile-only hardware/permission declarations that Google lists as
/// unsupported on PC and enables direct PC mouse behavior.
/// </summary>
public sealed class GpgPcManifestPostprocessor : IPostGenerateGradleAndroidProject
{
    private static readonly XNamespace AndroidNs =
        "http://schemas.android.com/apk/res/android";

    private static readonly HashSet<string> UnsupportedPcFeatures =
        new HashSet<string>
        {
            "android.hardware.audio.pro",
            "android.hardware.bluetooth",
            "android.hardware.camera",
            "android.hardware.camera.autofocus",
            "android.hardware.consumerir",
            "android.hardware.location",
            "android.hardware.location.gps",
            "android.hardware.location.network",
            "android.hardware.nfc",
            "android.hardware.sensor.light",
            "android.hardware.sensor.accelerometer",
            "android.hardware.sensor.barometer",
            "android.hardware.sensor.compass",
            "android.hardware.sensor.gyroscope",
            "android.hardware.sensor.proximity",
            "android.hardware.telephony",
            "android.hardware.touchscreen",
            "android.hardware.faketouch",
            "android.hardware.usb.accessory",
            "android.hardware.usb.host",
            "android.hardware.wifi",
            "android.software.midi"
        };

    private static readonly HashSet<string> UnsupportedPcPermissions =
        new HashSet<string>
        {
            "android.permission.ACCESS_COARSE_LOCATION",
            "android.permission.ACCESS_FINE_LOCATION",
            "android.permission.ACCESS_WIFI_STATE",
            "android.permission.BLUETOOTH",
            "android.permission.CAMERA",
            "android.permission.FOREGROUND_SERVICE",
            "android.permission.GET_ACCOUNTS",
            "android.permission.INSTALL_PACKAGES",
            "android.permission.READ_CONTACTS",
            "android.permission.READ_EXTERNAL_STORAGE",
            "android.permission.READ_PHONE_STATE",
            "android.permission.RECEIVE_BOOT_COMPLETED",
            "android.permission.REQUEST_INSTALL_PACKAGES",
            "android.permission.SYSTEM_ALERT_WINDOW",
            "android.permission.USE_CREDENTIALS",
            "android.permission.WRITE_EXTERNAL_STORAGE",
            "android.permission.WRITE_SETTINGS",
            "com.google.android.gms.permission.ACTIVITY_RECOGNITION"
        };

    public int callbackOrder => 10000;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string[] manifests =
        {
            Path.Combine(path, "launcher", "src", "main", "AndroidManifest.xml"),
            Path.Combine(path, "unityLibrary", "src", "main", "AndroidManifest.xml")
        };

        foreach (string manifestPath in manifests)
        {
            if (File.Exists(manifestPath))
                PatchManifest(manifestPath);
        }

        PatchProguardRules(path);
    }

    private static void PatchManifest(string manifestPath)
    {
        XDocument document = XDocument.Load(manifestPath);
        XElement manifest = document.Root;

        if (manifest == null)
            return;

        foreach (XElement feature in manifest
                     .Elements("uses-feature")
                     .ToList())
        {
            string featureName =
                (string)feature.Attribute(AndroidNs + "name");

            if (featureName != null &&
                UnsupportedPcFeatures.Contains(featureName))
            {
                feature.Remove();
            }
        }

        foreach (XElement permission in manifest
                     .Elements()
                     .Where(element =>
                         element.Name.LocalName == "uses-permission" ||
                         element.Name.LocalName == "uses-permission-sdk-23")
                     .ToList())
        {
            string permissionName =
                (string)permission.Attribute(AndroidNs + "name");

            if (permissionName != null &&
                UnsupportedPcPermissions.Contains(permissionName))
            {
                permission.Remove();
            }
        }

        // Keep this as the LAST manifest mutation in this processor. GPG PC
        // only sends real mouse-move events (and therefore hover) when this
        // feature survives into the final merged manifest. Without it the
        // client translates left-clicks into touchscreen taps, which makes a
        // button look like it only reacts for the instant it is clicked.
        EnsureOptionalFeature(manifest, "android.hardware.type.pc");

        document.Save(manifestPath);
        Debug.Log($"[GPG PC] Cleaned PC manifest: {manifestPath}");
    }

    private static void PatchProguardRules(string gradleProjectPath)
    {
        string proguardPath = Path.Combine(
            gradleProjectPath,
            "unityLibrary",
            "proguard-unity.txt"
        );

        if (!File.Exists(proguardPath))
            return;

        const string marker =
            "# Fateful Rush - Google Play Games on PC Input SDK";

        string contents = File.ReadAllText(proguardPath);
        if (contents.Contains(marker))
            return;

        string rules =
            System.Environment.NewLine +
            marker + System.Environment.NewLine +
            "-keep class com.google.android.libraries.play.hpe.** { *; }" +
            System.Environment.NewLine +
            "-keep class com.google.android.libraries.play.games.inputmapping.** { *; }" +
            System.Environment.NewLine;

        File.AppendAllText(proguardPath, rules);
        Debug.Log(
            $"[GPG PC] Added Input SDK ProGuard keep rules: {proguardPath}"
        );
    }

    private static void EnsureOptionalFeature(
        XElement manifest,
        string featureName)
    {
        XElement feature = manifest
            .Elements("uses-feature")
            .FirstOrDefault(element =>
                (string)element.Attribute(AndroidNs + "name") == featureName);

        if (feature == null)
        {
            feature = new XElement(
                "uses-feature",
                new XAttribute(AndroidNs + "name", featureName),
                new XAttribute(AndroidNs + "required", "false")
            );

            manifest.AddFirst(feature);
            return;
        }

        feature.SetAttributeValue(AndroidNs + "required", "false");
    }
}
#endif
