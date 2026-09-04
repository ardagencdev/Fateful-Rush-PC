using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDTransparency : MonoBehaviour
{
    [Range(0f, 1f)]
    public float alpha = 0.7f;

    [Header("Ignore")]
    public Graphic[] ignoreGraphics;

    private Graphic[] cachedGraphics;
    private HashSet<Graphic> ignoredGraphics;

    private void Awake()
    {
        CacheGraphics();
    }

    private void Start()
    {
        ApplyTransparency();
    }

    public void ApplyTransparency()
    {
        if (cachedGraphics == null)
            CacheGraphics();

        foreach (Graphic graphic in cachedGraphics)
        {
            if (graphic == null)
                continue;

            if (ignoredGraphics.Contains(graphic))
                continue;

            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }

    private void CacheGraphics()
    {
        cachedGraphics = GetComponentsInChildren<Graphic>(true);

        ignoredGraphics = new HashSet<Graphic>();

        if (ignoreGraphics == null)
            return;

        foreach (Graphic graphic in ignoreGraphics)
        {
            if (graphic != null)
                ignoredGraphics.Add(graphic);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        alpha = Mathf.Clamp01(alpha);
    }
#endif
}