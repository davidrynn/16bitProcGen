using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DOTS.Player.Editor
{
    /// <summary>
    /// Builds PlayerAnimatorController.controller from Kevin Iglesias Male clips.
    /// Run via Tools > Player > Build Animator Controller. Re-runnable: preserves the asset GUID
    /// so Inspector references survive a rebuild.
    /// </summary>
    public static class PlayerAnimatorControllerBuilder
    {
        private const string ControllerPath = "Assets/Scripts/Player/Animation/PlayerAnimatorController.controller";
        private const string KIBase = "Assets/Kevin Iglesias/Human Animations/Animations/Male/";
        private const string GlidePosePath = "Assets/Kevin Iglesias/Glide_Pose.anim";
        private const string BoxPlayerPath = "Assets/BoxPlayer.fbx";

        // Actual subdirectory paths per clip category
        private const string KI_IDLE  = KIBase + "Idles/HumanM@Idle01-Idle02.fbx";
        private const string KI_WALK  = KIBase + "Movement/Walk/HumanM@Walk01_Forward.fbx";
        private const string KI_RUN   = KIBase + "Movement/Run/HumanM@Run01_Forward.fbx";
        private const string KI_FALL  = KIBase + "Movement/Jump/HumanM@Fall01.fbx";
        private const string KI_JUMP_BEGIN = KIBase + "Movement/Jump/HumanM@Jump01 - Begin.fbx";
        private const string KI_LAND  = KIBase + "Movement/Jump/HumanM@Jump01 - Land.fbx";

        [MenuItem("Tools/Player/Build Animator Controller")]
        public static void Build()
        {
            // Validate all required clips before touching the asset — prevents writing a broken controller.
            var clipIdle  = LoadClipAt(KI_IDLE,  "Idle01");
            var clipWalk  = LoadClipAt(KI_WALK);
            var clipRun   = LoadClipAt(KI_RUN);
            var clipFall  = LoadClipAt(KI_FALL);
            var clipJumpBegin = LoadClipAt(KI_JUMP_BEGIN);
            var clipLand  = LoadClipAt(KI_LAND);
            var clipChargeStart = LoadClipByHints(
                BoxPlayerPath,
                "Player_Slingshot_Charge_Start",
                "Slingshot_Charge_Start",
                "Charge_Start");
            var clipChargeHold = LoadClipByHints(
                BoxPlayerPath,
                "Player_Slingshot_Charge_Hold",
                "Slingshot_Charge_Hold",
                "Charge_Hold");
            var clipChargeRelease = LoadClipByHints(
                BoxPlayerPath,
                "Player_Slingshot_Release",
                "Slingshot_Release",
                "Charge_Release",
                "Release");

            if (clipIdle == null ||
                clipWalk == null ||
                clipRun == null ||
                clipFall == null ||
                clipJumpBegin == null ||
                clipLand == null ||
                clipChargeStart == null ||
                clipChargeHold == null ||
                clipChargeRelease == null)
            {
                Debug.LogError("[PlayerAnimatorControllerBuilder] One or more required animator clips are missing — aborting. Import the required packs/clips and retry.");
                return;
            }

            var clipGlide = AssetDatabase.LoadAssetAtPath<AnimationClip>(GlidePosePath);
            bool glideIsPlaceholder = clipGlide == null;
            if (glideIsPlaceholder)
                clipGlide = clipFall;

            var dir = Path.GetDirectoryName(ControllerPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Delete only the asset file, not the .meta, so Unity reuses the existing GUID and
            // serialized Inspector references (e.g. PlayerVisualSpawner.animatorController) survive a rebuild.
            if (File.Exists(ControllerPath))
            {
                File.Delete(ControllerPath);
                AssetDatabase.Refresh();
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var root = controller.layers[0].stateMachine;

            // Parameters
            controller.AddParameter("Speed",              AnimatorControllerParameterType.Float);
            controller.AddParameter("MovementMode",       AnimatorControllerParameterType.Int);
            controller.AddParameter("ChargingNormalized", AnimatorControllerParameterType.Float);
            controller.AddParameter("LandingTrigger",     AnimatorControllerParameterType.Trigger);
            controller.AddParameter("GroundedBool",       AnimatorControllerParameterType.Bool);
            controller.AddParameter("BallisticRising",    AnimatorControllerParameterType.Bool);

            // States — layout col*280 wide, row*90 tall
            var stIdle         = AddState(root, "Idle",                    0, 0, clipIdle);
            var stLoco         = AddState(root, "LocoBlend",               1, 0, null);
            var stChargeStart  = AddState(root, "Slingshot_Charge_Start", -1, 2, clipChargeStart);
            var stChargeHold   = AddState(root, "Slingshot_Charge_Hold",  -1, 4, clipChargeHold);
            var stRelease      = AddState(root, "Slingshot_Release",       0, 4, clipChargeRelease);
            var stJump         = AddState(root, "Jump",                    0, 1, clipJumpBegin);
            var stBRise        = AddState(root, "BallisticRise",           0, 2, clipFall);
            var stFall         = AddState(root, "Falling",                 1, 2, clipFall);
            var stGCharge      = AddState(root, "GlideCharging",           2, 2, clipFall);
            var stGlide        = AddState(root, "Gliding",                 3, 2, clipGlide);
            var stTherm        = AddState(root, "ThermalBoost",            4, 2, clipFall);
            var stLand         = AddState(root, "Landing",                 0, -1, clipLand);
            root.defaultState = stIdle;

            // Walk/Run blend tree on LocoBlend state
            var blendTree = new BlendTree
            {
                name                   = "Walk_Run_BlendTree",
                blendParameter         = "Speed",
                blendType              = BlendTreeType.Simple1D,
                useAutomaticThresholds = false
            };
            blendTree.AddChild(clipWalk, 0.3f);
            blendTree.AddChild(clipRun,  1.0f);
            AssetDatabase.AddObjectToAsset(blendTree, controller);
            blendTree.hideFlags = HideFlags.HideInHierarchy;
            stLoco.motion = blendTree;

            // Any → Landing (LandingTrigger, no self-loop)
            var tAnyLand = root.AddAnyStateTransition(stLand);
            tAnyLand.AddCondition(AnimatorConditionMode.If, 0, "LandingTrigger");
            tAnyLand.hasExitTime         = false;
            tAnyLand.duration            = 0.05f;
            tAnyLand.canTransitionToSelf = false;

            // Landing → Idle (exit at 90%)
            AddTransition(stLand, stIdle, 0.15f, hasExitTime: true, exitTime: 0.9f);

            // Idle ↔ LocoBlend
            AddTrans(stIdle, stLoco, 0.1f).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            var tLocoIdle = AddTrans(stLoco, stIdle, 0.1f);
            tLocoIdle.AddCondition(AnimatorConditionMode.Less,   0.05f, "Speed");
            tLocoIdle.AddCondition(AnimatorConditionMode.Equals, 0,     "MovementMode");

            // Idle → Airborne states
            AddTrans(stIdle, stChargeStart, 0.1f).AddCondition(AnimatorConditionMode.Equals, 1, "MovementMode");
            var tIdleJump = AddTrans(stIdle, stJump, 0.1f);
            tIdleJump.AddCondition(AnimatorConditionMode.Equals, 2, "MovementMode");
            tIdleJump.AddCondition(AnimatorConditionMode.If, 0, "BallisticRising");
            var tIdleFall = AddTrans(stIdle, stFall, 0.1f);
            tIdleFall.AddCondition(AnimatorConditionMode.Equals, 2, "MovementMode");
            tIdleFall.AddCondition(AnimatorConditionMode.IfNot, 0, "BallisticRising");
            // Guard against the post-landing Mode-hysteresis window: PlayerGroundingSystem
            // holds Mode at Ballistic (2) for ModeDemotionMinGroundedTime after touchdown, so
            // without this a grounded player could momentarily satisfy the →Falling conditions.
            // "Falling while grounded" is never valid; gate on GroundedBool, not clip length.
            tIdleFall.AddCondition(AnimatorConditionMode.IfNot, 0, "GroundedBool");
            AddTrans(stIdle, stGCharge, 0.1f).AddCondition(AnimatorConditionMode.Equals, 3, "MovementMode");
            AddTrans(stIdle, stGlide,   0.2f).AddCondition(AnimatorConditionMode.Equals, 4, "MovementMode");
            AddTrans(stIdle, stTherm,   0.1f).AddCondition(AnimatorConditionMode.Equals, 5, "MovementMode");

            // LocoBlend → Airborne states
            AddTrans(stLoco, stChargeStart, 0.1f).AddCondition(AnimatorConditionMode.Equals, 1, "MovementMode");
            var tLocoJump = AddTrans(stLoco, stJump, 0.1f);
            tLocoJump.AddCondition(AnimatorConditionMode.Equals, 2, "MovementMode");
            tLocoJump.AddCondition(AnimatorConditionMode.If, 0, "BallisticRising");
            var tLocoFall = AddTrans(stLoco, stFall, 0.1f);
            tLocoFall.AddCondition(AnimatorConditionMode.Equals, 2, "MovementMode");
            tLocoFall.AddCondition(AnimatorConditionMode.IfNot, 0, "BallisticRising");
            // See tIdleFall above: never enter Falling while grounded (Mode-hysteresis window).
            tLocoFall.AddCondition(AnimatorConditionMode.IfNot, 0, "GroundedBool");
            AddTrans(stLoco, stGCharge, 0.1f).AddCondition(AnimatorConditionMode.Equals, 3, "MovementMode");
            AddTrans(stLoco, stGlide,   0.2f).AddCondition(AnimatorConditionMode.Equals, 4, "MovementMode");
            AddTrans(stLoco, stTherm,   0.1f).AddCondition(AnimatorConditionMode.Equals, 5, "MovementMode");

            // Slingshot charge sequence
            var tStartHold = AddTransition(stChargeStart, stChargeHold, 0.05f, hasExitTime: true, exitTime: 0.9f);
            tStartHold.AddCondition(AnimatorConditionMode.Equals, 1, "MovementMode");
            AddTrans(stChargeStart, stRelease, 0.05f).AddCondition(AnimatorConditionMode.Equals, 2, "MovementMode");
            AddTrans(stChargeStart, stIdle, 0.1f).AddCondition(AnimatorConditionMode.Equals, 0, "MovementMode");
            AddTrans(stChargeHold, stRelease, 0.05f).AddCondition(AnimatorConditionMode.Equals, 2, "MovementMode");
            AddTrans(stChargeHold, stIdle, 0.1f).AddCondition(AnimatorConditionMode.Equals, 0, "MovementMode");

            var tReleaseRise = AddTransition(stRelease, stBRise, 0.1f, hasExitTime: true, exitTime: 0.9f);
            tReleaseRise.AddCondition(AnimatorConditionMode.Equals, 2, "MovementMode");
            tReleaseRise.AddCondition(AnimatorConditionMode.If, 0, "BallisticRising");
            var tReleaseFall = AddTransition(stRelease, stFall, 0.1f, hasExitTime: true, exitTime: 0.9f);
            tReleaseFall.AddCondition(AnimatorConditionMode.Equals, 2, "MovementMode");
            tReleaseFall.AddCondition(AnimatorConditionMode.IfNot, 0, "BallisticRising");
            AddTrans(stRelease, stIdle, 0.1f).AddCondition(AnimatorConditionMode.Equals, 0, "MovementMode");

            // Airborne traversal graph
            var tJumpRise = AddTransition(stJump, stBRise, 0.05f, hasExitTime: true, exitTime: 0.9f);
            tJumpRise.AddCondition(AnimatorConditionMode.Equals, 2, "MovementMode");
            tJumpRise.AddCondition(AnimatorConditionMode.If, 0, "BallisticRising");
            var tJumpFall = AddTransition(stJump, stFall, 0.05f, hasExitTime: true, exitTime: 0.9f);
            tJumpFall.AddCondition(AnimatorConditionMode.Equals, 2, "MovementMode");
            tJumpFall.AddCondition(AnimatorConditionMode.IfNot, 0, "BallisticRising");
            AddTrans(stJump, stIdle, 0.15f).AddCondition(AnimatorConditionMode.Equals, 0, "MovementMode");
            AddTrans(stJump, stGCharge, 0.15f).AddCondition(AnimatorConditionMode.Equals, 3, "MovementMode");

            AddTrans(stBRise, stFall, 0.1f).AddCondition(AnimatorConditionMode.IfNot, 0, "BallisticRising");
            AddTrans(stBRise, stIdle, 0.15f).AddCondition(AnimatorConditionMode.Equals, 0, "MovementMode");
            AddTrans(stBRise, stGCharge, 0.15f).AddCondition(AnimatorConditionMode.Equals, 3, "MovementMode");
            AddTrans(stFall, stIdle, 0.15f).AddCondition(AnimatorConditionMode.Equals, 0, "MovementMode");
            AddTrans(stFall, stGCharge, 0.15f).AddCondition(AnimatorConditionMode.Equals, 3, "MovementMode");
            AddTrans(stFall, stTherm, 0.1f).AddCondition(AnimatorConditionMode.Equals, 5, "MovementMode");

            AddTrans(stGCharge, stIdle, 0.15f).AddCondition(AnimatorConditionMode.Equals, 0, "MovementMode");
            AddTrans(stGCharge, stFall, 0.1f).AddCondition(AnimatorConditionMode.Equals, 2, "MovementMode");

            // GlideCharging → Gliding
            AddTrans(stGCharge, stGlide, 0.2f).AddCondition(AnimatorConditionMode.Equals, 4, "MovementMode");

            AddTrans(stGlide, stIdle, 0.15f).AddCondition(AnimatorConditionMode.Equals, 0, "MovementMode");
            AddTrans(stGlide, stFall, 0.1f).AddCondition(AnimatorConditionMode.Equals, 2, "MovementMode");

            AddTrans(stTherm, stIdle, 0.15f).AddCondition(AnimatorConditionMode.Equals, 0, "MovementMode");
            AddTrans(stTherm, stFall, 0.1f).AddCondition(AnimatorConditionMode.Equals, 2, "MovementMode");

            // ThermalBoost → Gliding
            AddTrans(stTherm, stGlide, 0.2f).AddCondition(AnimatorConditionMode.Equals, 4, "MovementMode");

            // Save
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[PlayerAnimatorControllerBuilder] DONE -> {ControllerPath}\n" +
                $"  Idle  : {clipIdle?.name  ?? "MISSING"}\n" +
                $"  Walk  : {clipWalk?.name  ?? "MISSING"}\n" +
                $"  Run   : {clipRun?.name   ?? "MISSING"}\n" +
                $"  Jump  : {clipJumpBegin?.name ?? "MISSING"}\n" +
                $"  ChargeStart : {clipChargeStart?.name ?? "MISSING"}\n" +
                $"  ChargeHold  : {clipChargeHold?.name ?? "MISSING"}\n" +
                $"  Release     : {clipChargeRelease?.name ?? "MISSING"}\n" +
                $"  Fall  : {clipFall?.name  ?? "MISSING"}\n" +
                $"  Land  : {clipLand?.name  ?? "MISSING"}\n" +
                $"  Glide : {clipGlide?.name ?? "MISSING"}" +
                (glideIsPlaceholder ? " (placeholder=Fall01)" : " (custom Glide_Pose.anim)")
            );
        }

        private static AnimationClip LoadClipAt(string fbxPath, string preferredName = null)
        {
            var all = LoadClipsAt(fbxPath);
            AnimationClip clip = null;
            if (preferredName != null)
                clip = all.FirstOrDefault(c => c.name == preferredName);
            if (clip == null)
                clip = all.FirstOrDefault();
            if (clip == null)
                Debug.LogWarning($"[PlayerAnimatorControllerBuilder] No clip at: {fbxPath}");
            return clip;
        }

        private static AnimationClip LoadClipByHints(string assetPath, params string[] nameHints)
        {
            var all = LoadClipsAt(assetPath);

            foreach (var hint in nameHints)
            {
                if (string.IsNullOrWhiteSpace(hint))
                {
                    continue;
                }

                var clip = all.FirstOrDefault(c => string.Equals(c.name, hint, StringComparison.OrdinalIgnoreCase));
                if (clip != null)
                {
                    return clip;
                }
            }

            foreach (var hint in nameHints)
            {
                if (string.IsNullOrWhiteSpace(hint))
                {
                    continue;
                }

                var clip = all.FirstOrDefault(c => c.name.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0);
                if (clip != null)
                {
                    return clip;
                }
            }

            Debug.LogWarning(
                $"[PlayerAnimatorControllerBuilder] No clip at '{assetPath}' matched [{string.Join(", ", nameHints.Where(h => !string.IsNullOrWhiteSpace(h)))}]. Available clips: {string.Join(", ", all.Select(c => c.name))}");
            return null;
        }

        private static AnimationClip[] LoadClipsAt(string assetPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(assetPath)
                                .OfType<AnimationClip>()
                                .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal))
                                .ToArray();
        }

        private static AnimatorState AddState(AnimatorStateMachine sm, string name, int col, int row, AnimationClip clip)
        {
            var st = sm.AddState(name, new Vector3(col * 280f, row * 90f, 0f));
            st.motion = clip;
            return st;
        }

        private static AnimatorStateTransition AddTrans(AnimatorState from, AnimatorState to, float duration)
        {
            return AddTransition(from, to, duration, hasExitTime: false, exitTime: 0f);
        }

        private static AnimatorStateTransition AddTransition(
            AnimatorState from,
            AnimatorState to,
            float duration,
            bool hasExitTime,
            float exitTime)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = hasExitTime;
            t.exitTime    = exitTime;
            t.duration    = duration;
            t.offset      = 0f;
            return t;
        }
    }
}
