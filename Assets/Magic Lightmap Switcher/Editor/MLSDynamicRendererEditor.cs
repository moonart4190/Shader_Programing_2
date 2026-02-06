using UnityEditor;
using UnityEngine;

namespace MagicLightmapSwitcher
{
    [CustomEditor(typeof(MLSDynamicRenderer))]
    public class MLSDynamicRendererEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            MLSDynamicRenderer dynamicRenderer = (MLSDynamicRenderer)target;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Script GUID");
                GUILayout.Label(dynamicRenderer.scriptId);
                GUILayout.Space(10);
            }

            using (new EditorGUILayout.VerticalScope())
            {
                GUILayout.Label("Closest Reflection Probes:");
                GUILayout.Label(dynamicRenderer.probeIndexes[0].ToString());
                GUILayout.Label(dynamicRenderer.probeIndexes[1].ToString());
            }
            
#if BAKERY_INCLUDED
            if ((dynamicRenderer.switcherInstance.currentLightmapScenario.blendingModules & (1 << 4)) > 0)
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label(
                        MLSTooltipManager.MainComponent.GetParameter("Bakery Volumes Synch.",
                            MLSTooltipManager.MainComponent.Tabs.LightmapScenario),
                        GUILayout.MinWidth(200));
                    dynamicRenderer.bakeryVolumesBlendingSynch =
                        (MLSObject.BakeryVolumesBlendingSynch) EditorGUILayout.EnumPopup(dynamicRenderer.bakeryVolumesBlendingSynch);

                    switch (dynamicRenderer.bakeryVolumesBlendingSynch)
                    {
                        case MLSObject.BakeryVolumesBlendingSynch.Reflections:
                            dynamicRenderer.propertyBlock.SetInt("_MLS_BAKERY_VOLUMES_SYNCH", 0);
                            break;
                            
                        case MLSObject.BakeryVolumesBlendingSynch.Lightmaps:
                            dynamicRenderer.propertyBlock.SetInt("_MLS_BAKERY_VOLUMES_SYNCH", 1);
                            break;
                    }
                    
                    dynamicRenderer.objectRenderer.SetPropertyBlock(dynamicRenderer.propertyBlock);
                }
            }
#endif

            if (GUILayout.Button("Update GUID"))
            {
                dynamicRenderer.UpdateGUID();
            }
        }
    }
}
