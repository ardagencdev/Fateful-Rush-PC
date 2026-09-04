#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ObstacleIdleAnimation))]
[CanEditMultipleObjects]
public sealed class ObstacleIdleAnimationEditor : Editor
{
    private SerializedProperty preset;
    private SerializedProperty animationTarget;
    private SerializedProperty spriteRenderer;
    private SerializedProperty timeSource;
    private SerializedProperty randomizePhase;
    private SerializedProperty randomSeed;
    private SerializedProperty blendInDuration;

    private SerializedProperty rotate;
    private SerializedProperty rotationMotion;
    private SerializedProperty rotateSpeed;
    private SerializedProperty randomizeRotateDirection;
    private SerializedProperty rotateSpeedVariation;
    private SerializedProperty swayAngle;
    private SerializedProperty swaySpeed;

    private SerializedProperty hover;
    private SerializedProperty hoverAmount;
    private SerializedProperty hoverSpeed;
    private SerializedProperty horizontalFrequencyMultiplier;

    private SerializedProperty drift;
    private SerializedProperty driftAmount;
    private SerializedProperty driftSpeed;

    private SerializedProperty pulseScale;
    private SerializedProperty pulseAmount;
    private SerializedProperty pulseSpeed;
    private SerializedProperty pulseAxis;

    private SerializedProperty pulseColor;
    private SerializedProperty useOriginalColorAsColor1;
    private SerializedProperty color1;
    private SerializedProperty color2;
    private SerializedProperty colorPulseSpeed;
    private SerializedProperty preserveOriginalAlpha;

    private SerializedProperty shake;
    private SerializedProperty shakeAmount;
    private SerializedProperty shakeSpeed;

    private void OnEnable()
    {
        preset = Find("preset");
        animationTarget = Find("animationTarget");
        spriteRenderer = Find("spriteRenderer");
        timeSource = Find("timeSource");
        randomizePhase = Find("randomizePhase");
        randomSeed = Find("randomSeed");
        blendInDuration = Find("blendInDuration");

        rotate = Find("rotate");
        rotationMotion = Find("rotationMotion");
        rotateSpeed = Find("rotateSpeed");
        randomizeRotateDirection = Find("randomizeRotateDirection");
        rotateSpeedVariation = Find("rotateSpeedVariation");
        swayAngle = Find("swayAngle");
        swaySpeed = Find("swaySpeed");

        hover = Find("hover");
        hoverAmount = Find("hoverAmount");
        hoverSpeed = Find("hoverSpeed");
        horizontalFrequencyMultiplier = Find("horizontalFrequencyMultiplier");

        drift = Find("drift");
        driftAmount = Find("driftAmount");
        driftSpeed = Find("driftSpeed");

        pulseScale = Find("pulseScale");
        pulseAmount = Find("pulseAmount");
        pulseSpeed = Find("pulseSpeed");
        pulseAxis = Find("pulseAxis");

        pulseColor = Find("pulseColor");
        useOriginalColorAsColor1 = Find("useOriginalColorAsColor1");
        color1 = Find("color1");
        color2 = Find("color2");
        colorPulseSpeed = Find("colorPulseSpeed");
        preserveOriginalAlpha = Find("preserveOriginalAlpha");

        shake = Find("shake");
        shakeAmount = Find("shakeAmount");
        shakeSpeed = Find("shakeSpeed");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPresetSection();
        EditorGUILayout.Space(4f);
        DrawReferencesSection();
        EditorGUILayout.Space(4f);
        DrawGeneralSection();
        EditorGUILayout.Space(6f);

        DrawRotationSection();
        DrawHoverSection();
        DrawDriftSection();
        DrawScaleSection();
        DrawColorSection();
        DrawShakeSection();

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(8f);
        DrawRuntimeButtons();
    }

    private void DrawPresetSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Animation Preset", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(preset, GUIContent.none);

        using (new EditorGUI.DisabledScope(preset.hasMultipleDifferentValues))
        {
            if (GUILayout.Button("Apply", GUILayout.Width(70f)))
            {
                serializedObject.ApplyModifiedProperties();
                ApplyPresetToTargets();
                serializedObject.Update();
            }
        }

        EditorGUILayout.EndHorizontal();

