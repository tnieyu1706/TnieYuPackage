using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TnieYuPackage.Tools.SAB.AnimationBuilder
{
    public static class AnimationClipExporter
    {
        public static bool Export(ClipSet clips, Sprite[] sprites, string outputFolder, string controllerName, int sampleRate, bool loop)
        {
            var exportedClips = BuildClipAssets(clips, sprites, outputFolder, controllerName, sampleRate, loop);
            if (exportedClips == null) return false;

            string controllerPath = $"{outputFolder}/{controllerName}.controller";
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var stateMachine = controller.layers[0].stateMachine;

            AnimatorState defaultState = null;
            foreach (var animClip in exportedClips)
            {
                var state = controller.AddMotion(animClip);
                if (defaultState == null) defaultState = state;
            }

            for (int i = stateMachine.states.Length - 1; i >= 0; i--)
            {
                var state = stateMachine.states[i].state;
                if (state.motion == null)
                    stateMachine.RemoveState(state);
            }

            if (defaultState != null)
                stateMachine.defaultState = defaultState;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SpriteAnimationBuilder] Created controller: {controllerPath}");
            return true;
        }

        public static bool ExportOverride(ClipSet clips, Sprite[] sprites, string outputFolder, string controllerName, int sampleRate, bool loop, AnimatorController baseController)
        {
            if (baseController == null)
                return Fail("Base controller is null.");

            var exportedClips = BuildClipAssets(clips, sprites, outputFolder, controllerName, sampleRate, loop);
            if (exportedClips == null) return false;

            var overrideController = new AnimatorOverrideController
            {
                runtimeAnimatorController = baseController
            };

            var baseClips = baseController.animationClips;
            int overridden = 0;
            foreach (var animClip in exportedClips)
            {
                var baseClip = baseClips.FirstOrDefault(c => c.name == animClip.name);
                if (baseClip == null)
                {
                    Debug.LogWarning($"[SpriteAnimationBuilder] No clip named '{animClip.name}' in base controller; skipped.");
                    continue;
                }

                overrideController[baseClip] = animClip;
                overridden++;
            }

            if (overridden == 0)
                return Fail("No exported clip matched any clip in the base controller.");

            string overridePath = $"{outputFolder}/{controllerName}.overrideController";
            AssetDatabase.CreateAsset(overrideController, overridePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SpriteAnimationBuilder] Created override controller: {overridePath}");
            return true;
        }

        private static List<AnimationClip> BuildClipAssets(ClipSet clips, Sprite[] sprites, string outputFolder, string controllerName, int sampleRate, bool loop)
        {
            if (clips == null || sprites == null || sprites.Length == 0)
            {
                Fail("Sprites are empty.");
                return null;
            }

            if (string.IsNullOrEmpty(outputFolder))
            {
                Fail("Output folder is not set.");
                return null;
            }

            if (string.IsNullOrEmpty(controllerName))
            {
                Fail("Controller name is empty.");
                return null;
            }

            if (!AssetDatabase.IsValidFolder(outputFolder))
            {
                Fail($"Output folder is not a valid asset folder: {outputFolder}");
                return null;
            }

            if (sampleRate <= 0) sampleRate = 10;

            var validClips = clips.clips
                .Where(c => c.frameIndices.Count > 0)
                .ToList();

            if (validClips.Count == 0)
            {
                Fail("No clip has at least one frame.");
                return null;
            }

            string animFolder = Path.Combine(outputFolder, controllerName).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(animFolder))
                AssetDatabase.CreateFolder(outputFolder, controllerName);

            float frameTime = 1f / sampleRate;
            var usedNames = new HashSet<string>();
            var exportedClips = new List<AnimationClip>();

            foreach (var clip in validClips)
            {
                string baseName = SanitizeName(clip.name);
                string uniqueName = baseName;
                int n = 1;
                while (!usedNames.Add(uniqueName))
                    uniqueName = $"{baseName}_{n++}";

                string clipPath = $"{animFolder}/{uniqueName}.anim";
                var animClip = BuildClip(clip, sprites, frameTime, loop);
                if (animClip == null)
                {
                    Debug.LogWarning($"[SpriteAnimationBuilder] Clip '{clip.name}' has no valid frames; skipped.");
                    continue;
                }

                AssetDatabase.CreateAsset(animClip, clipPath);
                exportedClips.Add(animClip);
                Debug.Log($"[SpriteAnimationBuilder] Created clip: {clipPath}");
            }

            if (exportedClips.Count == 0)
            {
                Fail("No clip could be exported.");
                return null;
            }

            return exportedClips;
        }

        private static AnimationClip BuildClip(ClipDefinition clip, Sprite[] sprites, float frameTime, bool loop)
        {
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            var keys = new List<ObjectReferenceKeyframe>();

            foreach (int spriteIndex in clip.frameIndices)
            {
                if (spriteIndex < 0 || spriteIndex >= sprites.Length) continue;
                keys.Add(new ObjectReferenceKeyframe
                {
                    time = keys.Count * frameTime,
                    value = sprites[spriteIndex]
                });
            }

            if (keys.Count == 0) return null;

            var animClip = new AnimationClip { frameRate = 1f / frameTime };
            AnimationUtility.SetObjectReferenceCurve(animClip, binding, keys.ToArray());
            animClip.wrapMode = loop ? WrapMode.Loop : WrapMode.Default;

            var settings = AnimationUtility.GetAnimationClipSettings(animClip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(animClip, settings);

            return animClip;
        }

        private static string SanitizeName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private static bool Fail(string message)
        {
            Debug.LogError($"[SpriteAnimationBuilder] {message}");
            return false;
        }
    }
}