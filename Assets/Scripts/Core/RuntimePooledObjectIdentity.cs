using UnityEngine;

/// <summary>
/// Runtime marker that remembers which prefab a pooled instance belongs to.
/// </summary>
public sealed class RuntimePooledObjectIdentity : MonoBehaviour
{
    public GameObject SourcePrefab { get; set; }
    public bool IsQueued { get; set; }
}
