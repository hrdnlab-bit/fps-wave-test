using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HrdnLab.FpsWave.Editor
{
    /// <summary>
    /// Animator del Pilot in terza persona (clip Mixamo "rifle 8-way" retargettate con tools/autorig, dentro Pilot.fbx).
    /// Livello base: blend 2D sulla velocità locale (MoveX, MoveZ), in piedi o accucciato (Crouch), più lo stato in aria.
    /// Livello alto (maschera busto e braccia): ricarica. Le mani restano sull'arma con l'IK di CharacterRig, sopra le clip.
    /// Richiamato da <see cref="PlayerCharacterBuilder"/> (Setup/9) solo se l'FBX ha le clip; senza, resta l'animazione procedurale.
    /// </summary>
    public static class PilotAnimatorBuilder
    {
        private const string AnimationsDir = "Assets/_Project/Art/Animations";
        private const string ControllerPath = AnimationsDir + "/Pilot.controller";
        private const string MaskPath = AnimationsDir + "/Pilot_UpperBody.mask";

        // Metri per unità del modello di tools/autorig: osso della testa a 0,888 unità -> 1,58 m in Unity (Setup/9).
        private const float MetersPerUnit = 1.58f / 0.888f;
        // Velocità naturali delle clip (unità/s, stampate da tools/autorig) e velocità del gioco a cui vanno nel blend.
        private const float WalkUnits = 0.835f, RunUnits = 2.087f, SprintUnits = 3.13f, CrouchUnits = 0.886f;
        private const float WalkSpeed = 1.8f, RunSpeed = 4.5f, SprintSpeed = 7f, CrouchSpeed = 2.5f;

        private static readonly string[] LoopClips =
        {
            "Idle", "WalkF", "WalkB", "WalkL", "WalkR", "RunF", "RunB", "RunL", "RunR", "SprintF",
            "CrouchIdle", "CrouchF", "CrouchB", "CrouchL", "CrouchR", "Air",
        };

        private static readonly string[] UpperBodyBones = { "Spine1", "Spine2", "Neck", "Head", "Shoulder", "Arm", "Hand" };

        /// <summary>Nomi brevi (dopo "|") e loop delle clip di locomozione. Restituisce true se l'FBX contiene le clip.</summary>
        public static bool ConfigureClips(ModelImporter importer)
        {
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
                return false;
            bool changed = importer.clipAnimations == null || importer.clipAnimations.Length != clips.Length;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                string shortName = clip.takeName.Contains("|") ? clip.takeName[(clip.takeName.LastIndexOf('|') + 1)..] : clip.takeName;
                clip.name = shortName;
                clip.loopTime = LoopClips.Contains(shortName);
                clip.loopPose = false;
            }
            if (!changed)
            {
                ModelImporterClipAnimation[] current = importer.clipAnimations;
                for (int i = 0; i < clips.Length; i++)
                    changed |= current[i].name != clips[i].name || current[i].loopTime != clips[i].loopTime;
            }
            if (changed)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
            return clips.Any(c => c.name == "RunF");
        }

        /// <summary>Controller e maschera; null se le clip di base mancano.</summary>
        public static AnimatorController Build(string fbx, Transform animatorRoot, out float reloadClipLength)
        {
            reloadClipLength = 1f;
            AnimationClip[] all = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToArray();
            AnimationClip Clip(string n) => all.FirstOrDefault(c => c.name == n);
            string[] needed = LoopClips.Append("Reload").ToArray();
            string[] missing = needed.Where(n => Clip(n) == null).ToArray();
            if (missing.Length > 0)
            {
                Debug.LogWarning($"[FpsWave] {fbx}: clip mancanti ({string.Join(", ", missing)}), resta l'animazione procedurale.");
                return null;
            }
            reloadClipLength = Clip("Reload").length;
            AvatarMask mask = BuildMask(animatorRoot);

            // Si ricrea solo se le clip sono cambiate (stesso oggetto: i prefab restano collegati).
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null && existing.parameters.Any(p => p.name == "ReloadSpeed") && existing.layers.Length == 2
                && needed.All(n => existing.animationClips.Contains(Clip(n))))
                return existing;
            if (existing != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            AnimatorController c = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            c.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            c.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
            c.AddParameter("Crouch", AnimatorControllerParameterType.Float);
            c.AddParameter(new AnimatorControllerParameter { name = "Grounded", type = AnimatorControllerParameterType.Bool, defaultBool = true });
            c.AddParameter("Reload", AnimatorControllerParameterType.Trigger);
            c.AddParameter(new AnimatorControllerParameter { name = "ReloadSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

            AnimatorStateMachine sm = c.layers[0].stateMachine;
            AnimatorState move = c.CreateBlendTreeInController("Move", out BlendTree crouchTree, 0);
            crouchTree.blendType = BlendTreeType.Simple1D;
            crouchTree.blendParameter = "Crouch";
            crouchTree.useAutomaticThresholds = false;

            BlendTree stand = crouchTree.CreateBlendTreeChild(0f);
            stand.name = "Stand";
            Directional(stand);
            float walk = WalkSpeed / (WalkUnits * MetersPerUnit);
            float run = RunSpeed / (RunUnits * MetersPerUnit);
            float sprint = SprintSpeed / (SprintUnits * MetersPerUnit);
            stand.AddChild(Clip("Idle"), Vector2.zero);
            AddDir(stand, Clip("WalkF"), new Vector2(0f, WalkSpeed), walk);
            AddDir(stand, Clip("WalkB"), new Vector2(0f, -WalkSpeed), walk);
            AddDir(stand, Clip("WalkL"), new Vector2(-WalkSpeed, 0f), walk);
            AddDir(stand, Clip("WalkR"), new Vector2(WalkSpeed, 0f), walk);
            AddDir(stand, Clip("RunF"), new Vector2(0f, RunSpeed), run);
            AddDir(stand, Clip("RunB"), new Vector2(0f, -RunSpeed), run);
            AddDir(stand, Clip("RunL"), new Vector2(-RunSpeed, 0f), run);
            AddDir(stand, Clip("RunR"), new Vector2(RunSpeed, 0f), run);
            AddDir(stand, Clip("SprintF"), new Vector2(0f, SprintSpeed), sprint);

            BlendTree crouch = crouchTree.CreateBlendTreeChild(1f);
            crouch.name = "Crouch";
            Directional(crouch);
            float crouchRate = Mathf.Min(1.45f, CrouchSpeed / (CrouchUnits * MetersPerUnit)); // oltre scivola appena, ma non corre
            crouch.AddChild(Clip("CrouchIdle"), Vector2.zero);
            AddDir(crouch, Clip("CrouchF"), new Vector2(0f, CrouchSpeed), crouchRate);
            AddDir(crouch, Clip("CrouchB"), new Vector2(0f, -CrouchSpeed), crouchRate);
            AddDir(crouch, Clip("CrouchL"), new Vector2(-CrouchSpeed, 0f), crouchRate);
            AddDir(crouch, Clip("CrouchR"), new Vector2(CrouchSpeed, 0f), crouchRate);
            sm.defaultState = move;

            AnimatorState air = sm.AddState("Air");
            air.motion = Clip("Air");
            AnimatorStateTransition toAir = move.AddTransition(air);
            toAir.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
            toAir.hasExitTime = false;
            toAir.duration = 0.15f;
            AnimatorStateTransition land = air.AddTransition(move);
            land.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            land.hasExitTime = false;
            land.duration = 0.1f;

            // Busto e braccia: ricarica sopra la locomozione.
            c.AddLayer("UpperBody");
            AnimatorControllerLayer[] layers = c.layers;
            layers[1].avatarMask = mask;
            // Peso 0 a riposo: lo alza PlayerCharacter durante la ricarica (uno stato vuoto con Write Defaults
            // riporterebbe braccia e busto alla posa di default sopra la locomozione).
            layers[1].defaultWeight = 0f;
            layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
            c.layers = layers;
            AnimatorStateMachine upper = c.layers[1].stateMachine;
            AnimatorState empty = upper.AddState("Empty");
            empty.writeDefaultValues = false;
            upper.defaultState = empty;
            AnimatorState reload = upper.AddState("Reload");
            reload.motion = Clip("Reload");
            reload.speedParameterActive = true;
            reload.speedParameter = "ReloadSpeed";
            AnimatorStateTransition go = upper.AddAnyStateTransition(reload);
            go.AddCondition(AnimatorConditionMode.If, 0f, "Reload");
            go.canTransitionToSelf = false;
            go.hasExitTime = false;
            go.duration = 0.12f;
            AnimatorStateTransition back = reload.AddTransition(empty);
            back.hasExitTime = true;
            back.exitTime = 0.92f;
            back.duration = 0.18f;

            EditorUtility.SetDirty(c);
            AssetDatabase.SaveAssets();
            Debug.Log($"[FpsWave] Pilot.controller: locomozione 8 direzioni (corsa x{run:F2}, sprint x{sprint:F2}), accucciato, aria, ricarica.");
            return c;
        }

        private static void Directional(BlendTree tree)
        {
            tree.blendType = BlendTreeType.FreeformCartesian2D;
            tree.blendParameter = "MoveX";
            tree.blendParameterY = "MoveZ";
            tree.useAutomaticThresholds = false;
        }

        /// <summary>Figlio del blend 2D alla velocità del gioco, con la clip accelerata quanto serve per non far scivolare i piedi.</summary>
        private static void AddDir(BlendTree tree, Motion clip, Vector2 position, float timeScale)
        {
            tree.AddChild(clip, position);
            ChildMotion[] children = tree.children;
            children[^1].timeScale = timeScale;
            tree.children = children;
        }

        /// <summary>Maschera Generic per percorsi: busto alto, collo, testa, spalle, braccia, mani e dita.</summary>
        private static AvatarMask BuildMask(Transform root)
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            if (mask == null)
            {
                mask = new AvatarMask { name = "Pilot_UpperBody" };
                AssetDatabase.CreateAsset(mask, MaskPath);
            }
            var paths = new List<(string path, bool active)>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root)
                    continue;
                string path = AnimationUtility.CalculateTransformPath(t, root);
                bool active = UpperBodyBones.Any(b => t.name.Contains(b)) && !t.name.Contains("UpLeg");
                paths.Add((path, active));
            }
            mask.transformCount = paths.Count;
            for (int i = 0; i < paths.Count; i++)
            {
                mask.SetTransformPath(i, paths[i].path);
                mask.SetTransformActive(i, paths[i].active);
            }
            EditorUtility.SetDirty(mask);
            return mask;
        }
    }
}
