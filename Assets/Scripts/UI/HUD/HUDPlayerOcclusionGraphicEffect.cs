using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Graphic))]
public class HUDPlayerOcclusionGraphicEffect : BaseMeshEffect
{
    private HUDPlayerOcclusionController controller;
    private Graphic targetGraphic;
    private RectTransform rectTransform;
    private bool wasOverlapping;

    private readonly List<UIVertex> sourceStream =
        new List<UIVertex>(96);

    private readonly List<UIVertex> outputStream =
        new List<UIVertex>(384);

    public void Configure(
        HUDPlayerOcclusionController owner,
        Graphic graphic)
    {
        controller = owner;
        targetGraphic = graphic;
        rectTransform =
            graphic != null
                ? graphic.rectTransform
                : transform as RectTransform;

        if (targetGraphic != null)
            targetGraphic.SetVerticesDirty();
    }

    private void LateUpdate()
    {
        if (controller == null ||
            targetGraphic == null ||
            rectTransform == null)
        {
            return;
        }

        bool overlaps =
            controller.IsPotentiallyOverlapping(
                rectTransform
            );

        if (overlaps || wasOverlapping)
            targetGraphic.SetVerticesDirty();

        wasOverlapping = overlaps;
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (targetGraphic != null)
            targetGraphic.SetVerticesDirty();

        wasOverlapping = false;
    }

    public override void ModifyMesh(
        VertexHelper vertexHelper)
    {
        if (!IsActive() ||
            controller == null ||
            rectTransform == null ||
            vertexHelper == null ||
            vertexHelper.currentVertCount == 0)
        {
            return;
        }

        if (!controller.IsPotentiallyOverlapping(
                rectTransform
            ))
        {
            return;
        }

        sourceStream.Clear();
        outputStream.Clear();

        vertexHelper.GetUIVertexStream(
            sourceStream
        );

        if (sourceStream.Count < 3)
            return;

        int subdivisionDepth =
            controller.GraphicSubdivisionDepth;

        for (int i = 0;
             i + 2 < sourceStream.Count;
             i += 3)
        {
            AddSubdividedTriangle(
                sourceStream[i],
                sourceStream[i + 1],
                sourceStream[i + 2],
                subdivisionDepth
            );
        }

        for (int i = 0;
             i < outputStream.Count;
             i++)
        {
            UIVertex vertex =
                outputStream[i];

            Vector3 worldPoint =
                rectTransform.TransformPoint(
                    vertex.position
                );

            Vector2 screenPoint =
                controller.WorldToUIScreenPoint(
                    worldPoint
                );

            float alphaMultiplier =
                controller.GetAlphaMultiplier(
                    screenPoint
                );

            Color32 color = vertex.color;

            color.a = (byte)Mathf.RoundToInt(
                color.a * alphaMultiplier
            );

            vertex.color = color;
            outputStream[i] = vertex;
        }

        vertexHelper.Clear();
        vertexHelper.AddUIVertexTriangleStream(
            outputStream
        );
    }

    private void AddSubdividedTriangle(
        UIVertex a,
        UIVertex b,
        UIVertex c,
        int depth)
    {
        if (depth <= 0)
        {
            outputStream.Add(a);
            outputStream.Add(b);
            outputStream.Add(c);
            return;
        }

        UIVertex ab = LerpVertex(a, b, 0.5f);
        UIVertex bc = LerpVertex(b, c, 0.5f);
        UIVertex ca = LerpVertex(c, a, 0.5f);

        int nextDepth = depth - 1;

        AddSubdividedTriangle(
            a,
            ab,
            ca,
            nextDepth
        );

        AddSubdividedTriangle(
            ab,
            b,
            bc,
            nextDepth
        );

        AddSubdividedTriangle(
            ca,
            bc,
            c,
            nextDepth
        );

        AddSubdividedTriangle(
            ab,
            bc,
            ca,
            nextDepth
        );
    }

    private static UIVertex LerpVertex(
        UIVertex a,
        UIVertex b,
        float t)
    {
        UIVertex result = a;

        result.position = Vector3.Lerp(
            a.position,
            b.position,
            t
        );

        result.normal = Vector3.Lerp(
            a.normal,
            b.normal,
            t
        ).normalized;

        result.tangent = Vector4.Lerp(
            a.tangent,
            b.tangent,
            t
        );

        result.uv0 = Vector4.Lerp(
            a.uv0,
            b.uv0,
            t
        );

        result.uv1 = Vector4.Lerp(
            a.uv1,
            b.uv1,
            t
        );

        result.uv2 = Vector4.Lerp(
            a.uv2,
            b.uv2,
            t
        );

        result.uv3 = Vector4.Lerp(
            a.uv3,
            b.uv3,
            t
        );

        Color colorA = a.color;
        Color colorB = b.color;

        result.color = (Color32)Color.Lerp(
            colorA,
            colorB,
            t
        );

        return result;
    }
}
