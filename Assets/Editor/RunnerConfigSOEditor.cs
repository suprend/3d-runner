using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RunnerConfigSO))]
public class RunnerConfigSOEditor : Editor
{
    private static bool showCamera = true;
    private static bool showSpawn = true;
    private static bool showSlide = true;

    public override void OnInspectorGUI()
    {
        var cfg = (RunnerConfigSO)target;
        if (cfg == null)
        {
            DrawDefaultInspector();
            return;
        }

        DrawWarnings(cfg);
        DrawHelp(cfg);

        DrawDefaultInspector();
    }

    private static void DrawWarnings(RunnerConfigSO cfg)
    {
        if (cfg.fogEnabled && cfg.fogEnd <= cfg.fogStart)
        {
            EditorGUILayout.HelpBox("Fog is enabled but fogEnd <= fogStart. Linear fog will look wrong; set fogEnd > fogStart.", MessageType.Warning);
        }

        if (cfg.cameraFarClip < 10f)
        {
            EditorGUILayout.HelpBox("cameraFarClip is < 10. Runtime clamps it to >= 10, but you probably want a bigger value.", MessageType.Warning);
        }

        if (cfg.maxLaneIndex < cfg.minLaneIndex)
        {
            EditorGUILayout.HelpBox("maxLaneIndex < minLaneIndex. Lane range is invalid.", MessageType.Warning);
        }
    }

    private static void DrawHelp(RunnerConfigSO cfg)
    {
        showCamera = EditorGUILayout.BeginFoldoutHeaderGroup(showCamera, "Camera setup (Top-down guide)");
        if (showCamera)
        {
            EditorGUILayout.HelpBox(
                "How it works (RunnerCameraFollow):\n" +
                "• Camera position = player position + cameraOffset\n" +
                "• Camera looks at = player position + cameraLookAtOffset\n\n" +
                "Top-down & hide the spawn/edge:\n" +
                "• Increase cameraOffset.y (higher camera)\n" +
                "• Reduce |cameraOffset.z| (less behind / more above)\n" +
                "• Reduce cameraLookAtOffset.z if you look too far ahead\n" +
                "• Reduce cameraFarClip and/or fogEnd to hide distant pop-in\n" +
                "  (Fog also clamps far clip to fogEnd + 10)",
                MessageType.Info);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(6);

        showSpawn = EditorGUILayout.BeginFoldoutHeaderGroup(showSpawn, "Spawn visibility");
        if (showSpawn)
        {
            float actualFarClip = ComputeActualFarClip(cfg);
            float invisibleAhead = ComputeInvisibleAhead(cfg);

            EditorGUILayout.HelpBox(
                "If you see obstacles spawning/pop-in:\n" +
                "• Lower cameraFarClip and/or fogEnd (recommended)\n" +
                "• Increase spawnDistanceAhead only if needed (more CPU/pool usage)\n\n" +
                "Computed values:",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.FloatField(new GUIContent("ActualFarClip (camera)"), actualFarClip);
                EditorGUILayout.FloatField(new GUIContent("InvisibleAhead (spawner)"), invisibleAhead);
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(6);

        showSlide = EditorGUILayout.BeginFoldoutHeaderGroup(showSlide, "Slide responsiveness");
        if (showSlide)
        {
            EditorGUILayout.HelpBox(
                "Responsiveness tips:\n" +
                "• slideTransitionSeconds controls how fast the player crouches/stands.\n" +
                "  Lower value = faster return to standing (easier to chain jumps).\n" +
                "• Slide starts only from a Down press on the ground.\n" +
                "  Down in mid-air is used for fast-fall and won't force a slide on landing.",
                MessageType.Info);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(8);
    }

    private static float ComputeActualFarClip(RunnerConfigSO cfg)
    {
        float far = Mathf.Max(10f, cfg.cameraFarClip);
        if (cfg.fogEnabled) far = Mathf.Min(far, cfg.fogEnd + 10f);
        return far;
    }

    private static float ComputeInvisibleAhead(RunnerConfigSO cfg)
    {
        float ahead = Mathf.Max(0f, cfg.spawnDistanceAhead);
        ahead = Mathf.Max(ahead, cfg.cameraFarClip + 20f);
        if (cfg.fogEnabled) ahead = Mathf.Max(ahead, cfg.fogEnd + 10f);
        return ahead;
    }
}

