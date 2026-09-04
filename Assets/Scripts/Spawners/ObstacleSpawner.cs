using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObstacleSpawner : MonoBehaviour
{
    private const int MaximumObstaclesPerLevel = 5;
    private const int PlacementGridSteps = 21;
    private const int MaximumCandidatePositionsPerObstacle = 560;
    private const int LayoutRelaxationPasses = 3;
    private const int RandomSetRerolls = 4;

    private const float MinimumEdgeClearance = 0.35f;
    private const float MinimumPlayerEdgeClearance = 0.35f;
    private const float MinimumObstacleEdgeClearance = 0.05f;

    [Header("Obstacle Mode")]
    public ObstacleSpawnMode obstacleSpawnMode =
        ObstacleSpawnMode.Fixed;

    [Header("Level Obstacles")]
    public LevelObstacleOption[] levelObstacles;

    [Header("Random Obstacles")]
    [Range(0, MaximumObstaclesPerLevel)]
    public int randomObstacleCount = 5;

    [Header("Spawn Settings")]
    [Tooltip(
        "Minimum center-to-center distance. " +
        "Real obstacle footprints are checked separately."
    )]
    [Min(0f)]
    public float minDistanceBetweenObstacles = 1.2f;

    [Tooltip(
        "Minimum center-to-center distance from the player. " +
        "Real obstacle footprint clearance is checked separately."
    )]
    [Min(0f)]
    public float playerSafeDistance = 3.5f;

    [Min(0f)]
    public float edgePadding = 0.6f;

    [Tooltip(
        "Preferred clearance between the obstacle footprint and " +
        "CameraWorldBounds. If a complete layout cannot fit, " +
        "only this soft margin is gradually relaxed."
    )]
    [Min(0f)]
    public float arenaEdgeClearance = 1.25f;

    [Tooltip(
        "Fallback size only when a prefab has no usable Collider2D geometry."
    )]
    [Min(0f)]
    public float checkRadius = 0.8f;

    [Tooltip(
        "Small safety expansion added to the real collider footprint."
    )]
    [Min(0f)]
    public float footprintPadding = 0.08f;

    [Tooltip(
        "Extra random samples in addition to deterministic grid samples."
    )]
    [Min(1)]
    public int maxAttempts = 120;

    [Tooltip(
        "Maximum backtracking work per layout pass."
    )]
    [Min(1000)]
    public int maxLayoutSearchNodes = 30000;

    [Header("References")]
    public Transform player;

    [Header("Intro Popups")]
    [Min(0f)]
    public float obstaclePopupGap = 0.04f;

    [Header("Diagnostics")]
    public bool logPlacementDiagnostics;

    private struct PrefabFootprint
    {
        public Vector2 CenterOffset;
        public Vector2 HalfExtents;
    }

    private sealed class ObstacleCandidate
    {
        public GameObject Prefab;
        public PrefabFootprint Footprint;

        public readonly List<Vector2> Positions =
            new List<Vector2>(
                MaximumCandidatePositionsPerObstacle
            );
    }

    private struct PlannedPlacement
    {
        public ObstacleCandidate Candidate;
        public Vector2 RootPosition;
    }

    private int obstacleLayerIndex;
    private int wallLayerIndex;

    private ContactFilter2D spawnFilter;

    private readonly Collider2D[] spawnHits =
        new Collider2D[64];

    private readonly List<GameObject> spawnedObstacles =
        new List<GameObject>(
            MaximumObstaclesPerLevel
        );

    private readonly List<PlannedPlacement> plannedPlacements =
        new List<PlannedPlacement>(
            MaximumObstaclesPerLevel
        );

    private readonly List<PlannedPlacement> bestPlacements =
        new List<PlannedPlacement>(
            MaximumObstaclesPerLevel
        );

    private readonly Dictionary<GameObject, PrefabFootprint> footprintCache =
        new Dictionary<GameObject, PrefabFootprint>();

    private int layoutSearchNodes;
    private float activeEdgeClearance;

    private void Awake()
    {
        obstacleLayerIndex =
            LayerMask.NameToLayer("Obstacle");

        wallLayerIndex =
            LayerMask.NameToLayer("Wall");

        spawnFilter =
            ContactFilter2D.noFilter;

        spawnFilter.useTriggers = true;
    }

    public void SpawnObstacles()
    {
        CameraWorldBounds bounds =
            CameraWorldBounds.Instance;

        if (bounds == null)
        {
            Debug.LogWarning(
                "[ObstacleSpawner] CameraWorldBounds bulunamadı.",
                this
            );

            return;
        }

        if (levelObstacles == null ||
            levelObstacles.Length == 0)
        {
            return;
        }

        spawnedObstacles.Clear();
        plannedPlacements.Clear();
        bestPlacements.Clear();

        int setAttempts =
            obstacleSpawnMode ==
            ObstacleSpawnMode.Random
                ? RandomSetRerolls
                : 1;

        int requestedCount = 0;

        List<PlannedPlacement> bestOverall =
            new List<PlannedPlacement>(
                MaximumObstaclesPerLevel
            );

        for (int attempt = 0;
             attempt < setAttempts;
             attempt++)
        {
            List<ObstacleCandidate> candidates =
                obstacleSpawnMode ==
                ObstacleSpawnMode.Random
                    ? BuildRandomCandidateSet()
                    : BuildFixedCandidateSet();

            if (candidates.Count == 0)
                return;

            requestedCount =
                candidates.Count;

            bool complete =
                TryPlanLayout(candidates);

            if (plannedPlacements.Count >
                bestOverall.Count)
            {
                bestOverall.Clear();

                bestOverall.AddRange(
                    plannedPlacements
                );
            }

            if (complete)
            {
                InstantiateLayout(
                    plannedPlacements
                );

                return;
            }
        }

        if (bestOverall.Count > 0)
        {
            InstantiateLayout(
                bestOverall
            );

            Debug.LogWarning(
                $"[ObstacleSpawner] Complete layout could not fit. " +
                $"Spawned best valid subset: " +
                $"{bestOverall.Count}/{requestedCount}. " +
                "This means the selected prefab sizes and safety " +
                "rules are physically incompatible with the current arena.",
                this
            );
        }
        else
        {
            Debug.LogWarning(
                $"[ObstacleSpawner] No valid obstacle position exists. " +
                $"Arena={bounds.Width:0.00}x{bounds.Height:0.00}. " +
                "Enable diagnostics to inspect prefab footprints.",
                this
            );
        }
    }

    private List<ObstacleCandidate>
        BuildFixedCandidateSet()
    {
        List<ObstacleCandidate> result =
            new List<ObstacleCandidate>();

        foreach (
            LevelObstacleOption option
            in levelObstacles
        )
        {
            if (option == null ||
                !option.enabled ||
                option.prefab == null)
            {
                continue;
            }

            result.Add(
                CreateCandidate(
                    option.prefab
                )
            );
        }

        SortLargestFirst(
            result
        );

        if (result.Count >
            MaximumObstaclesPerLevel)
        {
            result.RemoveRange(
                MaximumObstaclesPerLevel,
                result.Count -
                MaximumObstaclesPerLevel
            );
        }

        return result;
    }

    private List<ObstacleCandidate>
        BuildRandomCandidateSet()
    {
        List<GameObject> pool =
            new List<GameObject>();

        foreach (
            LevelObstacleOption option
            in levelObstacles
        )
        {
            if (option == null ||
                !option.enabled ||
                option.prefab == null)
            {
                continue;
            }

            if (!pool.Contains(
                    option.prefab))
            {
                pool.Add(
                    option.prefab
                );
            }
        }

        Shuffle(
            pool
        );

        int count =
            Mathf.Min(
                randomObstacleCount,
                pool.Count,
                MaximumObstaclesPerLevel
            );

        List<ObstacleCandidate> result =
            new List<ObstacleCandidate>(
                count
            );

        for (int i = 0;
             i < count;
             i++)
        {
            result.Add(
                CreateCandidate(
                    pool[i]
                )
            );
        }

        SortLargestFirst(
            result
        );

        return result;
    }

    private ObstacleCandidate
        CreateCandidate(
            GameObject prefab)
    {
        return new ObstacleCandidate
        {
            Prefab = prefab,
            Footprint =
                GetPrefabFootprint(
                    prefab
                )
        };
    }

    private bool TryPlanLayout(
        List<ObstacleCandidate> candidates)
    {
        bestPlacements.Clear();

        float preferredEdge =
            Mathf.Max(
                edgePadding,
                arenaEdgeClearance
            );

        float minimumEdge =
            Mathf.Min(
                preferredEdge,
                MinimumEdgeClearance
            );

        for (int pass = 0;
             pass <
             LayoutRelaxationPasses;
             pass++)
        {
            float t =
                LayoutRelaxationPasses == 1
                    ? 0f
                    : pass /
                      (float)(
                          LayoutRelaxationPasses -
                          1
                      );

            activeEdgeClearance =
                Mathf.Lerp(
                    preferredEdge,
                    minimumEdge,
                    t
                );

            for (int i = 0;
                 i < candidates.Count;
                 i++)
            {
                BuildPositionPool(
                    candidates[i]
                );

                if (logPlacementDiagnostics)
                {
                    Debug.Log(
                        $"[ObstacleSpawner] " +
                        $"{candidates[i].Prefab.name} | " +
                        $"size=" +
                        $"{candidates[i].Footprint.HalfExtents * 2f} | " +
                        $"positions=" +
                        $"{candidates[i].Positions.Count} | " +
                        $"edge=" +
                        $"{activeEdgeClearance:0.00}",
                        this
                    );
                }
            }

            candidates.Sort(
                (a, b) =>
                {
                    int compare =
                        a.Positions.Count
                            .CompareTo(
                                b.Positions.Count
                            );

                    if (compare != 0)
                        return compare;

                    float areaA =
                        a.Footprint
                            .HalfExtents.x *
                        a.Footprint
                            .HalfExtents.y;

                    float areaB =
                        b.Footprint
                            .HalfExtents.x *
                        b.Footprint
                            .HalfExtents.y;

                    return areaB
                        .CompareTo(
                            areaA
                        );
                }
            );

            plannedPlacements.Clear();

            layoutSearchNodes = 0;

            if (TryPlaceRecursive(
                    candidates,
                    0))
            {
                if (logPlacementDiagnostics)
                {
                    Debug.Log(
                        $"[ObstacleSpawner] Complete layout solved: " +
                        $"{plannedPlacements.Count}/" +
                        $"{candidates.Count}, " +
                        $"edge={activeEdgeClearance:0.00}, " +
                        $"nodes={layoutSearchNodes}.",
                        this
                    );
                }

                return true;
            }
        }

        plannedPlacements.Clear();

        plannedPlacements.AddRange(
            bestPlacements
        );

        return false;
    }

    private bool TryPlaceRecursive(
        List<ObstacleCandidate> candidates,
        int index)
    {
        SaveBestLayout();

        if (index >=
            candidates.Count)
        {
            return
                plannedPlacements.Count ==
                candidates.Count;
        }

        if (layoutSearchNodes >=
            maxLayoutSearchNodes)
        {
            return false;
        }

        ObstacleCandidate candidate =
            candidates[index];

        for (int i = 0;
             i <
             candidate.Positions.Count;
             i++)
        {
            if (layoutSearchNodes++ >=
                maxLayoutSearchNodes)
            {
                break;
            }

            Vector2 position =
                candidate.Positions[i];

            if (!IsCompatibleWithPlannedLayout(
                    position,
                    candidate.Footprint))
            {
                continue;
            }

            plannedPlacements.Add(
                new PlannedPlacement
                {
                    Candidate =
                        candidate,

                    RootPosition =
                        position
                }
            );

            SaveBestLayout();

            if (TryPlaceRecursive(
                    candidates,
                    index + 1))
            {
                return true;
            }

            plannedPlacements.RemoveAt(
                plannedPlacements.Count - 1
            );
        }

        // Bir prefab gerçekten sığmıyorsa
        // diğer valid obstacle'ları çöpe atma.
        return TryPlaceRecursive(
            candidates,
            index + 1
        );
    }

    private void SaveBestLayout()
    {
        if (plannedPlacements.Count <=
            bestPlacements.Count)
        {
            return;
        }

        bestPlacements.Clear();

        bestPlacements.AddRange(
            plannedPlacements
        );
    }

    private void BuildPositionPool(
        ObstacleCandidate candidate)
    {
        candidate.Positions.Clear();

        if (!TryGetRootLimits(
                candidate.Footprint,
                out float minX,
                out float maxX,
                out float minY,
                out float maxY))
        {
            return;
        }

        HashSet<long> unique =
            new HashSet<long>();

        // Önce random sample:
        // başarılı layoutlar fazla grid gibi görünmesin.
        for (int i = 0;
             i < maxAttempts &&
             candidate.Positions.Count <
             MaximumCandidatePositionsPerObstacle;
             i++)
        {
            Vector2 position =
                new Vector2(
                    Random.Range(
                        minX,
                        maxX
                    ),
                    Random.Range(
                        minY,
                        maxY
                    )
                );

            TryAddPosition(
                candidate,
                position,
                unique
            );
        }

        // Ardından tüm kullanılabilir alanı
        // deterministic grid ile tara.
        for (int y = 0;
             y < PlacementGridSteps &&
             candidate.Positions.Count <
             MaximumCandidatePositionsPerObstacle;
             y++)
        {
            for (int x = 0;
                 x < PlacementGridSteps &&
                 candidate.Positions.Count <
                 MaximumCandidatePositionsPerObstacle;
                 x++)
            {
                float tx =
                    (x + 0.5f) /
                    PlacementGridSteps;

                float ty =
                    (y + 0.5f) /
                    PlacementGridSteps;

                Vector2 position =
                    new Vector2(
                        Mathf.Lerp(
                            minX,
                            maxX,
                            tx
                        ),
                        Mathf.Lerp(
                            minY,
                            maxY,
                            ty
                        )
                    );

                TryAddPosition(
                    candidate,
                    position,
                    unique
                );
            }
        }

        Shuffle(
            candidate.Positions
        );
    }

    private void TryAddPosition(
        ObstacleCandidate candidate,
        Vector2 position,
        HashSet<long> unique)
    {
        if (!IsStaticPositionValid(
                position,
                candidate.Footprint))
        {
            return;
        }

        int qx =
            Mathf.RoundToInt(
                position.x * 25f
            );

        int qy =
            Mathf.RoundToInt(
                position.y * 25f
            );

        long key =
            ((long)qx << 32) ^
            (uint)qy;

        if (unique.Add(key))
        {
            candidate.Positions.Add(
                position
            );
        }
    }

    private bool TryGetRootLimits(
        PrefabFootprint footprint,
        out float minX,
        out float maxX,
        out float minY,
        out float maxY)
    {
        CameraWorldBounds bounds =
            CameraWorldBounds.Instance;

        if (bounds == null)
        {
            minX = 0f;
            maxX = 0f;
            minY = 0f;
            maxY = 0f;

            return false;
        }

        Vector2 halfExtents =
            footprint.HalfExtents;

        Vector2 offset =
            footprint.CenterOffset;

        minX =
            bounds.MinX +
            activeEdgeClearance +
            halfExtents.x -
            offset.x;

        maxX =
            bounds.MaxX -
            activeEdgeClearance -
            halfExtents.x -
            offset.x;

        minY =
            bounds.MinY +
            activeEdgeClearance +
            halfExtents.y -
            offset.y;

        maxY =
            bounds.MaxY -
            activeEdgeClearance -
            halfExtents.y -
            offset.y;

        return
            minX <= maxX &&
            minY <= maxY;
    }

    private bool IsStaticPositionValid(
        Vector2 rootPosition,
        PrefabFootprint footprint)
    {
        Vector2 center =
            rootPosition +
            footprint.CenterOffset;

        Vector2 halfExtents =
            footprint.HalfExtents;

        if (player != null)
        {
            Vector2 playerPosition =
                player.position;

            float centerSafe =
                Mathf.Max(
                    0f,
                    playerSafeDistance
                );

            if ((center -
                 playerPosition)
                    .sqrMagnitude <
                centerSafe *
                centerSafe)
            {
                return false;
            }

            // Player ile collider'ın gerçek kenarı
            // arasında da minimum güvenlik payı bırak.
            if (DistancePointToAabb(
                    playerPosition,
                    center,
                    halfExtents) <
                MinimumPlayerEdgeClearance)
            {
                return false;
            }
        }

        Vector2 queryHalfExtents =
            new Vector2(
                Mathf.Max(
                    0.02f,
                    halfExtents.x -
                    0.01f
                ),
                Mathf.Max(
                    0.02f,
                    halfExtents.y -
                    0.01f
                )
            );

        int hitCount =
            Physics2D.OverlapBox(
                center,
                queryHalfExtents * 2f,
                0f,
                spawnFilter,
                spawnHits
            );

        for (int i = 0;
             i < hitCount;
             i++)
        {
            Collider2D hit =
                spawnHits[i];

            if (hit != null &&
                IsBlocked(hit))
            {
                return false;
            }
        }

        return true;
    }

    private bool
        IsCompatibleWithPlannedLayout(
            Vector2 rootPosition,
            PrefabFootprint footprint)
    {
        Vector2 center =
            rootPosition +
            footprint.CenterOffset;

        Vector2 halfExtents =
            footprint.HalfExtents;

        for (int i = 0;
             i <
             plannedPlacements.Count;
             i++)
        {
            PlannedPlacement other =
                plannedPlacements[i];

            Vector2 otherCenter =
                other.RootPosition +
                other.Candidate
                    .Footprint
                    .CenterOffset;

            Vector2 otherHalfExtents =
                other.Candidate
                    .Footprint
                    .HalfExtents;

            // Gerçek collider footprintleri
            // birbirinin içine giremez.
            float edgeDistance =
                DistanceBetweenAabbs(
                    center,
                    halfExtents,
                    otherCenter,
                    otherHalfExtents
                );

            if (edgeDistance <
                MinimumObstacleEdgeClearance)
            {
                return false;
            }

            // Inspector'daki eski 1.2 değeri artık tekrar
            // mantıklı şekilde center-to-center.
            float centerGap =
                Mathf.Max(
                    0f,
                    minDistanceBetweenObstacles
                );

            if ((center -
                 otherCenter)
                    .sqrMagnitude <
                centerGap *
                centerGap)
            {
                return false;
            }
        }

        return true;
    }

    private void InstantiateLayout(
        List<PlannedPlacement> layout)
    {
        spawnedObstacles.Clear();

        for (int i = 0;
             i < layout.Count;
             i++)
        {
            PlannedPlacement placement =
                layout[i];

            GameObject spawned =
                Instantiate(
                    placement
                        .Candidate
                        .Prefab,
                    placement
                        .RootPosition,
                    Quaternion.identity
                );

            spawnedObstacles.Add(
                spawned
            );
        }

        // Aynı frame içindeki Physics2D sorgularını
        // güncel transformlarla senkronize et.
        Physics2D.SyncTransforms();

        if (logPlacementDiagnostics)
        {
            Debug.Log(
                $"[ObstacleSpawner] Spawned " +
                $"{spawnedObstacles.Count} obstacle(s).",
                this
            );
        }
    }

    private PrefabFootprint
        GetPrefabFootprint(
            GameObject prefab)
    {
        if (prefab == null)
        {
            return new PrefabFootprint
            {
                CenterOffset = Vector2.zero,
                HalfExtents = Vector2.one * Mathf.Max(0.05f, checkRadius)
            };
        }

        if (footprintCache.TryGetValue(prefab, out PrefabFootprint cached))
            return cached;

        Transform root =
            prefab.transform;

        Collider2D[] colliders =
            prefab
                .GetComponentsInChildren
                <Collider2D>(true);

        bool found = false;

        Vector2 min =
            Vector2.zero;

        Vector2 max =
            Vector2.zero;

        foreach (
            Collider2D collider
            in colliders)
        {
            if (collider == null ||
                !collider.enabled)
            {
                continue;
            }

            if (collider is
                PolygonCollider2D polygon)
            {
                AddPolygonCollider(
                    root,
                    polygon,
                    ref found,
                    ref min,
                    ref max
                );
            }
            else if (collider is
                     CircleCollider2D circle)
            {
                AddCircleCollider(
                    root,
                    circle,
                    ref found,
                    ref min,
                    ref max
                );
            }
            else if (collider is
                     CapsuleCollider2D capsule)
            {
                AddRect(
                    root,
                    capsule.transform,
                    capsule.offset,
                    capsule.size * 0.5f,
                    ref found,
                    ref min,
                    ref max
                );
            }
            else if (collider is
                     BoxCollider2D box)
            {
                Vector2 half =
                    box.size *
                    0.5f +
                    Vector2.one *
                    box.edgeRadius;

                AddRect(
                    root,
                    box.transform,
                    box.offset,
                    half,
                    ref found,
                    ref min,
                    ref max
                );
            }
        }

        if (!found)
        {
            float fallback =
                Mathf.Max(
                    0.05f,
                    checkRadius
                );

            PrefabFootprint fallbackFootprint = new PrefabFootprint
            {
                CenterOffset = Vector2.zero,
                HalfExtents = Vector2.one * fallback
            };

            footprintCache[prefab] = fallbackFootprint;
            return fallbackFootprint;
        }

        Vector2 center =
            (min + max) *
            0.5f;

        Vector2 halfExtents =
            (max - min) *
            0.5f;

        halfExtents +=
            Vector2.one *
            footprintPadding;

        halfExtents.x =
            Mathf.Max(
                0.05f,
                halfExtents.x
            );

        halfExtents.y =
            Mathf.Max(
                0.05f,
                halfExtents.y
            );

        PrefabFootprint footprint = new PrefabFootprint
        {
            CenterOffset = center,
            HalfExtents = halfExtents
        };

        footprintCache[prefab] = footprint;
        return footprint;
    }

    private static void AddPolygonCollider(
        Transform root,
        PolygonCollider2D polygon,
        ref bool found,
        ref Vector2 min,
        ref Vector2 max)
    {
        for (int pathIndex = 0;
             pathIndex <
             polygon.pathCount;
             pathIndex++)
        {
            Vector2[] path =
                polygon.GetPath(
                    pathIndex
                );

            for (int i = 0;
                 i < path.Length;
                 i++)
            {
                AddSpawnSpacePoint(
                    root,
                    polygon.transform,
                    path[i] +
                    polygon.offset,
                    ref found,
                    ref min,
                    ref max
                );
            }
        }
    }

    private static void AddCircleCollider(
        Transform root,
        CircleCollider2D circle,
        ref bool found,
        ref Vector2 min,
        ref Vector2 max)
    {
        const int Samples = 16;

        for (int i = 0;
             i < Samples;
             i++)
        {
            float angle =
                i *
                Mathf.PI *
                2f /
                Samples;

            Vector2 direction =
                new Vector2(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle)
                );

            Vector2 point =
                circle.offset +
                direction *
                circle.radius;

            AddSpawnSpacePoint(
                root,
                circle.transform,
                point,
                ref found,
                ref min,
                ref max
            );
        }
    }

    private static void AddRect(
        Transform root,
        Transform source,
        Vector2 center,
        Vector2 half,
        ref bool found,
        ref Vector2 min,
        ref Vector2 max)
    {
        AddSpawnSpacePoint(
            root,
            source,
            center +
            new Vector2(
                -half.x,
                -half.y
            ),
            ref found,
            ref min,
            ref max
        );

        AddSpawnSpacePoint(
            root,
            source,
            center +
            new Vector2(
                -half.x,
                half.y
            ),
            ref found,
            ref min,
            ref max
        );

        AddSpawnSpacePoint(
            root,
            source,
            center +
            new Vector2(
                half.x,
                -half.y
            ),
            ref found,
            ref min,
            ref max
        );

        AddSpawnSpacePoint(
            root,
            source,
            center +
            new Vector2(
                half.x,
                half.y
            ),
            ref found,
            ref min,
            ref max
        );
    }

    private static void AddSpawnSpacePoint(
        Transform root,
        Transform source,
        Vector2 localPoint,
        ref bool found,
        ref Vector2 min,
        ref Vector2 max)
    {
        Vector3 currentWorld =
            source.TransformPoint(
                localPoint
            );

        Vector3 rootLocal =
            root.InverseTransformPoint(
                currentWorld
            );

        // Instantiate(... Quaternion.identity) root rotationı
        // sıfırlıyor ama prefab scale'ini koruyor.
        // Footprint'i tam bu spawn şekline göre hesapla.
        Vector2 point =
            new Vector2(
                rootLocal.x *
                root.localScale.x,

                rootLocal.y *
                root.localScale.y
            );

        if (!found)
        {
            min = point;
            max = point;
            found = true;

            return;
        }

        min =
            Vector2.Min(
                min,
                point
            );

        max =
            Vector2.Max(
                max,
                point
            );
    }

    public IEnumerator
        PlaySpawnedObstaclePopupsAndWait()
    {
        if (spawnedObstacles.Count == 0)
            yield break;

        List<GameObject> popupList =
            new List<GameObject>(
                spawnedObstacles
            );

        Shuffle(
            popupList
        );

        foreach (
            GameObject obstacle
            in popupList)
        {
            if (obstacle == null)
                continue;

            SpawnPopEffect effect =
                obstacle
                    .GetComponent
                    <SpawnPopEffect>();

            if (effect != null)
            {
                yield return
                    effect.PlayAndWait();
            }
            else if (
                obstacle
                    .transform
                    .localScale ==
                Vector3.zero)
            {
                obstacle
                    .transform
                    .localScale =
                    Vector3.one;
            }

            if (obstaclePopupGap >
                0f)
            {
                yield return
                    new WaitForSecondsRealtime(
                        obstaclePopupGap
                    );
            }
        }
    }

    public void
        HideSpawnedObstaclesInstant()
    {
        foreach (
            GameObject obstacle
            in spawnedObstacles)
        {
            if (obstacle == null)
                continue;

            SpawnPopEffect effect =
                obstacle
                    .GetComponent
                    <SpawnPopEffect>();

            if (effect != null)
            {
                effect.HideInstant();
            }
        }
    }

    public void ClearObstacles()
    {
        foreach (
            GameObject obstacle
            in spawnedObstacles)
        {
            if (obstacle == null)
                continue;

            // Destroy frame sonunda çalışır.
            // Aynı frame'de eski collider'ı blocker olarak görme.
            obstacle.SetActive(
                false
            );

            Destroy(
                obstacle
            );
        }

        spawnedObstacles.Clear();
        plannedPlacements.Clear();
        bestPlacements.Clear();
    }

    private bool IsBlocked(
        Collider2D hit)
    {
        GameObject obj =
            hit.gameObject;

        return
            hit.CompareTag("Coin") ||
            hit.CompareTag("Player") ||
            hit.CompareTag("PowerUp") ||
            hit.CompareTag("Enemy") ||
            hit.CompareTag("Bomb") ||
            obj.layer ==
            obstacleLayerIndex ||
            obj.layer ==
            wallLayerIndex;
    }

    private static float
        DistancePointToAabb(
            Vector2 point,
            Vector2 center,
            Vector2 halfExtents)
    {
        float dx =
            Mathf.Max(
                0f,
                Mathf.Abs(
                    point.x -
                    center.x
                ) -
                halfExtents.x
            );

        float dy =
            Mathf.Max(
                0f,
                Mathf.Abs(
                    point.y -
                    center.y
                ) -
                halfExtents.y
            );

        return Mathf.Sqrt(
            dx * dx +
            dy * dy
        );
    }

    private static float
        DistanceBetweenAabbs(
            Vector2 centerA,
            Vector2 halfA,
            Vector2 centerB,
            Vector2 halfB)
    {
        float dx =
            Mathf.Max(
                0f,
                Mathf.Abs(
                    centerA.x -
                    centerB.x
                ) -
                (
                    halfA.x +
                    halfB.x
                )
            );

        float dy =
            Mathf.Max(
                0f,
                Mathf.Abs(
                    centerA.y -
                    centerB.y
                ) -
                (
                    halfA.y +
                    halfB.y
                )
            );

        return Mathf.Sqrt(
            dx * dx +
            dy * dy
        );
    }

    private static void
        SortLargestFirst(
            List<ObstacleCandidate>
                candidates)
    {
        candidates.Sort(
            (a, b) =>
            {
                float areaA =
                    a.Footprint
                        .HalfExtents.x *
                    a.Footprint
                        .HalfExtents.y;

                float areaB =
                    b.Footprint
                        .HalfExtents.x *
                    b.Footprint
                        .HalfExtents.y;

                return areaB
                    .CompareTo(
                        areaA
                    );
            }
        );
    }

    private static void Shuffle<T>(
        List<T> list)
    {
        for (int i = 0;
             i < list.Count;
             i++)
        {
            int index =
                Random.Range(
                    i,
                    list.Count
                );

            T temp =
                list[i];

            list[i] =
                list[index];

            list[index] =
                temp;
        }
    }

    private void OnValidate()
    {
        randomObstacleCount =
            Mathf.Clamp(
                randomObstacleCount,
                0,
                MaximumObstaclesPerLevel
            );

        minDistanceBetweenObstacles =
            Mathf.Max(
                0f,
                minDistanceBetweenObstacles
            );

        playerSafeDistance =
            Mathf.Max(
                0f,
                playerSafeDistance
            );

        edgePadding =
            Mathf.Max(
                0f,
                edgePadding
            );

        arenaEdgeClearance =
            Mathf.Max(
                0f,
                arenaEdgeClearance
            );

        checkRadius =
            Mathf.Max(
                0f,
                checkRadius
            );

        footprintPadding =
            Mathf.Max(
                0f,
                footprintPadding
            );

        maxAttempts =
            Mathf.Max(
                1,
                maxAttempts
            );

        maxLayoutSearchNodes =
            Mathf.Max(
                1000,
                maxLayoutSearchNodes
            );

        obstaclePopupGap =
            Mathf.Max(
                0f,
                obstaclePopupGap
            );
    }
}