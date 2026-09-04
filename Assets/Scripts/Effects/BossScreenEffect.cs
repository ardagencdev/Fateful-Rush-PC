using UnityEngine;

// Legacy serialization shim.
// The old pulsing boss-screen warning system is no longer used.
// Keep this component temporarily so existing scenes/prefabs that still
// reference its script GUID do not become Missing Script components.
[AddComponentMenu("")]
public sealed class BossScreenEffect : MonoBehaviour
{
}
