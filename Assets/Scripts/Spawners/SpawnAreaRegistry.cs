using System.Collections.Generic;
using UnityEngine;

public static class SpawnAreaRegistry
{
    private struct Entry
    {
        public Transform transform;
        public float radius;

        public Entry(Transform transform, float radius)
        {
            this.transform = transform;
            this.radius = radius;
        }
    }

    private static readonly List<Entry> entries =
        new List<Entry>(64);

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        entries.Clear();
    }

    public static bool IsAreaFree(
        Vector2 position,
        float radius)
    {
        Cleanup();

        radius = Mathf.Max(0f, radius);

        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];

            if (entry.transform == null)
                continue;

            float combinedRadius =
                radius + entry.radius;

            Vector2 registeredPosition =
                entry.transform.position;

            if ((registeredPosition - position).sqrMagnitude <
                combinedRadius * combinedRadius)
            {
                return false;
            }
        }

        return true;
    }

    public static void Register(
        GameObject obj,
        float radius)
    {
        if (obj == null)
            return;

        Cleanup();

        Transform targetTransform = obj.transform;
        radius = Mathf.Max(0f, radius);

        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];

            if (entry.transform != targetTransform)
                continue;

            entries[i] = new Entry(
                targetTransform,
                radius
            );

            return;
        }

        entries.Add(
            new Entry(
                targetTransform,
                radius
            )
        );
    }

    public static void Unregister(GameObject obj)
    {
        if (obj == null)
            return;

        Transform targetTransform = obj.transform;

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].transform == null ||
                entries[i].transform == targetTransform)
            {
                entries.RemoveAt(i);
            }
        }
    }

    public static void Clear()
    {
        entries.Clear();
    }

    private static void Cleanup()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].transform == null)
                entries.RemoveAt(i);
        }
    }
}