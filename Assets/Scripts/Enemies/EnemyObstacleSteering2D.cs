using UnityEngine;

/// <summary>
/// Lightweight local obstacle steering shared by moving enemies.
/// It anticipates collisions with the enemy's real Collider2D shape and
/// chooses a clear direction that keeps as much progress as possible.
/// </summary>
public static class EnemyObstacleSteering2D
{
    private const float MinimumDirectionSqr = 0.0001f;
    private const float MinimumPenetrationDepth = 0.001f;
    private const float RecoveryPadding = 0.015f;
    private const float MinimumRecoveryStep = 0.06f;
    private const float MaximumRecoveryStep = 0.30f;

    // Dynamic-obstacle prediction. The look-ahead window is deliberately short:
    // this is local steering, not global pathfinding. It is long enough to see a
    // hovering/rotating obstacle enter the enemy's future corridor before contact.
    private const float MinimumPredictionTime = 0.28f;
    private const float MaximumPredictionTime = 0.70f;
    private const float PredictionExtraPadding = 0.045f;
    private const int PredictionSamples = 4;

    // Shared non-alloc buffers. Unity 2D physics queries run on the main thread
    // in this project, so reusable buffers avoid per-enemy GC.
    private static readonly Collider2D[] OverlapBuffer = new Collider2D[24];
    private static readonly Collider2D[] PredictiveObstacleBuffer =
        new Collider2D[24];
    private static readonly Vector2[] PredictiveLinearVelocities =
        new Vector2[24];
    private static readonly float[] PredictiveAngularVelocities =
        new float[24];

    public static LayerMask BuildNavigationMask(LayerMask configuredMask)
    {
        int mask = configuredMask.value;

        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleLayer >= 0)
            mask |= 1 << obstacleLayer;

        int wallLayer = LayerMask.NameToLayer("Wall");
        if (wallLayer >= 0)
            mask |= 1 << wallLayer;

