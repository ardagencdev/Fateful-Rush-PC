using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight scene-local object pool for frequently reused runtime objects.
/// The pool object is destroyed automatically with the current scene, so stale
/// references cannot leak across gameplay reloads.
/// </summary>
public sealed class RuntimeObjectPool : MonoBehaviour
{
    private static RuntimeObjectPool instance;

    private readonly Dictionary<GameObject, Queue<GameObject>> pools =
        new Dictionary<GameObject, Queue<GameObject>>();


    private static RuntimeObjectPool Instance
    {
        get
        {
            if (instance != null)
                return instance;

            GameObject poolObject = new GameObject("Runtime Object Pool");
            instance = poolObject.AddComponent<RuntimeObjectPool>();
            return instance;
        }
    }

    public static void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0)
            return;

        Instance.PrewarmInternal(prefab, count);
    }

    public static GameObject Spawn(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation)
    {
        if (prefab == null)
            return null;

        return Instance.SpawnInternal(prefab, position, rotation);
    }

    public static void Release(GameObject instanceObject)
    {
        if (instanceObject == null)
            return;

        RuntimePooledObjectIdentity identity =
            instanceObject.GetComponent<RuntimePooledObjectIdentity>();

        if (identity == null ||
            identity.SourcePrefab == null ||
            instance == null)
        {
            Destroy(instanceObject);
            return;
        }

        instance.ReleaseInternal(instanceObject, identity.SourcePrefab);
    }

    private void PrewarmInternal(GameObject prefab, int count)
    {
        Queue<GameObject> queue = GetOrCreateQueue(prefab);

        int existingCount = queue.Count;
        int createCount = Mathf.Max(0, count - existingCount);

        for (int i = 0; i < createCount; i++)
        {
            GameObject item = CreateInstance(prefab);
            QueueInactive(item, prefab, queue);
        }
    }

    private GameObject SpawnInternal(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation)
    {
        Queue<GameObject> queue = GetOrCreateQueue(prefab);
        GameObject item = null;

        while (queue.Count > 0 && item == null)
        {
            item = queue.Dequeue();

            if (item != null)
            {
                RuntimePooledObjectIdentity identity =
                    item.GetComponent<RuntimePooledObjectIdentity>();

                if (identity != null)
                    identity.IsQueued = false;
            }
        }

        if (item == null)
            item = CreateInstance(prefab);

        Transform itemTransform = item.transform;
        itemTransform.SetParent(null, false);
        itemTransform.SetPositionAndRotation(position, rotation);

        item.SetActive(true);
        return item;
    }

    private void ReleaseInternal(
        GameObject item,
        GameObject sourcePrefab)
    {
        if (item == null || sourcePrefab == null)
            return;

        RuntimePooledObjectIdentity identity =
            item.GetComponent<RuntimePooledObjectIdentity>();

        if (identity == null)
            identity = item.AddComponent<RuntimePooledObjectIdentity>();

        if (identity.IsQueued)
            return;

        identity.SourcePrefab = sourcePrefab;
        item.SetActive(false);
        item.transform.SetParent(transform, false);

        Queue<GameObject> queue = GetOrCreateQueue(sourcePrefab);
        QueueInactive(item, sourcePrefab, queue);
    }

    private GameObject CreateInstance(GameObject prefab)
    {
        GameObject item = Instantiate(prefab, transform);

        RuntimePooledObjectIdentity identity = item.GetComponent<RuntimePooledObjectIdentity>();
        if (identity == null)
            identity = item.AddComponent<RuntimePooledObjectIdentity>();

        identity.SourcePrefab = prefab;
        item.SetActive(false);
        return item;
    }

    private void QueueInactive(
        GameObject item,
        GameObject prefab,
        Queue<GameObject> queue)
    {
        if (item == null)
            return;

        RuntimePooledObjectIdentity identity = item.GetComponent<RuntimePooledObjectIdentity>();
        if (identity == null)
            identity = item.AddComponent<RuntimePooledObjectIdentity>();

        identity.SourcePrefab = prefab;

        if (identity.IsQueued)
            return;

        identity.IsQueued = true;
        item.SetActive(false);
        item.transform.SetParent(transform, false);
        queue.Enqueue(item);
    }

    private Queue<GameObject> GetOrCreateQueue(GameObject prefab)
    {
        if (pools.TryGetValue(prefab, out Queue<GameObject> queue))
            return queue;

        queue = new Queue<GameObject>();
        pools.Add(prefab, queue);
        return queue;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        pools.Clear();
    }
}
