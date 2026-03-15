using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class RunnerFeedbackAnimatorAssetGenerator
{
    private const string AuraPath = "Visual/FeedbackAura";
    private const string ResourcesFolder = "Assets/Resources";
    private const string ControllerPath = "Assets/Resources/RunnerPlayerFeedbackController.controller";
    private const string IdleClipPath = "Assets/Resources/RunnerPlayerFeedback_Idle.anim";
    private const string DamageClipPath = "Assets/Resources/RunnerPlayerFeedback_Damage.anim";
    private const string PickupClipPath = "Assets/Resources/RunnerPlayerFeedback_Pickup.anim";
    private const string ShieldClipPath = "Assets/Resources/RunnerPlayerFeedback_Shield.anim";

    [InitializeOnLoadMethod]
    private static void EnsureOnLoad()
    {
        EditorApplication.delayCall += EnsureAssets;
    }

    [MenuItem("Tools/Runner/Regenerate Feedback Animator")]
    private static void RegenerateFromMenu()
    {
        CreateOrUpdateAssets();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void EnsureAssets()
    {
        if (EditorApplication.isCompiling) return;
        CreateOrUpdateAssets();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void CreateOrUpdateAssets()
    {
        EnsureFolder(ResourcesFolder);

        var idleClip = CreateOrUpdateClip(IdleClipPath, false, new[]
        {
            new Keyframe(0f, 0.01f),
            new Keyframe(0.1f, 0.01f),
        });

        var damageClip = CreateOrUpdateClip(DamageClipPath, false, new[]
        {
            new Keyframe(0f, 0.18f),
            new Keyframe(0.08f, 1.45f),
            new Keyframe(0.18f, 0.06f),
        });

        var pickupClip = CreateOrUpdateClip(PickupClipPath, false, new[]
        {
            new Keyframe(0f, 0.18f),
            new Keyframe(0.10f, 1.65f),
            new Keyframe(0.22f, 0.06f),
        });

        var shieldClip = CreateOrUpdateClip(ShieldClipPath, true, new[]
        {
            new Keyframe(0f, 1.12f),
            new Keyframe(0.45f, 1.24f),
            new Keyframe(0.90f, 1.12f),
        });

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        BuildController(controller, idleClip, damageClip, pickupClip, shieldClip);
        EditorUtility.SetDirty(controller);
    }

    private static AnimationClip CreateOrUpdateClip(string path, bool loopTime, Keyframe[] keys)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }

        var curve = new AnimationCurve(keys);
        SetScaleCurve(clip, "m_LocalScale.x", curve);
        SetScaleCurve(clip, "m_LocalScale.y", curve);
        SetScaleCurve(clip, "m_LocalScale.z", curve);

        var so = new SerializedObject(clip);
        var settings = so.FindProperty("m_AnimationClipSettings");
        if (settings != null)
        {
            settings.FindPropertyRelative("m_LoopTime").boolValue = loopTime;
            settings.FindPropertyRelative("m_StopTime").floatValue = keys[keys.Length - 1].time;
            so.ApplyModifiedProperties();
        }

        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void SetScaleCurve(AnimationClip clip, string propertyName, AnimationCurve curve)
    {
        var binding = EditorCurveBinding.FloatCurve(AuraPath, typeof(Transform), propertyName);
        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }

    private static void BuildController(
        AnimatorController controller,
        AnimationClip idleClip,
        AnimationClip damageClip,
        AnimationClip pickupClip,
        AnimationClip shieldClip)
    {
        while (controller.parameters.Length > 0)
        {
            controller.RemoveParameter(controller.parameters[0]);
        }

        controller.AddParameter("Damage", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Pickup", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("ShieldActive", AnimatorControllerParameterType.Bool);

        var stateMachine = controller.layers[0].stateMachine;

        foreach (var transition in stateMachine.anyStateTransitions)
        {
            stateMachine.RemoveAnyStateTransition(transition);
        }

        foreach (var childState in stateMachine.states)
        {
            stateMachine.RemoveState(childState.state);
        }

        var idle = stateMachine.AddState("Idle");
        idle.motion = idleClip;

        var damage = stateMachine.AddState("Damage");
        damage.motion = damageClip;

        var pickup = stateMachine.AddState("Pickup");
        pickup.motion = pickupClip;

        var shield = stateMachine.AddState("ShieldLoop");
        shield.motion = shieldClip;

        stateMachine.defaultState = idle;

        var toShield = idle.AddTransition(shield);
        toShield.hasExitTime = false;
        toShield.duration = 0.08f;
        toShield.AddCondition(AnimatorConditionMode.If, 0f, "ShieldActive");

        var shieldToIdle = shield.AddTransition(idle);
        shieldToIdle.hasExitTime = false;
        shieldToIdle.duration = 0.08f;
        shieldToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "ShieldActive");

        var anyDamage = stateMachine.AddAnyStateTransition(damage);
        anyDamage.hasExitTime = false;
        anyDamage.duration = 0.02f;
        anyDamage.AddCondition(AnimatorConditionMode.If, 0f, "Damage");

        var anyPickup = stateMachine.AddAnyStateTransition(pickup);
        anyPickup.hasExitTime = false;
        anyPickup.duration = 0.02f;
        anyPickup.AddCondition(AnimatorConditionMode.If, 0f, "Pickup");

        var damageToIdle = damage.AddTransition(idle);
        damageToIdle.hasExitTime = true;
        damageToIdle.exitTime = 1f;
        damageToIdle.duration = 0.04f;
        damageToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "ShieldActive");

        var damageToShield = damage.AddTransition(shield);
        damageToShield.hasExitTime = true;
        damageToShield.exitTime = 1f;
        damageToShield.duration = 0.04f;
        damageToShield.AddCondition(AnimatorConditionMode.If, 0f, "ShieldActive");

        var pickupToIdle = pickup.AddTransition(idle);
        pickupToIdle.hasExitTime = true;
        pickupToIdle.exitTime = 1f;
        pickupToIdle.duration = 0.04f;
        pickupToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "ShieldActive");

        var pickupToShield = pickup.AddTransition(shield);
        pickupToShield.hasExitTime = true;
        pickupToShield.exitTime = 1f;
        pickupToShield.duration = 0.04f;
        pickupToShield.AddCondition(AnimatorConditionMode.If, 0f, "ShieldActive");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = "Assets";
        string[] parts = path.Split('/');
        for (int i = 1; i < parts.Length; i++)
        {
            string current = parent + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(current))
            {
                AssetDatabase.CreateFolder(parent, parts[i]);
            }
            parent = current;
        }
    }
}
