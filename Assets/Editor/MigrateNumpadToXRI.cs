using UnityEngine;
using UnityEditor;
using System.Reflection;
using System;
using UnityEngine.XR.Interaction.Toolkit;

#if UNITY_EDITOR
public class MigrateNumpadToXRI
{
    [MenuItem("Tools/Migrate Numpad to XRI")]
    public static void MigratePrefab()
    {
        string prefabPath = "Assets/Numpad/NumberPad - Prefabs/NumPad With Canvas.prefab";
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        if (prefabRoot == null)
        {
            Debug.LogError($"Could not load prefab at {prefabPath}. Check if the path is correct.");
            return;
        }

        int migratedButtons = 0;
        int removedMissingCount = 0;

        Transform[] allTransforms = prefabRoot.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in allTransforms)
        {
            GameObject go = t.gameObject;
            
            // Remove missing scripts
            removedMissingCount += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);

            // Find Oculus Event Wrapper to extract events
            Component eventWrapper = null;
            Component[] components = go.GetComponents<Component>();
            foreach(var c in components)
            {
                if (c != null && c.GetType().Name == "InteractableUnityEventWrapper")
                {
                    eventWrapper = c;
                    break;
                }
            }

            if (eventWrapper != null)
            {
                // We found a button with Oculus events! Add XRSimpleInteractable
                XRSimpleInteractable xrInteractable = go.GetComponent<XRSimpleInteractable>();
                if (xrInteractable == null)
                {
                    xrInteractable = go.AddComponent<XRSimpleInteractable>();
                }
                
                // Copy the UnityEvent from Oculus _whenUnselect to XRI m_SelectEntered
                SerializedObject sourceSO = new SerializedObject(eventWrapper);
                SerializedProperty sourceProp = sourceSO.FindProperty("_whenUnselect");
                
                if (sourceProp != null)
                {
                    SerializedObject destSO = new SerializedObject(xrInteractable);
                    SerializedProperty destProp = destSO.FindProperty("m_SelectEntered");
                    if (destProp != null)
                    {
                        SerializedProperty sourceCalls = sourceProp.FindPropertyRelative("m_PersistentCalls.m_Calls");
                        SerializedProperty destCalls = destProp.FindPropertyRelative("m_PersistentCalls.m_Calls");
                        
                        if (sourceCalls != null && destCalls != null)
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
                            migratedButtons++;
                        }
                    }
                }
            }

            // Clean up Oculus components
            foreach (Component comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                Type compType = comp.GetType();
                if (compType.Namespace != null && (compType.Namespace.StartsWith("Oculus") || compType.Namespace.StartsWith("Meta")))
                {
                    UnityEngine.Object.DestroyImmediate(comp, true);
                }
            }

            // Ensure physical buttons have colliders for XRI
            XRSimpleInteractable interactable = go.GetComponent<XRSimpleInteractable>();
            if (interactable != null)
            {
                if (go.GetComponent<Collider>() == null)
                {
                    BoxCollider bc = go.AddComponent<BoxCollider>();
                    bc.isTrigger = true;
                }
            }
        }

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);

        Debug.Log($"Migration Complete! Successfully migrated {migratedButtons} physical buttons to XRI XRSimpleInteractable.");
    }
}
#endif
