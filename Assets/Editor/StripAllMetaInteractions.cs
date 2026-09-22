using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Reflection;
using System;

#if UNITY_EDITOR
public class StripAllMetaInteractions
{
    [MenuItem("Tools/Strip All Meta Interactions")]
    public static void PurgeAllMetaComponents()
    {
        if (!EditorUtility.DisplayDialog("Purge Meta SDK", 
            "Are you sure you want to completely remove ALL Meta/Oculus components and missing scripts from ALL prefabs and scenes in this project? This cannot be undone automatically.", 
            "Yes, Strip Everything", "Cancel"))
        {
            return;
        }

        int totalOculusComponentsRemoved = 0;
        int totalMissingScriptsRemoved = 0;

        // 1. Process all Prefabs
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        int processedPrefabs = 0;
        
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
            
            if (prefabRoot != null)
            {
                bool modified = CleanGameObject(prefabRoot, ref totalOculusComponentsRemoved, ref totalMissingScriptsRemoved);
                if (modified)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                }
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            processedPrefabs++;
            EditorUtility.DisplayProgressBar("Purging Meta SDK", $"Processing Prefabs: {processedPrefabs}/{prefabGuids.Length}", (float)processedPrefabs / prefabGuids.Length);
        }

        // 2. Process all Scenes
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        int processedScenes = 0;

        // Ask to save current scene first
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        foreach (string guid in sceneGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            
            try 
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                bool sceneModified = false;

                GameObject[] rootObjects = scene.GetRootGameObjects();
                foreach (GameObject root in rootObjects)
                {
                    bool modified = CleanGameObject(root, ref totalOculusComponentsRemoved, ref totalMissingScriptsRemoved);
                    if (modified) sceneModified = true;
                }

                if (sceneModified)
                {
                    EditorSceneManager.SaveScene(scene);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not process scene {path}: {e.Message}");
            }

            processedScenes++;
            EditorUtility.DisplayProgressBar("Purging Meta SDK", $"Processing Scenes: {processedScenes}/{sceneGuids.Length}", (float)processedScenes / sceneGuids.Length);
        }

        EditorUtility.ClearProgressBar();

        EditorUtility.DisplayDialog("Purge Complete", 
            $"Successfully purged the Meta SDK!\n\nRemoved {totalOculusComponentsRemoved} Meta components.\nRemoved {totalMissingScriptsRemoved} missing scripts.\nChecked {prefabGuids.Length} Prefabs and {sceneGuids.Length} Scenes.", 
            "OK");
            
        Debug.Log($"Meta SDK Purge Complete! Removed {totalOculusComponentsRemoved} Oculus components and {totalMissingScriptsRemoved} missing scripts.");
    }

    private static bool CleanGameObject(GameObject root, ref int oculusCount, ref int missingCount)
    {
        bool wasModified = false;
        Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);
        
        foreach (Transform t in allTransforms)
        {
            GameObject go = t.gameObject;
            
            // Remove missing scripts
            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            if (removed > 0)
            {
                missingCount += removed;
                wasModified = true;
            }

            // Remove Oculus components
            Component[] components = go.GetComponents<Component>();
            foreach (Component comp in components)
            {
                if (comp == null) continue;
                
                Type compType = comp.GetType();
                if (compType.Namespace != null && (compType.Namespace.StartsWith("Oculus") || compType.Namespace.StartsWith("Meta")))
                {
                    UnityEngine.Object.DestroyImmediate(comp, true);
                    oculusCount++;
                    wasModified = true;
                }
            }
        }

        return wasModified;
    }
}
#endif
