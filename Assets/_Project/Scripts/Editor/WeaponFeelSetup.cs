using HrdnLab.FpsWave.Player;
using HrdnLab.FpsWave.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HrdnLab.FpsWave.Editor
{
    /// <summary>
    /// Sensazione delle armi: aggiunge <see cref="WeaponFeelDriver"/> al Player (collegato a PlayerWeapons) e dà a ogni
    /// WeaponDefinition in Data/Weapons il profilo di partenza (automatica o semi-automatica). Dopo, ogni arma si ritocca
    /// nel suo asset (sezione "Sensazione"): rieseguire questo strumento riporta i profili ai valori di partenza.
    /// Richiamato anche da "3. Build M1 Content".
    /// </summary>
    public static class WeaponFeelSetup
    {
        private const string WeaponsDir = "Assets/_Project/Data/Weapons";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";

        [MenuItem("FpsWave/Setup/8. Apply Weapon Feel")]
        public static void ApplyMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Apply(applyPresets: true);
        }

        public static void Apply(bool applyPresets)
        {
            int weapons = 0;
            if (applyPresets)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:WeaponDefinition", new[] { WeaponsDir }))
                {
                    var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                    if (definition == null)
                        continue;
                    definition.EditorSetFeel(definition.Automatic ? WeaponFeel.AutomaticPreset() : WeaponFeel.SemiAutoPreset());
                    if (definition.Automatic)
                        definition.EditorSetSounds(HrdnLab.FpsWave.Audio.SoundId.RifleShotClose, HrdnLab.FpsWave.Audio.SoundId.RifleShotFar);
                    else
                        definition.EditorSetSounds(HrdnLab.FpsWave.Audio.SoundId.PistolShotClose, HrdnLab.FpsWave.Audio.SoundId.PistolShotFar);
                    EditorUtility.SetDirty(definition);
                    weapons++;
                }
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                if (!root.TryGetComponent(out WeaponFeelDriver driver))
                    driver = root.AddComponent<WeaponFeelDriver>();
                driver.EditorSetup(root.GetComponent<PlayerLook>(), root.GetComponent<PlayerMotor>(), root.GetComponent<PlayerInputReader>());
                if (root.TryGetComponent(out PlayerWeapons playerWeapons))
                {
                    var so = new SerializedObject(playerWeapons);
                    SerializedProperty feel = so.FindProperty("_feel");
                    if (feel != null)
                        feel.objectReferenceValue = driver;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[FpsWave] Sensazione armi: WeaponFeelDriver sul Player, profili di partenza applicati a {weapons} armi.");
        }
    }
}