        return mask;
    }

    /// <summary>
    /// Configures an AI-driven body for deterministic top-down locomotion.
    /// Rigidbody2D.Slide is primarily intended for Kinematic movement; keeping
    /// the body Kinematic prevents Box2D's normal collision impulses/friction
    /// from fighting the steering code and producing wall jitter or pinning.
    /// Full kinematic contacts preserves collision callbacks with every body type.
    /// </summary>
    public static void ConfigureAIMovementBody(
        Rigidbody2D body,
        bool freezeRotation = true)
    {
        if (body == null)
            return;

        body.gravityScale = 0f;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.bodyType = RigidbodyType2D.Kinematic;
        body.useFullKinematicContacts = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.freezeRotation = freezeRotation;
    }

    public static Vector2 GetSteeredDirection(
        Collider2D selfCollider,
        Vector2 desiredDirection,
        Vector2 goalDirection,
        ContactFilter2D solidFilter,
        RaycastHit2D[] hitBuffer,
        float probeDistance,
        float movementDistance,
        float castSkin,
        int angleAttempts,
        float outwardBias,
        ref int preferredSide)
    {
        // If a moving obstacle has already entered us, normal casts start inside
        // the obstacle and are no longer useful for choosing a clean route.
        if (TryGetOverlapRecovery(
                selfCollider,
                solidFilter,
                out Vector2 overlapDirection,
                out _))
        {
            return overlapDirection;
        }

        if (desiredDirection.sqrMagnitude <= MinimumDirectionSqr)
            return Vector2.zero;

        desiredDirection.Normalize();

        if (goalDirection.sqrMagnitude <= MinimumDirectionSqr)
            goalDirection = desiredDirection;
        else
            goalDirection.Normalize();

        if (selfCollider == null || hitBuffer == null || hitBuffer.Length == 0)
            return desiredDirection;

        movementDistance = Mathf.Max(0f, movementDistance);
        castSkin = Mathf.Max(0f, castSkin);

        float minimumClearance = movementDistance + castSkin + 0.005f;
        float fixedDelta = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        float estimatedSpeed = movementDistance / fixedDelta;

        // Speed-scaled look-ahead: a fast enemy should begin its turn earlier
        // instead of using the same short probe as a slow enemy. This follows
        // the standard steering-behaviour approach where avoidance distance is
        // based on current speed/agility. Existing inspector values remain a
        // lower bound, so no prefab migration is required.
        float speedLookAhead = estimatedSpeed * 0.25f;
        float effectiveProbeDistance = Mathf.Max(
            probeDistance,
            Mathf.Max(
                minimumClearance + 0.05f,
                speedLookAhead
            )
        );

        float fullProbeDistance = effectiveProbeDistance + castSkin;

        float predictionTime = Mathf.Clamp(
            fullProbeDistance / Mathf.Max(estimatedSpeed, 0.01f) + 0.12f,
            MinimumPredictionTime,
            MaximumPredictionTime
        );

        int predictiveObstacleCount = CollectPredictiveObstacles(
            selfCollider,
            solidFilter,
            fullProbeDistance,
            predictionTime
        );

        float directClearance = GetClearance(
            selfCollider,
            desiredDirection,
            solidFilter,
            hitBuffer,
            fullProbeDistance,
            out RaycastHit2D directHit
        );

        bool directStaticBlocked =
            directClearance < fullProbeDistance - 0.001f;

        bool directMovingThreat = TryGetPredictedMovingThreat(
            selfCollider,
            desiredDirection,
            estimatedSpeed,
            predictionTime,
            castSkin,
            predictiveObstacleCount,
            out Vector2 predictedEscapeNormal,
            out _,
            out _
        );

        // A path is only truly clear when it is clear now AND none of the
        // moving/rotating obstacle shapes will sweep into it during look-ahead.
        if (!directStaticBlocked && !directMovingThreat)
            return desiredDirection;

        int preferredSideLocal = preferredSide >= 0 ? 1 : -1;
        angleAttempts = Mathf.Clamp(angleAttempts, 2, 8);
        outwardBias = Mathf.Clamp(outwardBias, 0f, 1f);

        Vector2 obstacleNormal;

        if (directStaticBlocked && directHit.collider != null)
        {
            obstacleNormal = directHit.normal.sqrMagnitude > MinimumDirectionSqr
                ? directHit.normal.normalized
                : -desiredDirection;
        }
        else if (directMovingThreat &&
                 predictedEscapeNormal.sqrMagnitude > MinimumDirectionSqr)
        {
            obstacleNormal = predictedEscapeNormal.normalized;
        }
        else
        {
            obstacleNormal = -desiredDirection;
        }

        Vector2 bestDirection = Vector2.zero;
        float bestScore = float.NegativeInfinity;
        int bestSide = preferredSideLocal;

        // Slide-like tangent choices first. This gives a stable side decision and
        // prevents the AI from repeatedly steering into/away from the same edge.
        Vector2 surfaceTangent = new Vector2(
            -obstacleNormal.y,
            obstacleNormal.x
        );

        EvaluateCandidate(
            surfaceTangent * preferredSideLocal,
            preferredSideLocal,
            true
        );

        EvaluateCandidate(
            surfaceTangent * -preferredSideLocal,
            -preferredSideLocal,
            true
        );

        // Fan outward from the desired direction. Reynolds-style obstacle
        // avoidance benefits from looking ahead farther at speed; the exact
        // Collider2D cast still handles the current static geometry.
        for (int i = 0; i < angleAttempts; i++)
        {
            float t = angleAttempts <= 1
                ? 1f
                : i / (float)(angleAttempts - 1);

            float angle = Mathf.Lerp(25f, 115f, t);

            int firstSide = preferredSideLocal;
            int secondSide = -preferredSideLocal;

            EvaluateCandidate(
                Rotate(desiredDirection, angle * firstSide),
                firstSide,
                false
            );

            EvaluateCandidate(
                Rotate(desiredDirection, angle * secondSide),
                secondSide,
                false
            );
        }

        if (bestDirection.sqrMagnitude > MinimumDirectionSqr)
        {
            preferredSide = bestSide;
            return bestDirection.normalized;
        }

        // If every forward/tangent route is temporarily unsafe, move away from
        // the blocking surface rather than standing still and letting a moving
        // obstacle pin the enemy against its contact point.
        if (obstacleNormal.sqrMagnitude > MinimumDirectionSqr)
        {
            float awayClearance = GetClearance(
                selfCollider,
                obstacleNormal,
                solidFilter,
                hitBuffer,
                minimumClearance,
                out _
            );

            bool movingSafe = !TryGetPredictedMovingThreat(
                selfCollider,
                obstacleNormal,
                estimatedSpeed,
                predictionTime,
                castSkin,
                predictiveObstacleCount,
                out _,
                out _,
                out _
            );

            if (awayClearance >= minimumClearance && movingSafe)
                return obstacleNormal;
        }

        return Vector2.zero;

        void EvaluateCandidate(
            Vector2 candidateDirection,
            int side,
            bool surfaceSlide)
        {
            if (candidateDirection.sqrMagnitude <= MinimumDirectionSqr)
                return;

            candidateDirection.Normalize();

            // Keep a small outward component while passing an edge. Without this
            // margin a pursuit vector can immediately pull the enemy back into
            // contact on the next FixedUpdate and create visible chatter.
            if (obstacleNormal.sqrMagnitude > MinimumDirectionSqr)
            {
                float bias = surfaceSlide
                    ? Mathf.Max(outwardBias, 0.24f)
                    : outwardBias;

                candidateDirection = (
                    candidateDirection +
                    obstacleNormal * bias
                ).normalized;
            }

            float clearance = GetClearance(
                selfCollider,
                candidateDirection,
                solidFilter,
                hitBuffer,
                fullProbeDistance,
                out _
            );

            if (clearance < minimumClearance)
                return;

            bool predictedThreat = TryGetPredictedMovingThreat(
                selfCollider,
                candidateDirection,
                estimatedSpeed,
                predictionTime,
                castSkin,
                predictiveObstacleCount,
                out _,
                out float predictedSeparation,
                out _
            );

            if (predictedThreat)
                return;

            float clearanceScore = Mathf.Clamp01(
                clearance / effectiveProbeDistance
            );

            float predictionScore = predictiveObstacleCount <= 0
                ? 1f
                : Mathf.Clamp01(
                    predictedSeparation /
                    Mathf.Max(0.20f, castSkin + PredictionExtraPadding + 0.10f)
                );

            float desiredProgress = Vector2.Dot(
                candidateDirection,
                desiredDirection
            );

            float goalProgress = Vector2.Dot(
                candidateDirection,
                goalDirection
            );

            float sideCommitmentBonus = side == preferredSideLocal
                ? 0.16f
                : 0f;

            float score =
                clearanceScore * 1.35f +
                predictionScore * 0.35f +
                desiredProgress * 0.75f +
                goalProgress * 0.55f +
                sideCommitmentBonus;

            if (score <= bestScore)
                return;

            bestScore = score;
            bestDirection = candidateDirection;
            bestSide = side >= 0 ? 1 : -1;
        }
    }


    /// <summary>
    /// Detects whether this collider is already penetrating a navigation solid
    /// and returns a weighted separation direction plus the deepest overlap.
    /// Uses Collider2D.Distance so the result stays reliable even when a moving
    /// obstacle enters the enemy between physics steps.
    /// </summary>
    public static bool TryGetOverlapRecovery(
        Collider2D selfCollider,
        ContactFilter2D solidFilter,
        out Vector2 recoveryDirection,
        out float penetrationDepth)
    {
        recoveryDirection = Vector2.zero;
        penetrationDepth = 0f;

        if (selfCollider == null || !selfCollider.enabled)
            return false;

        int overlapCount = Physics2D.OverlapCollider(
            selfCollider,
            solidFilter,
            OverlapBuffer
        );

        if (overlapCount <= 0)
            return false;

        Rigidbody2D selfBody = selfCollider.attachedRigidbody;
        Vector2 weightedDirection = Vector2.zero;
        Vector2 strongestDirection = Vector2.zero;
        float strongestDepth = 0f;

        for (int i = 0; i < overlapCount; i++)
        {
            Collider2D other = OverlapBuffer[i];

            if (other == null || other == selfCollider || !other.enabled)
                continue;

            if (selfBody != null && other.attachedRigidbody == selfBody)
                continue;

            ColliderDistance2D separation = selfCollider.Distance(other);

            if (!separation.isValid || !separation.isOverlapped)
                continue;

            float depth = Mathf.Max(
                MinimumPenetrationDepth,
                -separation.distance
            );

            // ColliderDistance2D.normal points from pointB to pointA.
            // When the colliders overlap, distance is negative; therefore the
            // translation that separates collider A (selfCollider) is
            // normal * distance, i.e. the OPPOSITE of normal.
            Vector2 escapeDirection = -separation.normal;

            if (escapeDirection.sqrMagnitude <= MinimumDirectionSqr)
            {
                escapeDirection =
                    (Vector2)selfCollider.bounds.center -
                    (Vector2)other.bounds.center;
            }

            if (escapeDirection.sqrMagnitude <= MinimumDirectionSqr)
            {
                escapeDirection =
                    selfBody != null &&
                    selfBody.linearVelocity.sqrMagnitude > MinimumDirectionSqr
                        ? -selfBody.linearVelocity.normalized
                        : Vector2.up;
            }
            else
            {
                escapeDirection.Normalize();
            }

            weightedDirection +=
                escapeDirection * (depth + RecoveryPadding);

            if (depth > strongestDepth)
            {
                strongestDepth = depth;
                strongestDirection = escapeDirection;
            }

            penetrationDepth = Mathf.Max(penetrationDepth, depth);
        }

        if (penetrationDepth <= 0f)
            return false;

        if (weightedDirection.sqrMagnitude > MinimumDirectionSqr)
            recoveryDirection = weightedDirection.normalized;
        else if (strongestDirection.sqrMagnitude > MinimumDirectionSqr)
            recoveryDirection = strongestDirection.normalized;

        return recoveryDirection.sqrMagnitude > MinimumDirectionSqr;
    }

    /// <summary>
    /// Calculates a short but decisive separation step. It is intentionally
    /// capped so a badly overlapped enemy never teleports across the arena.
    /// </summary>
    public static float GetOverlapRecoveryDistance(
        float penetrationDepth,
        float normalMovementDistance,
        float castSkin)
    {
        // Resolve only the penetration + a tiny release margin. The previous
        // implementation forced at least ~2.5 normal movement steps, which made
        // the enemy overshoot away from a wall and then steer straight back into
        // it on the next frame: the classic visible obstacle jitter loop.
        float requiredDistance =
            Mathf.Max(0f, penetrationDepth) +
            Mathf.Max(0f, castSkin) +
            RecoveryPadding;

        float maxStep = Mathf.Clamp(
            Mathf.Max(0f, normalMovementDistance) * 1.5f +
            Mathf.Max(0f, castSkin),
            MinimumRecoveryStep,
            MaximumRecoveryStep
        );

        return Mathf.Min(requiredDistance, maxStep);
    }

    /// <summary>
    /// Returns the free travel distance for the collider in a direction,
    /// using the exact same shape-cast rules as local steering.
    /// Useful for long-range route planning without duplicating physics code.
    /// </summary>
    public static float GetPathClearance(
        Collider2D selfCollider,
        Vector2 direction,
        ContactFilter2D solidFilter,
        RaycastHit2D[] hitBuffer,
        float distance)
    {
        if (distance <= 0f)
            return 0f;

        if (direction.sqrMagnitude <= MinimumDirectionSqr)
            return 0f;

        if (selfCollider == null ||
            hitBuffer == null ||
            hitBuffer.Length == 0)
        {
            return distance;
        }

        return GetClearance(
            selfCollider,
            direction.normalized,
            solidFilter,
            hitBuffer,
            distance,
            out _
        );
    }

    public static bool IsPathClear(
        Collider2D selfCollider,
        Vector2 direction,
        ContactFilter2D solidFilter,
        RaycastHit2D[] hitBuffer,
        float distance,
        float castSkin = 0f)
    {
        if (distance <= 0f)
            return true;

        if (direction.sqrMagnitude <= MinimumDirectionSqr)
            return true;

        if (selfCollider == null ||
            hitBuffer == null ||
            hitBuffer.Length == 0)
        {
            return true;
        }

        float requiredDistance =
            Mathf.Max(0f, distance) +
            Mathf.Max(0f, castSkin);

        float clearance = GetClearance(
            selfCollider,
            direction.normalized,
            solidFilter,
            hitBuffer,
            requiredDistance,
            out _
        );

        return clearance >= requiredDistance - 0.001f;
    }

    private static int CollectPredictiveObstacles(
        Collider2D selfCollider,
        ContactFilter2D solidFilter,
        float probeDistance,
        float predictionTime)
    {
        if (selfCollider == null || !selfCollider.enabled)
            return 0;

        Rigidbody2D selfBody = selfCollider.attachedRigidbody;
        if (selfBody == null)
            return 0;

        float selfRadius = selfCollider.bounds.extents.magnitude;
        float searchRadius = Mathf.Max(
            0.25f,
            probeDistance + selfRadius + 1.0f * predictionTime
        );

        // Broad-phase prediction also sees animated obstacle colliders which
        // were accidentally left on a non-navigation layer. They are accepted
        // below only when they either match the navigation mask or belong to an
        // ObstacleIdleAnimation, so players/enemies are not treated as walls.
        ContactFilter2D predictiveFilter = ContactFilter2D.noFilter;
        predictiveFilter.useTriggers = false;

        int hitCount = Physics2D.OverlapCircle(
            selfCollider.bounds.center,
            searchRadius,
            predictiveFilter,
            PredictiveObstacleBuffer
        );

        int writeIndex = 0;

        for (int i = 0; i < hitCount && writeIndex < PredictiveObstacleBuffer.Length; i++)
        {
            Collider2D other = PredictiveObstacleBuffer[i];

            if (other == null || other == selfCollider || !other.enabled)
                continue;

            Rigidbody2D otherBody = other.attachedRigidbody;

            if (otherBody == null || otherBody == selfBody)
                continue;

            ObstacleIdleAnimation idleAnimation =
                other.GetComponentInParent<ObstacleIdleAnimation>();

            bool matchesNavigationMask =
                !solidFilter.useLayerMask ||
                (solidFilter.layerMask.value &
                 (1 << other.gameObject.layer)) != 0;

            if (!matchesNavigationMask && idleAnimation == null)
                continue;

            GetObstacleMotion(
                otherBody,
                idleAnimation,
                out Vector2 linearVelocity,
                out float angularVelocity
            );

            // Static/non-moving bodies are already handled exactly by Collider2D.Cast.
            if (linearVelocity.sqrMagnitude <= 0.0001f &&
                Mathf.Abs(angularVelocity) <= 0.01f)
            {
                continue;
            }

            PredictiveObstacleBuffer[writeIndex] = other;
            PredictiveLinearVelocities[writeIndex] = linearVelocity;
            PredictiveAngularVelocities[writeIndex] = angularVelocity;
            writeIndex++;
        }

        for (int i = writeIndex;
             i < hitCount && i < PredictiveObstacleBuffer.Length;
             i++)
        {
            PredictiveObstacleBuffer[i] = null;
            PredictiveLinearVelocities[i] = Vector2.zero;
            PredictiveAngularVelocities[i] = 0f;
        }

        return writeIndex;
    }

    private static bool TryGetPredictedMovingThreat(
        Collider2D selfCollider,
        Vector2 candidateDirection,
        float selfSpeed,
        float predictionTime,
        float castSkin,
        int obstacleCount,
        out Vector2 escapeNormal,
        out float minimumSeparation,
        out float earliestThreatTime)
    {
        escapeNormal = Vector2.zero;
        minimumSeparation = float.PositiveInfinity;
        earliestThreatTime = float.PositiveInfinity;

        if (selfCollider == null ||
            candidateDirection.sqrMagnitude <= MinimumDirectionSqr ||
            obstacleCount <= 0)
        {
            return false;
        }

        Rigidbody2D selfBody = selfCollider.attachedRigidbody;
        if (selfBody == null)
            return false;

        candidateDirection.Normalize();
        Vector2 selfVelocity = candidateDirection * Mathf.Max(0f, selfSpeed);
        float safetyDistance = Mathf.Max(0f, castSkin) + PredictionExtraPadding;
        bool foundThreat = false;

        for (int obstacleIndex = 0;
             obstacleIndex < obstacleCount && obstacleIndex < PredictiveObstacleBuffer.Length;
             obstacleIndex++)
        {
            Collider2D other = PredictiveObstacleBuffer[obstacleIndex];
            if (other == null || !other.enabled)
                continue;

            Rigidbody2D otherBody = other.attachedRigidbody;
            if (otherBody == null || otherBody == selfBody)
                continue;

            Vector2 otherVelocity =
                PredictiveLinearVelocities[obstacleIndex];

            float otherAngularVelocity =
                PredictiveAngularVelocities[obstacleIndex];

            for (int sample = 1; sample <= PredictionSamples; sample++)
            {
                float t = predictionTime * (sample / (float)PredictionSamples);

                Vector2 selfPosition =
                    selfBody.position + selfVelocity * t;

                float selfAngle = selfBody.rotation;

                Vector2 otherPosition =
                    otherBody.position + otherVelocity * t;

                float otherAngle =
                    otherBody.rotation + otherAngularVelocity * t;

                ColliderDistance2D futureDistance = selfCollider.Distance(
                    selfPosition,
                    selfAngle,
                    other,
                    otherPosition,
                    otherAngle
                );

                if (!futureDistance.isValid)
                    continue;

                float separation = futureDistance.distance;

                if (separation < minimumSeparation)
                    minimumSeparation = separation;

                if (separation > safetyDistance)
                    continue;

                foundThreat = true;

                if (t > earliestThreatTime)
                    continue;

                earliestThreatTime = t;

                // Collider2D.Distance returns a normal which, when multiplied
                // by its signed distance, moves this collider toward contact (or
                // out of an overlap). For avoidance we always want the direction
                // AWAY from the predicted obstacle, so use the opposite normal.
                Vector2 normal = -futureDistance.normal;

                if (normal.sqrMagnitude <= MinimumDirectionSqr)
                {
                    normal = selfPosition - otherPosition;
                }

                if (normal.sqrMagnitude > MinimumDirectionSqr)
                    escapeNormal = normal.normalized;
            }
        }

        if (float.IsPositiveInfinity(minimumSeparation))
            minimumSeparation = 999f;

        return foundThreat;
    }

    private static void GetObstacleMotion(
        Rigidbody2D obstacleBody,
        ObstacleIdleAnimation idleAnimation,
        out Vector2 linearVelocity,
        out float angularVelocity)
    {
        linearVelocity = obstacleBody != null
            ? obstacleBody.linearVelocity
            : Vector2.zero;

        angularVelocity = obstacleBody != null
            ? obstacleBody.angularVelocity
            : 0f;

        if (idleAnimation == null)
            return;

        if (idleAnimation.TryGetPhysicsMotion(
                obstacleBody,
                out Vector2 scriptedLinearVelocity,
                out float scriptedAngularVelocity))
        {
            linearVelocity = scriptedLinearVelocity;
            angularVelocity = scriptedAngularVelocity;
        }
    }

    private static float GetClearance(
        Collider2D selfCollider,
        Vector2 direction,
        ContactFilter2D solidFilter,
        RaycastHit2D[] hitBuffer,
        float distance,
        out RaycastHit2D closestHit)
    {
        closestHit = default;

        if (direction.sqrMagnitude <= MinimumDirectionSqr || distance <= 0f)
            return distance;

        Vector2 castDirection = direction.normalized;

        int hitCount = selfCollider.Cast(
            castDirection,
            solidFilter,
            hitBuffer,
            distance
        );

        float closestDistance = distance;
        bool foundBlockingHit = false;
        Rigidbody2D selfBody = selfCollider.attachedRigidbody;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = hitBuffer[i];
            Collider2D hitCollider = hit.collider;

            if (hitCollider == null)
                continue;

            if (hitCollider == selfCollider)
                continue;

            if (selfBody != null && hitCollider.attachedRigidbody == selfBody)
                continue;

            // A moving obstacle can leave the enemy exactly touching its
            // surface (not technically overlapping). Collider2D.Cast may then
            // report a distance of zero for every candidate. If the candidate
            // direction is moving away from, or tangentially along, that
            // surface, the zero-distance contact must not block the escape.
            if (hit.distance <= 0.002f &&
                hit.normal.sqrMagnitude > MinimumDirectionSqr)
            {
                float intoSurface = Vector2.Dot(
                    castDirection,
                    hit.normal.normalized
                );

                if (intoSurface >= -0.02f)
                    continue;
            }

            if (hit.distance >= closestDistance)
                continue;

            closestDistance = Mathf.Max(0f, hit.distance);
            closestHit = hit;
            foundBlockingHit = true;
        }

        return foundBlockingHit
            ? closestDistance
            : distance;
    }

    /// <summary>
    /// Moves a Rigidbody2D using Unity 6's built-in top-down Slide solver.
    /// The custom steering above still chooses the preferred route, while
    /// Slide handles the final physical contact and automatically continues
    /// along obstacle surfaces instead of letting a moving obstacle pin the
    /// body at the contact point.
    /// </summary>
    public static bool MoveWithPhysicsSlide(
        Rigidbody2D body,
        Collider2D selfCollider,
        Vector2 velocity,
        float deltaTime,
        ContactFilter2D solidFilter,
        int maxIterations = 5)
    {
        if (body == null || deltaTime <= 0f ||
            velocity.sqrMagnitude <= MinimumDirectionSqr)
        {
            return false;
        }

        if (selfCollider == null || !selfCollider.enabled)
        {
            body.MovePosition(body.position + velocity * deltaTime);
            return true;
        }

        // Defensive fallback for any legacy prefab that is still Dynamic.
        // AI movement is authoritative, so solver velocity must not accumulate.
        if (body.bodyType == RigidbodyType2D.Dynamic)
            body.linearVelocity = Vector2.zero;

        Rigidbody2D.SlideMovement movement =
            CreateTopDownSlideMovement(
                selfCollider,
                solidFilter,
                maxIterations,
                useSimulationMove: true,
                useNoMove: false
            );

        Vector2 start = body.position;
        Rigidbody2D.SlideResults result = body.Slide(
            velocity,
            deltaTime,
            movement
        );

        return (result.position - start).sqrMagnitude > 0.000001f;
    }

    public static bool MoveDisplacementWithPhysicsSlide(
        Rigidbody2D body,
        Collider2D selfCollider,
        Vector2 displacement,
        float deltaTime,
        ContactFilter2D solidFilter,
        int maxIterations = 5)
    {
        if (deltaTime <= 0f ||
            displacement.sqrMagnitude <= MinimumDirectionSqr)
        {
            return false;
        }

        return MoveWithPhysicsSlide(
            body,
            selfCollider,
            displacement / deltaTime,
            deltaTime,
            solidFilter,
            maxIterations
        );
    }

    /// <summary>
    /// Calculates the position Unity's Slide solver would reach without
    /// moving the Rigidbody2D. Used by enemies that must clamp movement to
    /// gameplay bounds before committing the final MovePosition.
    /// </summary>
    public static Vector2 CalculatePhysicsSlideTarget(
        Rigidbody2D body,
        Collider2D selfCollider,
        Vector2 velocity,
        float deltaTime,
        ContactFilter2D solidFilter,
        int maxIterations = 5)
    {
        if (body == null || deltaTime <= 0f ||
            velocity.sqrMagnitude <= MinimumDirectionSqr)
        {
            return body != null ? body.position : Vector2.zero;
        }

        if (selfCollider == null || !selfCollider.enabled)
            return body.position + velocity * deltaTime;

        Rigidbody2D.SlideMovement movement =
            CreateTopDownSlideMovement(
                selfCollider,
                solidFilter,
                maxIterations,
                useSimulationMove: false,
                useNoMove: true
            );

        return body.Slide(
            velocity,
            deltaTime,
            movement
        ).position;
    }

    private static Rigidbody2D.SlideMovement CreateTopDownSlideMovement(
        Collider2D selfCollider,
        ContactFilter2D solidFilter,
        int maxIterations,
        bool useSimulationMove,
        bool useNoMove)
    {
        Rigidbody2D.SlideMovement movement =
            new Rigidbody2D.SlideMovement
            {
                gravity = Vector2.zero,
                surfaceAnchor = Vector2.zero,

                // Unity specifically documents a zero surfaceUp vector for
                // top-down games: surface-angle checks are ignored and Slide
                // is allowed in every direction around a contact.
                surfaceUp = Vector2.zero,

                maxIterations = Mathf.Clamp(maxIterations, 1, 12),
                selectedCollider = selfCollider,
                useAttachedTriggers = false,
                useSimulationMove = useSimulationMove,
                useNoMove = useNoMove
            };

        if (solidFilter.useLayerMask)
            movement.SetLayerMask(solidFilter.layerMask);

        return movement;
    }

    private static Vector2 Rotate(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);

        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos
        );
    }
}
