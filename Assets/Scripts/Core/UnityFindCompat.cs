// Shared compatibility wrapper for Fateful Rush.
// Android master uses Unity 6000.5; Google Play Games on PC uses Unity 6000.3.
// Keep object-search behavior identical while compiling against the API preferred by each Editor.

public static class UnityFindCompat
{
    public static T[] FindObjectsByType<T>() where T : UnityEngine.Object
    {
#if UNITY_6000_5_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<T>();
#else
        return UnityEngine.Object.FindObjectsByType<T>(UnityEngine.FindObjectsSortMode.None);
#endif
    }

    public static T[] FindObjectsByType<T>(UnityEngine.FindObjectsInactive inactive) where T : UnityEngine.Object
    {
#if UNITY_6000_5_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<T>(inactive);
#else
        return UnityEngine.Object.FindObjectsByType<T>(inactive, UnityEngine.FindObjectsSortMode.None);
#endif
    }
}
