using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Conservatively removes raycast work from decorative text labels.
/// Images are intentionally left untouched because a transparent Image can be
/// used as a deliberate input blocker. Text that belongs to any interactive
/// parent is also preserved.
/// </summary>
public static class UIRaycastOptimizer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        OptimizeLoadedScene();

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        OptimizeLoadedScene();
    }

    private static void OptimizeLoadedScene()
    {
        Graphic[] graphics = UnityFindCompat.FindObjectsByType<Graphic>(
            FindObjectsInactive.Include
        );

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];

            if (graphic == null || !graphic.raycastTarget)
                continue;

            // Only text is changed automatically. This avoids breaking
            // invisible/transparent Images that intentionally block input.
            if (!(graphic is TMP_Text) && !(graphic is Text))
                continue;

            if (HasInteractiveObjectInParentChain(graphic.transform))
                continue;

            graphic.raycastTarget = false;
        }
    }

    private static bool HasInteractiveObjectInParentChain(Transform start)
    {
        Transform current = start;

        while (current != null)
        {
            GameObject target = current.gameObject;

            if (target.GetComponent<Selectable>() != null ||
                target.GetComponent<ScrollRect>() != null ||
                target.GetComponent<EventTrigger>() != null ||
                ImplementsPointerOrDragHandler(target))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool ImplementsPointerOrDragHandler(GameObject target)
    {
        MonoBehaviour[] behaviours = target.GetComponents<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];

            if (behaviour == null)
                continue;

            if (behaviour is IPointerClickHandler ||
                behaviour is IPointerDownHandler ||
                behaviour is IPointerUpHandler ||
                behaviour is IPointerEnterHandler ||
                behaviour is IPointerExitHandler ||
                behaviour is IBeginDragHandler ||
                behaviour is IDragHandler ||
                behaviour is IEndDragHandler ||
                behaviour is IScrollHandler ||
                behaviour is IDropHandler)
            {
                return true;
            }
        }

        return false;
    }
}