        if (!preset.hasMultipleDifferentValues &&
            preset.enumValueIndex != (int)ObstacleIdleAnimation.IdlePreset.Custom)
        {
            EditorGUILayout.HelpBox(
                "Preset changes are applied automatically. You can still fine-tune every value below.",
                MessageType.Info);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawReferencesSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(animationTarget);
        EditorGUILayout.PropertyField(spriteRenderer);
        EditorGUILayout.EndVertical();
    }

    private void DrawGeneralSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(timeSource);
        EditorGUILayout.PropertyField(blendInDuration);
        EditorGUILayout.PropertyField(randomizePhase);

        if (randomizePhase.boolValue || randomizePhase.hasMultipleDifferentValues)
            EditorGUILayout.PropertyField(randomSeed);

        EditorGUILayout.EndVertical();
    }

    private void DrawRotationSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.PropertyField(rotate, new GUIContent("Rotation"));

        if (rotate.boolValue || rotate.hasMultipleDifferentValues)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(rotationMotion, new GUIContent("Mode"));

            if (!rotationMotion.hasMultipleDifferentValues &&
                rotationMotion.enumValueIndex == (int)ObstacleIdleAnimation.RotationMotion.Continuous)
            {
                EditorGUILayout.PropertyField(rotateSpeed);
                EditorGUILayout.PropertyField(randomizeRotateDirection);
                EditorGUILayout.PropertyField(rotateSpeedVariation);
            }
            else
            {
                EditorGUILayout.PropertyField(swayAngle);
                EditorGUILayout.PropertyField(swaySpeed);
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawHoverSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.PropertyField(hover, new GUIContent("Hover"));

        if (hover.boolValue || hover.hasMultipleDifferentValues)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(hoverAmount);
            EditorGUILayout.PropertyField(hoverSpeed);
            EditorGUILayout.PropertyField(horizontalFrequencyMultiplier, new GUIContent("Horizontal Frequency"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawDriftSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.PropertyField(drift, new GUIContent("Organic Drift"));

        if (drift.boolValue || drift.hasMultipleDifferentValues)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(driftAmount);
            EditorGUILayout.PropertyField(driftSpeed);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawScaleSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.PropertyField(pulseScale, new GUIContent("Scale Pulse"));

        if (pulseScale.boolValue || pulseScale.hasMultipleDifferentValues)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(pulseAmount);
            EditorGUILayout.PropertyField(pulseSpeed);
            EditorGUILayout.PropertyField(pulseAxis);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawColorSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.PropertyField(pulseColor, new GUIContent("Color Pulse"));

        if (pulseColor.boolValue || pulseColor.hasMultipleDifferentValues)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(useOriginalColorAsColor1, new GUIContent("Use Original As Color 1"));

            if (!useOriginalColorAsColor1.boolValue || useOriginalColorAsColor1.hasMultipleDifferentValues)
                EditorGUILayout.PropertyField(color1);

            EditorGUILayout.PropertyField(color2);
            EditorGUILayout.PropertyField(colorPulseSpeed, new GUIContent("Pulse Speed"));
            EditorGUILayout.PropertyField(preserveOriginalAlpha);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawShakeSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.PropertyField(shake, new GUIContent("Shake"));

        if (shake.boolValue || shake.hasMultipleDifferentValues)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(shakeAmount);
            EditorGUILayout.PropertyField(shakeSpeed);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawRuntimeButtons()
    {
        EditorGUILayout.BeginHorizontal();

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Restart Animation"))
            {
                foreach (Object selectedTarget in targets)
                    ((ObstacleIdleAnimation)selectedTarget).RestartAnimation();
            }

            if (GUILayout.Button("Recapture Base Pose"))
            {
                foreach (Object selectedTarget in targets)
                    ((ObstacleIdleAnimation)selectedTarget).RecaptureBasePose();
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void ApplyPresetToTargets()
    {
        foreach (Object selectedTarget in targets)
        {
            ObstacleIdleAnimation animation = (ObstacleIdleAnimation)selectedTarget;
            Undo.RecordObject(animation, "Apply Obstacle Idle Preset");
            animation.ApplySelectedPreset();
            EditorUtility.SetDirty(animation);
        }
    }

    private SerializedProperty Find(string propertyName)
    {
        return serializedObject.FindProperty(propertyName);
    }
}
#endif
