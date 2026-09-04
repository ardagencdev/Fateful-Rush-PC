using TMPro;
using UnityEngine;

[DefaultExecutionOrder(10000)]
public class HUDPlayerOcclusionTMP : MonoBehaviour
{
    private HUDPlayerOcclusionController controller;
    private TMP_Text textComponent;
    private RectTransform rectTransform;

    private bool wasOccluded;

    public void Configure(
        HUDPlayerOcclusionController owner,
        TMP_Text targetText)
    {
        controller = owner;
        textComponent = targetText;
        rectTransform =
            targetText != null
                ? targetText.rectTransform
                : transform as RectTransform;

        if (textComponent != null)
            textComponent.SetVerticesDirty();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ResolveController();

        wasOccluded = false;

        if (textComponent != null)
            textComponent.SetVerticesDirty();
    }

    private void OnDisable()
    {
        RestoreBaseMesh();
        wasOccluded = false;
    }

    private void LateUpdate()
    {
        ResolveReferences();
        ResolveController();

        if (controller == null ||
            textComponent == null ||
            rectTransform == null)
        {
            return;
        }

        bool overlaps =
            controller.IsPotentiallyOverlapping(
                rectTransform
            );

        /*
         * TMP meshini yalnızca gerektiğinde yeniden kuruyoruz.
         * Player text alanına girdiğinde mesh temiz base renklerle yeniden
         * üretilir, lokal alpha uygulanır ve UpdateVertexData ile aynı frame
         * CanvasRenderer'a açıkça gönderilir.
         *
         * Bu yol Score / Timer / runtime LEVEL için aynıdır ve TMP'nin kendi
         * mesh yenileme zamanlamasına bağlı olmadığı için text/color update'leri
         * fade'i ezemez.
         */
        if (overlaps)
        {
            ApplyOcclusion();
            wasOccluded = true;
            return;
        }

        if (wasOccluded)
        {
            RestoreBaseMesh();
            wasOccluded = false;
        }
    }

    private void ApplyOcclusion()
    {
        if (textComponent == null ||
            controller == null ||
            rectTransform == null)
        {
            return;
        }

        textComponent.ForceMeshUpdate(false, false);

        TMP_TextInfo textInfo =
            textComponent.textInfo;

        if (textInfo == null ||
            textInfo.meshInfo == null)
        {
            return;
        }

        byte baseAlpha =
            (byte)Mathf.RoundToInt(
                Mathf.Clamp01(textComponent.color.a) * 255f
            );

        for (int i = 0;
             i < textInfo.characterCount;
             i++)
        {
            TMP_CharacterInfo characterInfo =
                textInfo.characterInfo[i];

            if (!characterInfo.isVisible)
                continue;

            int materialIndex =
                characterInfo.materialReferenceIndex;

            int vertexIndex =
                characterInfo.vertexIndex;

            if (materialIndex < 0 ||
                materialIndex >= textInfo.meshInfo.Length)
            {
                continue;
            }

            TMP_MeshInfo meshInfo =
                textInfo.meshInfo[materialIndex];

            Vector3[] vertices = meshInfo.vertices;
            Color32[] colors = meshInfo.colors32;

            if (vertices == null ||
                colors == null)
            {
                continue;
            }

            for (int corner = 0;
                 corner < 4;
                 corner++)
            {
                int index = vertexIndex + corner;

                if (index < 0 ||
                    index >= vertices.Length ||
                    index >= colors.Length)
                {
                    continue;
                }

                Vector3 worldPoint =
                    rectTransform.TransformPoint(
                        vertices[index]
                    );

                Vector2 screenPoint =
                    controller.WorldToUIScreenPoint(
                        worldPoint
                    );

                float alphaMultiplier =
                    controller.GetAlphaMultiplier(
                        screenPoint
                    );

                Color32 color = colors[index];

                color.a =
                    (byte)Mathf.RoundToInt(
                        baseAlpha * alphaMultiplier
                    );

                colors[index] = color;
            }
        }

        textComponent.UpdateVertexData(
            TMP_VertexDataUpdateFlags.Colors32
        );
    }

    private void RestoreBaseMesh()
    {
        if (textComponent == null ||
            !textComponent.gameObject.activeInHierarchy)
        {
            return;
        }

        textComponent.ForceMeshUpdate(false, false);
    }

    private void ResolveReferences()
    {
        if (textComponent == null)
            textComponent = GetComponent<TMP_Text>();

        if (rectTransform == null &&
            textComponent != null)
        {
            rectTransform = textComponent.rectTransform;
        }
    }

    private void ResolveController()
    {
        if (controller != null)
            return;

        controller =
            FindAnyObjectByType<HUDPlayerOcclusionController>();
    }
}
