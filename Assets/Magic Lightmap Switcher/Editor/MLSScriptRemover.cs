using UnityEngine;
using UnityEditor;
using MagicLightmapSwitcher;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
namespace MagicLightmapSwitcher
{
    public class MLSScriptRemover : EditorWindow
    {
        private Vector2 scrollPosition;
        private List<MLSLight> allLights = new List<MLSLight>();
        private List<MLSStaticRenderer> allStaticRenderers = new List<MLSStaticRenderer>();
        private List<MLSDynamicRenderer> allDynamicRenderers = new List<MLSDynamicRenderer>();
        private List<MLSObject> allMLSObjects = new List<MLSObject>();
        private int removedCount = 0;
        private bool showSelectedOnly = false;
        private enum ComponentType { MLSLight, MLSStaticRenderer, MLSDynamicRenderer, MLSObject }
        private ComponentType selectedComponentType = ComponentType.MLSLight;
        private bool showDebugInfo = false;

        // Add menu item to open the window
        [MenuItem("Tools/Magic Tools/Magic Lightmap Switcher/Clean Options/", priority = 1)]
        [MenuItem("Tools/Magic Tools/Magic Lightmap Switcher/Clean Options/MLS Component Cleaner", priority = 1)]
        public static void ShowWindow()
        {
            // Get existing open window or create a new one
            MLSScriptRemover window = GetWindow<MLSScriptRemover>("MLS Script Remover");
            window.minSize = new Vector2(350, 300);
            window.RefreshComponentsList();
        }

        // Add emergency removal function for MLSStaticRenderer
        [MenuItem("Tools/Magic Tools/Magic Lightmap Switcher/Clean Options/Emergency Remove MLSStaticRenderer", priority = 1)]
        public static void EmergencyRemoveMLSStaticRenderer()
        {
            var components = Resources.FindObjectsOfTypeAll<MLSStaticRenderer>();
            Debug.Log($"Found {components.Length} MLSStaticRenderer components to remove");

            foreach (var component in components)
            {
                if (component != null && component.gameObject.scene.IsValid())
                {
                    Debug.Log($"Removing MLSStaticRenderer from {component.gameObject.name}");
                    DestroyImmediate(component);
                }
            }
        }

