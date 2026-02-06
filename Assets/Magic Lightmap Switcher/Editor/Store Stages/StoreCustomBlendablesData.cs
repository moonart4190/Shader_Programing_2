#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MagicLightmapSwitcher
{
    public class StoreCustomBlendablesData
    {
        public IEnumerator Execute(StoredLightmapData lightmapData, MagicLightmapSwitcher mainComponent)
        {
            MLSProgressBarHelper.StartNewStage("Storing Custom Blendable Data...");

            yield return null;

            Object[] customBlendables = Object.FindObjectsOfType(typeof(MLSCustomBlendable));

            List<StoredLightmapData.CustomBlendableData> customBlendableDatas = new List<StoredLightmapData.CustomBlendableData>(customBlendables.Length);

            int counter = 0;

            foreach (MLSCustomBlendable customBlendable in customBlendables)
            {
                if (mainComponent.workflow == MagicLightmapSwitcher.Workflow.MultiScene)
                {
                    if (customBlendable.gameObject.scene != EditorSceneManager.GetSceneAt(MLSManager.selectedScene))
                    {
                        continue;
                    }
                }

                customBlendableDatas.Add(new StoredLightmapData.CustomBlendableData());
                customBlendable.GetSharedParameters();

                customBlendableDatas[counter].sourceScriptName = customBlendable.name;
                customBlendableDatas[counter].lightmapName = lightmapData.dataName;
                customBlendableDatas[counter].sourceScriptId = customBlendable.sourceScriptId;
                customBlendableDatas[counter].blendableFloatFieldsDatas = new StoredLightmapData.CustomBlendableData.BlendableFloatFieldData[customBlendable.blendableFloatFields.Count];
                customBlendableDatas[counter].blendableCubemapFieldsDatas = new StoredLightmapData.CustomBlendableData.BlendableCubemapFieldData[customBlendable.blendableCubemapFields.Count];
                customBlendableDatas[counter].blendableColorFieldsDatas = new StoredLightmapData.CustomBlendableData.BlendableColorFieldData[customBlendable.blendableColorFields.Count];
                customBlendableDatas[counter].blendableBoolFieldsDatas = new StoredLightmapData.CustomBlendableData.BlendableBoolFieldData[customBlendable.blendableBoolFields.Count];

                for (int i = 0; i < customBlendable.blendableFloatFields.Count; i++)
                {
                    customBlendableDatas[counter].blendableFloatFieldsDatas[i] = new StoredLightmapData.CustomBlendableData.BlendableFloatFieldData();
                    customBlendableDatas[counter].blendableFloatFieldsDatas[i].fieldName = customBlendable.blendableFloatFields[i].Name;
                    customBlendableDatas[counter].blendableFloatFieldsDatas[i].fieldValue = (float) customBlendable.blendableFloatFields[i].GetValue(customBlendable);
                }

                for (int i = 0; i < customBlendable.blendableCubemapFields.Count; i++)
                {
                    customBlendableDatas[counter].blendableCubemapFieldsDatas[i] = new StoredLightmapData.CustomBlendableData.BlendableCubemapFieldData();
                    customBlendableDatas[counter].blendableCubemapFieldsDatas[i].fieldName = customBlendable.blendableCubemapFields[i].Name;
                    customBlendableDatas[counter].blendableCubemapFieldsDatas[i].fieldValue = (Cubemap) customBlendable.blendableCubemapFields[i].GetValue(customBlendable);
                }

                for (int i = 0; i < customBlendable.blendableColorFields.Count; i++)
                {
                    customBlendableDatas[counter].blendableColorFieldsDatas[i] = new StoredLightmapData.CustomBlendableData.BlendableColorFieldData();
                    customBlendableDatas[counter].blendableColorFieldsDatas[i].fieldName = customBlendable.blendableColorFields[i].Name;
                    customBlendableDatas[counter].blendableColorFieldsDatas[i].fieldValue = (Color) customBlendable.blendableColorFields[i].GetValue(customBlendable);
                }

                for (int i = 0; i < customBlendable.blendableBoolFields.Count; i++)
                {
                    customBlendableDatas[counter].blendableBoolFieldsDatas[i] = new StoredLightmapData.CustomBlendableData.BlendableBoolFieldData();
                    customBlendableDatas[counter].blendableBoolFieldsDatas[i].fieldName = customBlendable.blendableBoolFields[i].Name;
                    customBlendableDatas[counter].blendableBoolFieldsDatas[i].fieldValue = (bool) customBlendable.blendableBoolFields[i].GetValue(customBlendable);
                }

                counter++;

                if (UnityEditorInternal.InternalEditorUtility.isApplicationActive)
                {
                    if (MLSProgressBarHelper.UpdateProgress(customBlendables.Length, 0))
                    {
                        yield return null;
                    }
                }
            }

            lightmapData.sceneLightingData.customBlendableDatas = customBlendableDatas.ToArray();
            MLSLightmapDataStoring.stageExecuting = false;
        }
    }
}
#endif
