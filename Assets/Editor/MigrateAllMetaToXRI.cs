using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Reflection;
using System;
using UnityEngine.XR.Interaction.Toolkit;

#if UNITY_EDITOR
public class MigrateAllMetaToXRI
{
    [MenuItem("Tools/Smart Migrate Meta to XRI")]
    public static void SmartMigrateAll()
    {
        if (!EditorUtility.DisplayDialog("Smart Migrate to XRI", 
            "This will convert all Meta Poke Interactables into XRI Simple Interactables across all prefabs and scenes, preserving button events, and then purge the remaining Oculus components. Proceed?", 
            "Yes, Convert & Purge", "Cancel"))
        {
            return;
        }

        int totalMigrated = 0;
        int totalOculusRemoved = 0;
        int totalMissingRemoved = 0;

        // 1. Process all Prefabs
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        int processedPrefabs = 0;
        
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
            
            if (prefabRoot != null)
            {
                bool modified = MigrateGameObject(prefabRoot, ref totalMigrated, ref totalOculusRemoved, ref totalMissingRemoved);
                if (modified)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                }
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            processedPrefabs++;
            EditorUtility.DisplayProgressBar("Smart Migrating", $"Processing Prefabs: {processedPrefabs}/{prefabGuids.Length}", (float)processedPrefabs / prefabGuids.Length);
        }

        // 2. Process all Scenes
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        int processedScenes = 0;

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
                    bool modified = MigrateGameObject(root, ref totalMigrated, ref totalOculusRemoved, ref totalMissingRemoved);
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
            EditorUtility.DisplayProgressBar("Smart Migrating", $"Processing Scenes: {processedScenes}/{sceneGuids.Length}", (float)processedScenes / sceneGuids.Length);
        }

        EditorUtility.ClearProgressBar();

        EditorUtility.DisplayDialog("Migration Complete", 
            $"Smart Migration Successful!\n\nConverted {totalMigrated} Poke Buttons to XRI.\nPurged {totalOculusRemoved} Meta components.\nRemoved {totalMissingRemoved} missing scripts.\n\nChecked {prefabGuids.Length} Prefabs and {sceneGuids.Length} Scenes.", 
            "Awesome!");
            
        Debug.Log($"Smart Migration Complete! Converted {totalMigrated} buttons, Purged {totalOculusRemoved} Meta components, Removed {totalMissingRemoved} missing scripts.");
    }

    private static bool MigrateGameObject(GameObject root, ref int migratedCount, ref int oculusCount, ref int missingCount)
    {
        bool wasModified = false;
        Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);
        
        foreach (Transform t in allTransforms)
        {
            GameObject go = t.gameObject;
            
            // 1. Remove missing scripts
            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            if (removed > 0)
            {
                missingCount += removed;
                wasModified = true;
            }

            // 2. SMART MIGRATION: Map PokeInteractable and EventWrappers to XRSimpleInteractable
            Component eventWrapper = null;
            bool hasPoke = false;
            
            Component[] components = go.GetComponents<Component>();
            foreach(var c in components)
            {
                if (c == null) continue;
                string typeName = c.GetType().Name;
                if (typeName == "InteractableUnityEventWrapper" || typeName == "PointableUnityEventWrapper")
                {
                    eventWrapper = c;
                }
                if (typeName == "PokeInteractable" || typeName == "RayInteractable" || typeName == "InteractableColorVisual")
                {
                    hasPoke = true;
                }
            }

            if (eventWrapper != null || hasPoke)
            {
                XRSimpleInteractable xrInteractable = go.GetComponent<XRSimpleInteractable>();
                if (xrInteractable == null)
                {
                    xrInteractable = go.AddComponent<XRSimpleInteractable>();
                    wasModified = true;
                }
                
                if (go.GetComponent<Collider>() == null)
                {
                    BoxCollider bc = go.AddComponent<BoxCollider>();
                    bc.isTrigger = true;
                    wasModified = true;
                }
                
                if (eventWrapper != null)
                {
                    // Copy _whenUnselect to selectEntered (standard button press mapping)
                    SerializedObject sourceSO = new SerializedObject(eventWrapper);
                    SerializedProperty sourceProp = sourceSO.FindProperty("_whenUnselect");
                    if (sourceProp == null) sourceProp = sourceSO.FindProperty("_whenRelease"); // alternate name
                    
                    if (sourceProp != null)
                    {
                        SerializedObject destSO = new SerializedObject(xrInteractable);
                        SerializedProperty destProp = destSO.FindProperty("m_SelectEntered");
                        if (destProp != null)
                        {
                            SerializedProperty sourceCalls = sourceProp.FindPropertyRelative("m_PersistentCalls.m_Calls");
                            SerializedProperty destCalls = destProp.FindPropertyRelative("m_PersistentCalls.m_Calls");
                            
                            if (sourceCalls != null && destCalls != null && sourceCalls.arraySize > 0)
                            {
                                destCalls.ClearArray();
                                for (int i = 0; i < sourceCalls.arraySize; i++)
                                {
                                    destCalls.InsertArrayElementAtIndex(i);
                                    SerializedProperty sourceItem = sourceCalls.GetArrayElementAtIndex(i);
                                    SerializedProperty destItem = destCalls.GetArrayElementAtIndex(i);
                                    
                                    destItem.FindPropertyRelative("m_Target").objectReferenceValue = sourceItem.FindPropertyRelative("m_Target").objectReferenceValue;
                                    destItem.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = sourceItem.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue;
                                    destItem.FindPropertyRelative("m_MethodName").stringValue = sourceItem.FindPropertyRelative("m_MethodName").stringValue;
                                    destItem.FindPropertyRelative("m_Mode").enumValueIndex = sourceItem.FindPropertyRelative("m_Mode").enumValueIndex;
                                    destItem.FindPropertyRelative("m_CallState").enumValueIndex = sourceItem.FindPropertyRelative("m_CallState").enumValueIndex;
                                    
                                    SerializedProperty sArgs = sourceItem.FindPropertyRelative("m_Arguments");
                                    SerializedProperty dArgs = destItem.FindPropertyRelative("m_Arguments");
                                    dArgs.FindPropertyRelative("m_ObjectArgument").objectReferenceValue = sArgs.FindPropertyRelative("m_ObjectArgument").objectReferenceValue;
                                    dArgs.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue = sArgs.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue;
                                    dArgs.FindPropertyRelative("m_IntArgument").intValue = sArgs.FindPropertyRelative("m_IntArgument").intValue;
                                    dArgs.FindPropertyRelative("m_FloatArgument").floatValue = sArgs.FindPropertyRelative("m_FloatArgument").floatValue;
                                    dArgs.FindPropertyRelative("m_StringArgument").stringValue = sArgs.FindPropertyRelative("m_StringArgument").stringValue;
                                    dArgs.FindPropertyRelative("m_BoolArgument").boolValue = sArgs.FindPropertyRelative("m_BoolArgument").boolValue;
                                }
                                destSO.ApplyModifiedProperties();
                                migratedCount++;
                                wasModified = true;
                            }
                        }
                    }
                }
            }

            // 3. Purge Meta/Oculus components
            foreach (Component comp in go.GetComponents<Component>())
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
