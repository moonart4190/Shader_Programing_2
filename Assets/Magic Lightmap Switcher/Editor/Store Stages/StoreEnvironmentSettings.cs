using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MagicLightmapSwitcher
{
    public class StoreEnvironmentSettings
    {
        public IEnumerator Execute(StoredLightmapData lightmapData, MagicLightmapSwitcher mainComponent)
        {
            MLSProgressBarHelper.StartNewStage("Storing Environment Settings...");

            if (RenderSettings.skybox != null)
            {
                StoredLightmapData.SkyboxSettings skyboxSettings = new StoredLightmapData.SkyboxSettings();

                for (int i = 0; i < RenderSettings.skybox.shader.GetPropertyCount(); i++)
                {
                    var properties = new StoredLightmapData.SkyboxSettings.SkyboxShaderProperty();

                    properties.Create(RenderSettings.skybox, RenderSettings.skybox.shader, i);
                    skyboxSettings.skyboxShaderProperties.Add(properties);
                }

                //if (RenderSettings.skybox.HasProperty("_Tex"))
                //{
                //    skyboxSettings.skyboxTexture = RenderSettings.skybox.GetTexture("_Tex") as Cubemap;
                //}

                //if (RenderSettings.skybox.HasProperty("_Tint"))
                //{
                //    skyboxSettings.tintColor = RenderSettings.skybox.GetColor("_Tint");
                //}

                //if (RenderSettings.skybox.HasProperty("_Exposure"))
                //{
                //    skyboxSettings.exposure = RenderSettings.skybox.GetFloat("_Exposure");                    
                //}

                lightmapData.sceneLightingData.skyboxSettings = skyboxSettings;
            }

            lightmapData.sceneLightingData.fogSettings.enabled = RenderSettings.fog;
            lightmapData.sceneLightingData.fogSettings.fogColor = RenderSettings.fogColor;
            lightmapData.sceneLightingData.fogSettings.fogDensity = RenderSettings.fogDensity;

            lightmapData.sceneLightingData.environmentSettings.source = RenderSettings.ambientMode;
            //lightmapData.sceneLightingData.environmentSettings.sunSource = RenderSettings.sun;
            lightmapData.sceneLightingData.environmentSettings.intensityMultiplier = RenderSettings.ambientIntensity;
            lightmapData.sceneLightingData.environmentSettings.ambientColor = RenderSettings.ambientLight;
            lightmapData.sceneLightingData.environmentSettings.skyColor = RenderSettings.ambientSkyColor;
            lightmapData.sceneLightingData.environmentSettings.equatorColor = RenderSettings.ambientEquatorColor;
            lightmapData.sceneLightingData.environmentSettings.groundColor = RenderSettings.ambientGroundColor;

            if (UnityEditorInternal.InternalEditorUtility.isApplicationActive)
            {
                if (MLSProgressBarHelper.UpdateProgress(1, 0))
                {
                    yield return null;
                }
            }

            MLSLightmapDataStoring.stageExecuting = false;
        }
    }
}