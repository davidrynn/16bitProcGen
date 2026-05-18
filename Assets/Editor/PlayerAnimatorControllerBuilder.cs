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

        // Actual subdirectory paths per clip category
        private const string KI_IDLE  = KIBase + "Idles/HumanM@Idle01-Idle02.fbx";
        private const string KI_WALK  = KIBase + "Movement/Walk/HumanM@Walk01_Forward.fbx";
        private const string KI_RUN   = KIBase + "Movement/Run/HumanM@Run01_Forward.fbx";
        private const string KI_FALL  = KIBase + "Movement/Jump/HumanM@Fall01.fbx";
        private const string KI_LAND  = KIBase + "Movement/Jump/HumanM@Jump01 - Land.fbx";

        [MenuItem("Tools/Player/Build Animator Controller")]
        public static void Build()
        {
            // Validate all required clips before touching the asset — prevents writing a broken controller.
            var clipIdle  = LoadClipAt(KI_IDLE,  "Idle01");
            var clipWalk  = LoadClipAt(KI_WALK);
            var clipRun   = LoadClipAt(KI_RUN);
            var clipFall  = LoadClipAt(KI_FALL);
            var clipLand  = LoadClipAt(KI_LAND);

            if (clipIdle == null || clipWalk == null || clipRun == null || clipFall == null || clipLand == null)
            {
                Debug.LogError("[PlayerAnimatorControllerBuilder] One or more required Kevin Iglesias clips are missing — aborting. Import the required packs and retry.");
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
            var stIdle    = AddState(root, "Idle",           0,  0, clipIdle);
            var stLoco    = AddState(root, "LocoBlend",      1,  0, null);
            var stCharge  = AddState(root, "SlingshotCharge",-1,  2, clipIdle);
            var stBRise   = AddState(root, "BallisticRise",  0,  2, null);   // T-pose (no clip)
            var stFall    = AddState(root, "Falling",        1,  2, clipFall);
            var stGCharge = AddState(root, "GlideCharging",  2,  2, clipFall);
            var stGlide   = AddState(root, "Gliding",        3,  2, clipGlide);
            var stTherm   = AddState(root, "ThermalBoost",   4,  2, clipFall);
            var stLand    = AddState(root, "Landing",        0, -1, clipLand);
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
            var tLandIdle = stLand.AddTransition(stIdle);
            tLandIdle.hasExitTime = true;
            tLandIdle.exitTime    = 0.9f;
            tLandIdle.duration    = 0.1f;

            // Idle ↔ LocoBlend
            AddTrans(stIdle, stLoco, 0.1f).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            var tLocoIdle = AddTrans(stLoco, stIdle, 0.1f);
            tLocoIdle.AddCondition(AnimatorConditionMode.Less,   0.05f, "Speed");
            tLocoIdle.AddCondition(AnimatorConditionMode.Equals, 0,     "MovementMode");

            // Idle → Airborne states
            AddTrans(stIdle, stCharge, 0.1f).AddCondition(AnimatorConditionMode.Equals, 1, "MovementMode");
            var tIdleBRise = AddTrans(stIdle, stBRise, 0.1f);
            tIdleBRise.AddCondition(AnimatorConditionMode.Equals, 2, "MovementMode");
            tIdleBRise.AddCondition(AnimatorConditionMode.If,     0, "BallisticRising");
            var tIdleFall = AddTrans(stIdle, stFall, 0.1f);
            tIdleFall.AddCondition(AnimatorConditionMode.Equals, 2, "MovementMode");
            tIdleFall.AddCondition(AnimatorConditionMode.IfNot,  0, "BallisticRising");
            AddTrans(stIdle, stGCharge, 0.1f).AddCondition(AnimatorConditionMode.Equals, 3, "MovementMode");
            AddTrans(stIdle, stGlide,   0.2f).AddCondition(AnimatorConditionMode.Equals, 4, "MovementMode");
            AddTrans(stIdle, stTherm,   0.1f).AddCondition(AnimatorConditionMode.Equals, 5, "MovementMode");

            // LocoBlend → Airborne states
            AddTrans(stLoco, stCharge, 0.1f).AddCondition(AnimatorConditionMode.Equals, 1, "MovementMode");
            var tLocoBRise = AddTrans(stLoco, stBRise, 0.1f);
            tLocoBRise.AddCondition(AnimatorConditionMode.Equals, 2, "MovementMode");
            tLocoBRise.AddCondition(AnimatorConditionMode.If,     0, "BallisticRising");
            var tLocoFall = AddTrans(stLoco, stFall, 0.1f);
            tLocoFall.AddCondition(AnimatorConditionMode.Equals, 2, "MovementMode");
            tLocoFall.AddCondition(AnimatorConditionMode.IfNot,  0, "BallisticRising");
            AddTrans(stLoco, stGCharge, 0.1f).AddCondition(AnimatorConditionMode.Equals, 3, "MovementMode");
            AddTrans(stLoco, stGlide,   0.2f).AddCondition(AnimatorConditionMode.Equals, 4, "MovementMode");
            AddTrans(stLoco, stTherm,   0.1f).AddCondition(AnimatorConditionMode.Equals, 5, "MovementMode");

            // Airborne → Ground
            AddTrans(stCharge,  stIdle, 0.15f).AddCondition(AnimatorConditionMode.Equals, 0, "MovementMode");
            AddTrans(stBRise,   stIdle, 0.15f).AddCondition(AnimatorConditionMode.Equals, 0, "MovementMode");
            AddTrans(stFall,    stIdle, 0.15f).AddCondition(AnimatorConditionMode.Equals, 0, "MovementMode");
            AddTrans(stGCharge, stIdle, 0.15f).AddCondition(AnimatorConditionMode.Equals, 0, "MovementMode");
            AddTrans(stGlide,   stIdle, 0.15f).AddCondition(AnimatorConditionMode.Equals, 0, "MovementMode");
            AddTrans(stTherm,   stIdle, 0.15f).AddCondition(AnimatorConditionMode.Equals, 0, "MovementMode");

            // BallisticRise ↔ Falling (vy sign flip)
            AddTrans(stBRise, stFall, 0.1f).AddCondition(AnimatorConditionMode.IfNot, 0, "BallisticRising");
            var tFallToRise = AddTrans(stFall, stBRise, 0.1f);
            tFallToRise.AddCondition(AnimatorConditionMode.Equals, 2, "MovementMode");
            tFallToRise.AddCondition(AnimatorConditionMode.If,     0, "BallisticRising");

            // SlingshotCharge → Ballistic
            var tChToRise = AddTrans(stCharge, stBRise, 0.1f);
            tChToRise.AddCondition(AnimatorConditionMode.Equals, 2, "MovementMode");
            tChToRise.AddCondition(AnimatorConditionMode.If,     0, "BallisticRising");
            var tChToFall = AddTrans(stCharge, stFall, 0.1f);
            tChToFall.AddCondition(AnimatorConditionMode.Equals, 2, "MovementMode");
            tChToFall.AddCondition(AnimatorConditionMode.IfNot,  0, "BallisticRising");

            // GlideCharging → Gliding
            AddTrans(stGCharge, stGlide, 0.2f).AddCondition(AnimatorConditionMode.Equals, 4, "MovementMode");

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
                $"  Fall  : {clipFall?.name  ?? "MISSING"}\n" +
                $"  Land  : {clipLand?.name  ?? "MISSING"}\n" +
                $"  Glide : {clipGlide?.name ?? "MISSING"}" +
                (glideIsPlaceholder ? " (placeholder=Fall01)" : " (custom Glide_Pose.anim)")
            );
        }

        private static AnimationClip LoadClipAt(string fbxPath, string preferredName = null)
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                                   .OfType<AnimationClip>()
                                   .Where(c => !c.name.StartsWith("__preview__"))
                                   .ToArray();
            AnimationClip clip = null;
            if (preferredName != null)
                clip = all.FirstOrDefault(c => c.name == preferredName);
            if (clip == null)
                clip = all.FirstOrDefault();
            if (clip == null)
                Debug.LogWarning($"[PlayerAnimatorControllerBuilder] No clip at: {fbxPath}");
            return clip;
        }

        private static AnimatorState AddState(AnimatorStateMachine sm, string name, int col, int row, AnimationClip clip)
        {
            var st = sm.AddState(name, new Vector3(col * 280f, row * 90f, 0f));
            st.motion = clip;
            return st;
        }

        private static AnimatorStateTransition AddTrans(AnimatorState from, AnimatorState to, float duration)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration    = duration;
            t.offset      = 0f;
            return t;
        }
    }
}
