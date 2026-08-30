using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DungeonCrawler;

namespace DungeonCrawler.EditorTools
{
    // Runs once, automatically, the first time this project opens (or scripts recompile)
    // in the Editor after licensing/sign-in completes. Builds the test scene described in
    // README.md section 3 ("Fastest path to testing") so there's nothing manual left to do
    // before hitting Play. Guards on the scene file's existence so it never overwrites
    // hand-edits made after the first run.
    [InitializeOnLoad]
    public static class AutoTestSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/TestScene.unity";

        static AutoTestSceneSetup()
        {
            if (File.Exists(ScenePath)) return;
            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            if (File.Exists(ScenePath)) return; // re-check post-delay in case of a double callback

            ConfigureSpriteImportSettings();
            BuildTestScene();

            Debug.Log("[AutoTestSceneSetup] Test scene created at " + ScenePath +
                " -- Bootstrap GameObject is in the scene with GameBootstrap attached. Hit Play to test.");
        }

        private static void ConfigureSpriteImportSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Sprites" });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.spritePixelsPerUnit = 16;
                importer.SaveAndReimport();
            }
        }

        private static void BuildTestScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var camGO = new GameObject("Main Camera", typeof(Camera));
            camGO.tag = "MainCamera";
            camGO.AddComponent<AudioListener>();
            camGO.transform.position = new Vector3(0, 10, -6);

            var bootstrapGO = new GameObject("Bootstrap");
            var bootstrap = bootstrapGO.AddComponent<GameBootstrap>();
            bootstrap.classToTest = GameBootstrap.TestClass.Knight;
            bootstrap.spawnAbyssEncounter = true;

            if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