        [MenuItem("Tools/Magic Tools/Magic Lightmap Switcher/Clean Options/Emergency Remove MLSDynamicRenderer", priority = 1)]
        public static void EmergencyRemoveMLSDynamicRenderer()
        {
            var components = Resources.FindObjectsOfTypeAll<MLSDynamicRenderer>();
            Debug.Log($"Found {components.Length} MLSDynamicRenderer components to remove");

            foreach (var component in components)
            {
                if (component != null && component.gameObject.scene.IsValid())
                {
                    Debug.Log($"Removing MLSDynamicRenderer from {component.gameObject.name}");
                    DestroyImmediate(component);
                }
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("MLS Script Remover Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("This tool helps you remove MLSLight, MLSStaticRenderer, MLSDynamicRenderer, or MLSObject components from your scene. All operations support Undo (Ctrl+Z/Cmd+Z).", MessageType.Info);
            EditorGUILayout.Space(10);

            // Component selection dropdown
            selectedComponentType = (ComponentType) EditorGUILayout.EnumPopup("Component Type", selectedComponentType);

            // Debug toggle
            showDebugInfo = EditorGUILayout.Toggle("Show Debug Info", showDebugInfo);

            // Quick action buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Remove From All Objects", GUILayout.Height(30)))
            {
                RemoveComponents(false);
            }

            if (GUILayout.Button("Remove From Selected Objects", GUILayout.Height(30)))
            {
                RemoveComponents(true);
            }
            EditorGUILayout.EndHorizontal();

            // Emergency removal button
            if (GUILayout.Button("Emergency Removal (Bypass Normal Process)", GUILayout.Height(25)))
            {
                if (EditorUtility.DisplayDialog("Confirm Emergency Removal",
                    $"This will forcibly remove all {selectedComponentType} components without using the normal method. Continue?",
                    "Yes, Remove All", "Cancel"))
                {
                    PerformEmergencyRemoval();
                }
            }

            EditorGUILayout.Space(15);
            DrawSeparator();
            EditorGUILayout.Space(5);

            // Filter option
            bool newShowSelectedOnly = EditorGUILayout.Toggle("Show Selected Objects Only", showSelectedOnly);
            if (newShowSelectedOnly != showSelectedOnly)
            {
                showSelectedOnly = newShowSelectedOnly;
                RefreshComponentsList();
            }

            // Debug information
            if (showDebugInfo)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Debug Information:", EditorStyles.boldLabel);
                int totalFoundMLSLight = Resources.FindObjectsOfTypeAll<MLSLight>().Length;
                int totalFoundMLSStaticRenderer = Resources.FindObjectsOfTypeAll<MLSStaticRenderer>().Length;
                int totalFoundMLSDynamicRenderer = Resources.FindObjectsOfTypeAll<MLSDynamicRenderer>().Length;
                int totalFoundMLSObject = Resources.FindObjectsOfTypeAll<MLSObject>().Length;

                EditorGUILayout.LabelField($"Total MLSLight components found: {totalFoundMLSLight}");
                EditorGUILayout.LabelField($"Total MLSStaticRenderer components found: {totalFoundMLSStaticRenderer}");
                EditorGUILayout.LabelField($"Total MLSDynamicRenderer components found: {totalFoundMLSDynamicRenderer}");
                EditorGUILayout.LabelField($"Total MLSObject components found: {totalFoundMLSObject}");

                EditorGUILayout.Space(5);
                DrawSeparator();
                EditorGUILayout.Space(5);
            }

            // Manual selection and removal section
            EditorGUILayout.LabelField($"{selectedComponentType} Components In Scene:", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            if ((selectedComponentType == ComponentType.MLSLight && allLights.Count == 0) ||
                (selectedComponentType == ComponentType.MLSStaticRenderer && allStaticRenderers.Count == 0) ||
                (selectedComponentType == ComponentType.MLSDynamicRenderer && allDynamicRenderers.Count == 0) ||
                (selectedComponentType == ComponentType.MLSObject && allMLSObjects.Count == 0))
            {
                EditorGUILayout.HelpBox($"No {selectedComponentType} components found in the scene using standard detection. Try the Emergency Removal option if you're sure the components exist.", MessageType.Info);
            }
            else
            {
                if (GUILayout.Button("Refresh List"))
                {
                    RefreshComponentsList();
                }

                EditorGUILayout.Space(5);
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

                if (selectedComponentType == ComponentType.MLSLight)
                {
                    DisplayComponentList(allLights);
                }
                else if (selectedComponentType == ComponentType.MLSStaticRenderer)
                {
                    DisplayComponentList(allStaticRenderers);
                }
                else if (selectedComponentType == ComponentType.MLSDynamicRenderer)
                {
                    DisplayComponentList(allDynamicRenderers);
                }
                else if (selectedComponentType == ComponentType.MLSObject)
                {
                    DisplayComponentList(allMLSObjects);
                }

                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space(10);

            // Show removal count
            if (removedCount > 0)
            {
                EditorGUILayout.HelpBox($"Removed {removedCount} component(s) in this session.", MessageType.Info);
            }
        }

        private void DisplayComponentList<T>(List<T> components) where T : Component
        {
            foreach (var component in components)
            {
                if (component != null)
                {
                    EditorGUILayout.BeginHorizontal();

                    // Component name and gameobject reference
                    EditorGUILayout.ObjectField(component.gameObject, typeof(GameObject), true);

                    // Remove button for individual component
                    if (GUILayout.Button($"Remove {typeof(T).Name}", GUILayout.Width(170)))
                    {
                        RemoveSingleComponent(component);
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        private void RefreshComponentsList()
        {
            allLights.Clear();
            allStaticRenderers.Clear();
            allMLSObjects.Clear();

            // Debug logging of found components
            if (showDebugInfo)
            {
                var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                int totalMLSLights = 0;
                int totalMLSStaticRenderers = 0;
                int totalMLSObjects = 0;

                foreach (var obj in allObjects)
                {
                    // Skip objects that are not in the scene
                    if (!obj.scene.IsValid())
                        continue;

                    var lightComponents = obj.GetComponents<MLSLight>();
                    var rendererComponents = obj.GetComponents<MLSStaticRenderer>();
                    var objectComponents = obj.GetComponents<MLSObject>();

                    totalMLSLights += lightComponents.Length;
                    totalMLSStaticRenderers += rendererComponents.Length;
                    totalMLSObjects += objectComponents.Length;

                    foreach (var component in lightComponents)
                    {
                        Debug.Log($"Found MLSLight on {obj.name}");
                    }

                    foreach (var component in rendererComponents)
                    {
                        Debug.Log($"Found MLSStaticRenderer on {obj.name}");
                    }

                    foreach (var component in objectComponents)
                    {
                        Debug.Log($"Found MLSObject on {obj.name}");
                    }
                }

                Debug.Log($"Total MLSLight components found: {totalMLSLights}");
                Debug.Log($"Total MLSStaticRenderer components found: {totalMLSStaticRenderers}");
                Debug.Log($"Total MLSObject components found: {totalMLSObjects}");
            }

            if (showSelectedOnly)
            {
                var selectedObjects = Selection.gameObjects;
                if (selectedComponentType == ComponentType.MLSLight)
                    allLights.AddRange(selectedObjects.SelectMany(obj => obj.GetComponents<MLSLight>()));
                else if (selectedComponentType == ComponentType.MLSStaticRenderer)
                    allStaticRenderers.AddRange(selectedObjects.SelectMany(obj => obj.GetComponents<MLSStaticRenderer>()));
                else if (selectedComponentType == ComponentType.MLSObject)
                    allMLSObjects.AddRange(selectedObjects.SelectMany(obj => obj.GetComponents<MLSObject>()));
            }
            else
            {
                if (selectedComponentType == ComponentType.MLSLight)
                    allLights.AddRange(Resources.FindObjectsOfTypeAll<MLSLight>().Where(light => light.gameObject.scene.IsValid()));
                else if (selectedComponentType == ComponentType.MLSStaticRenderer)
                    allStaticRenderers.AddRange(Resources.FindObjectsOfTypeAll<MLSStaticRenderer>().Where(renderer => renderer.gameObject.scene.IsValid()));
                else if (selectedComponentType == ComponentType.MLSObject)
                    allMLSObjects.AddRange(Resources.FindObjectsOfTypeAll<MLSObject>().Where(obj => obj.gameObject.scene.IsValid()));
            }
        }

        private void DrawSeparator()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));
            EditorGUILayout.EndHorizontal();
        }

        private void RemoveComponents(bool selectedOnly)
        {
            int count = 0;

            if (selectedComponentType == ComponentType.MLSLight)
            {
                var lights = selectedOnly
                ? Selection.gameObjects.SelectMany(obj => obj.GetComponents<MLSLight>()).ToArray()
                : Resources.FindObjectsOfTypeAll<MLSLight>().Where(light => light.gameObject.scene.IsValid()).ToArray();

                if (lights.Length > 0)
                {
                    // Register undo operation
                    Undo.RecordObjects(lights, selectedOnly ? "Remove Selected MLSLight Components" : "Remove All MLSLight Components");

                    // Remove components with undo support
                    foreach (var light in lights)
                    {
                        if (light != null)
                        {
                            Undo.DestroyObjectImmediate(light);
                            count++;
                        }
                    }
                }
            }
            else if (selectedComponentType == ComponentType.MLSStaticRenderer)
            {
                var staticRenderers = selectedOnly
                ? Selection.gameObjects.SelectMany(obj => obj.GetComponents<MLSStaticRenderer>()).ToArray()
                : Resources.FindObjectsOfTypeAll<MLSStaticRenderer>().Where(renderer => renderer.gameObject.scene.IsValid()).ToArray();

                if (staticRenderers.Length > 0)
                {
                    // Register undo operation
                    Undo.RecordObjects(staticRenderers, selectedOnly ? "Remove Selected MLSStaticRenderer Components" : "Remove All MLSStaticRenderer Components");

                    // Remove components with undo support
                    foreach (var renderer in staticRenderers)
                    {
                        if (renderer != null)
                        {
                            Undo.DestroyObjectImmediate(renderer);
                            count++;
                        }
                    }
                }
            }
            else if (selectedComponentType == ComponentType.MLSObject)
            {
                var mlsObjects = selectedOnly
                ? Selection.gameObjects.SelectMany(obj => obj.GetComponents<MLSObject>()).ToArray()
                : Resources.FindObjectsOfTypeAll<MLSObject>().Where(obj => obj.gameObject.scene.IsValid()).ToArray();

                if (mlsObjects.Length > 0)
                {
                    // Register undo operation
                    Undo.RecordObjects(mlsObjects, selectedOnly ? "Remove Selected MLSObject Components" : "Remove All MLSObject Components");

                    // Remove components with undo support
                    foreach (var obj in mlsObjects)
                    {
                        if (obj != null)
                        {
                            Undo.DestroyObjectImmediate(obj);
                            count++;
                        }
                    }
                }
            }

            if (count > 0)
            {
                removedCount += count;
                RefreshComponentsList();
                Debug.Log($"Removed {count} {selectedComponentType} components from {(selectedOnly ? "selected objects" : "all objects in the scene")}.");
            }
            else
            {
                Debug.Log($"No {selectedComponentType} components found on {(selectedOnly ? "selected objects" : "all objects in the scene")}.");
            }
        }

        private void RemoveSingleComponent<T>(T component) where T : Component
        {
            // Register undo operation
            Undo.RecordObject(component, $"Remove Single {typeof(T).Name} Component");
            Undo.DestroyObjectImmediate(component);
            removedCount++;
            RefreshComponentsList();
        }

        private void PerformEmergencyRemoval()
        {
            int count = 0;

            if (selectedComponentType == ComponentType.MLSLight)
            {
                var components = Resources.FindObjectsOfTypeAll<MLSLight>();
                foreach (var component in components)
                {
                    if (component != null && component.gameObject.scene.IsValid())
                    {
                        DestroyImmediate(component);
                        count++;
                    }
                }
            }
            else if (selectedComponentType == ComponentType.MLSStaticRenderer)
            {
                var components = Resources.FindObjectsOfTypeAll<MLSStaticRenderer>();
                foreach (var component in components)
                {
                    if (component != null && component.gameObject.scene.IsValid())
                    {
                        DestroyImmediate(component);
                        count++;
                    }
                }
            }
            else if (selectedComponentType == ComponentType.MLSObject)
            {
                var components = Resources.FindObjectsOfTypeAll<MLSObject>();
                foreach (var component in components)
                {
                    if (component != null && component.gameObject.scene.IsValid())
                    {
                        DestroyImmediate(component);
                        count++;
                    }
                }
            }

            removedCount += count;
            RefreshComponentsList();
            Debug.Log($"Emergency removal: Removed {count} {selectedComponentType} components from scene.");
        }
    }
}
#endif