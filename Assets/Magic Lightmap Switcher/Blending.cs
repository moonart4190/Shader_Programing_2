using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static MagicLightmapSwitcher.StoredLightmapData;
using Color = UnityEngine.Color;
using Debug = UnityEngine.Debug;

#if UNITY_EDITOR
using UnityEditor;
#endif

#if MT_HDRP_7_INCLUDED || MT_HDRP_8_INCLUDED || MT_HDRP_9_INCLUDED || MT_HDRP_10_INCLUDED || MT_HDRP_11_INCLUDED || MT_HDRP_12_INCLUDED || MT_HDRP_13_INCLUDED || MT_HDRP_14_INCLUDED
using UnityEngine.Rendering.HighDefinition;
#endif

namespace MagicLightmapSwitcher
{
    public class Blending
    {
        public static Action<float> currentBlendingValue;
        private static bool _propertyIdsInitialized = false;

        #region Shader Properties
        public static int _MLS_ENABLE_LIGHTMAPS_BLENDING;
        public static int _MLS_ENABLE_REFLECTIONS_BLENDING;
        public static int _MLS_ENABLE_SKY_CUBEMAPS_BLENDING;
        public static int _MLS_REFLECTIONS_FLAG;
        public static int _MLS_CURRENT_LIGHTMAP_PAIR;
        public static int _MLS_Lightmaps_Count;
        public static int _MLS_Cubemap_Array;
        public static int _MLS_Reflection_Probe_Positions;
        public static int _MLS_Reflection_Probe_Count;
        public static int _MLS_Lightmap_Color_Blend_From;
        public static int _MLS_Lightmap_Color_Blend_To;
        public static int _MLS_Lightmap_Directional_Blend_From;
        public static int _MLS_Lightmap_Directional_Blend_To;
        public static int _MLS_Lightmap_ShadowMask_Blend_From;
        public static int _MLS_Lightmap_ShadowMask_Blend_To;
        public static int _MLS_Reflection_Blend_From_0;
        public static int _MLS_Reflection_Blend_To_0;
        public static int _MLS_Reflection_Blend_From_1;
        public static int _MLS_Reflection_Blend_To_1;
        public static int _MLS_Lightmaps_Blend_Factor;
        public static int _MLS_Reflections_Blend_Factor;
        public static int _MLS_Sky_Cubemap_Blend_Factor;
        public static int _MLS_Sky_Cubemap_Blend_From;
        public static int _MLS_Sky_Cubemap_Blend_To;
        public static int _MLS_Sky_Blend_From_Exposure;
        public static int _MLS_Sky_Blend_To_Exposure;
        public static int _MLS_Sky_Blend_From_Tint;
        public static int _MLS_Sky_Blend_To_Tint;
        public static int _MLS_SkyReflection_Blend_From;
        public static int _MLS_SkyReflection_Blend_To;
        public static int _MLS_LightCookie_Main_Blend_From;
        public static int _MLS_LightCookie_Main_Blend_To;

        public static int[] _MLS_Lightmap_Color_Array;
        public static int[] _MLS_Lightmap_Directional_Array;
        public static int[] _MLS_Lightmap_ShadowMask_Array;
        public static int[] _MLS_MainLight_Cookie_Array;

#if BAKERY_INCLUDED
        public static int _MLS_BakeryRNM0_Array;
        public static int _MLS_BakeryRNM1_Array;
        public static int _MLS_BakeryRNM2_Array;

        public static int _MLS_BakeryRNM0_From;
        public static int _MLS_BakeryRNM0_To;
        public static int _MLS_BakeryRNM1_From;
        public static int _MLS_BakeryRNM1_To;
        public static int _MLS_BakeryRNM2_From;
        public static int _MLS_BakeryRNM2_To;
        public static int _MLS_BakeryVolume0_From;
        public static int _MLS_BakeryVolume0_To;
        public static int _MLS_BakeryVolume1_From;
        public static int _MLS_BakeryVolume1_To;
        public static int _MLS_BakeryVolume2_From;
        public static int _MLS_BakeryVolume2_To;
        public static int _MLS_BakeryVolumeMask_From;
        public static int _MLS_BakeryVolumMask_To;
        public static int _MLS_BakeryVolumeCompressed_From;
        public static int _MLS_BakeryVolumeCompressed_To;
#endif
        #endregion

        public static Dictionary<string, BlendingOperationalData> blendingOperationalDatas = new Dictionary<string, BlendingOperationalData>();
        public static List<MagicLightmapSwitcher.AffectedObject> _affectedTerrains;
        public static List<MagicLightmapSwitcher.AffectedObject> _resultStaticAffectedObjects;
        public static List<MagicLightmapSwitcher.AffectedObject> _resultDynamicAffectedObjects;
        public static List<MLSLight> _resultAffectedLights;
        public static MLSLight _mainDirectionalLight;
        private static bool lightProbesArrayProcessing;
        private static Queue<BlendProbesThreadData> blendProbesThreadsQueue = new Queue<BlendProbesThreadData>();
        private static Queue<ProbesReplacingThreadData> probesReplacingThreadsQueue = new Queue<ProbesReplacingThreadData>();
        private static ProbesReplacingThreadData lastReplacedProbesData = new ProbesReplacingThreadData();

        public static CubemapArray _cubemapArray;
        public static Vector4[] _reflectionProbePositionsArray;
        public static int _cubemapWidth;
        public static int _cubemapCount;
        public static GraphicsFormat _cubemapFormat;
        public static GraphicsFormat _lightmapFormat;
        public static Texture2DArray[] _lightmapLightArray;
        public static Texture2DArray[] _lightmapDirArray;
        public static Texture2DArray[] _lightmapShadowMaskArray;
        public static Texture2DArray[] _mainLightCookiesArray;
        public static int _lightmapWidth;
        public static int _lightmapHeight;
        public static int _arrayDepth;
        public static int _lightmapCount;
        public static int _currentResolution;

        private static ComputeBuffer _blendLightmapsBuffer;
        private static ComputeShader _lightmapsProcessingComputeShader;
        private static Shader _lightmapsProcessingShader;
        private static Material _tmpMat;
        private static RenderTexture[] _lightmapTextureOutRT;
        private static Texture2D[] _textureFrom, _textureTo, _lightmapTextureColorOut, _lightmapTextureDirectionalOut, _lightmapTextureShadowmaskOut, _reflectionTextureOut;
        private static Cubemap _textureCubeFrom, _textureCubeTo, _textureCubeOut;

        private static MagicLightmapSwitcher _currentSwitcherInstance;
        private static StoredLightingScenario _currentScenario;
        private static float _currentBlendingValue;

#if BAKERY_INCLUDED
        public static Texture2DArray _lightmapBakeryRNM0Array;
        public static Texture2DArray _lightmapBakeryRNM1Array;
        public static Texture2DArray _lightmapBakeryRNM2Array;

        public static int _lightmapRNM_Width;
        public static int _lightmapRNM_Height;
        public static int _lightmapRNM_Depth;
        public static int _lightmapRNM_Count;
#endif        

        private static int _lastFromIndex = -1;

#if MT_HDRP_7_INCLUDED || MT_HDRP_8_INCLUDED || MT_HDRP_9_INCLUDED || MT_HDRP_10_INCLUDED || MT_HDRP_11_INCLUDED || MT_HDRP_12_INCLUDED || MT_HDRP_13_INCLUDED || MT_HDRP_14_INCLUDED
        public static HDRenderPipelineAsset hdRenderPipelineAsset;
        private static HDAdditionalReflectionData hdRelectionProbeData;
#endif
        private static bool isDeferredMode = false;

        public class BlendingOperationalData
        {
            public string sceneName;
            public int loadIndex;
            public int lightProbesArrayIndex;
        }

        public class BlendProbesThreadData
        {
            public bool isBusy;
            public MagicLightmapSwitcher switcherInstance;
            public int lightProbesArrayPosition;
            public float[] blendFromArray;
            public float[] blendToArray;
            public SphericalHarmonicsL2[] sphericalHarmonicsArray;
            public float blendFactor;
        }

        public class ProbesReplacingThreadData
        {
            public bool isBusy;
            public MagicLightmapSwitcher switcherInstance;
            public SphericalHarmonicsL2[] sphericalHarmonicsArray;
            public BlendProbesThreadData lastProbesData;
        }

        public enum LightmapType
        {
            Color,
            Directional,
            Shadowmask
        }

        public static int _tempLightmapTextureRT = Shader.PropertyToID("ResultTexture");

#if UNITY_EDITOR
        public static void InitComputes()
        {
            var lightmapCount = LightmapSettings.lightmaps.Length;
            var path = AssetDatabase.FindAssets("MLSLightmapsBlend", null);
            _lightmapsProcessingComputeShader = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(path[0]), typeof(ComputeShader)) as ComputeShader;

            _lightmapTextureOutRT = new RenderTexture[lightmapCount];
            _lightmapTextureColorOut = new Texture2D[lightmapCount];
            _lightmapTextureDirectionalOut = new Texture2D[lightmapCount];
            _lightmapTextureShadowmaskOut = new Texture2D[lightmapCount];
            _textureFrom = new Texture2D[lightmapCount];
            _textureTo = new Texture2D[lightmapCount];

            for (int i = 0; i < lightmapCount; i++)
            {
                var lightmapTextureSize = LightmapSettings.lightmaps[0].lightmapColor.width;

                _lightmapTextureOutRT[i] = new RenderTexture(lightmapTextureSize, lightmapTextureSize, 0, GraphicsFormat.R32G32B32A32_SFloat);
                _lightmapTextureOutRT[i].enableRandomWrite = true;
                _lightmapTextureOutRT[i].Create();

                _lightmapTextureColorOut[i] = new Texture2D(lightmapTextureSize, lightmapTextureSize, GraphicsFormat.R32G32B32A32_SFloat, TextureCreationFlags.None);
                _lightmapTextureDirectionalOut[i] = new Texture2D(lightmapTextureSize, lightmapTextureSize, GraphicsFormat.R32G32B32A32_SFloat, TextureCreationFlags.None);
                _lightmapTextureShadowmaskOut[i] = new Texture2D(lightmapTextureSize, lightmapTextureSize, GraphicsFormat.R32G32B32A32_SFloat, TextureCreationFlags.None);
            }
        }
#endif

        public static void InitiShaderProperties()
        {
            if (!_propertyIdsInitialized)
            {
                _MLS_ENABLE_LIGHTMAPS_BLENDING = Shader.PropertyToID("_MLS_ENABLE_LIGHTMAPS_BLENDING");
                _MLS_ENABLE_REFLECTIONS_BLENDING = Shader.PropertyToID("_MLS_ENABLE_REFLECTIONS_BLENDING");
                _MLS_ENABLE_SKY_CUBEMAPS_BLENDING = Shader.PropertyToID("_MLS_ENABLE_SKY_CUBEMAPS_BLENDING");
                _MLS_REFLECTIONS_FLAG = Shader.PropertyToID("_MLS_ReflectionsFlag");
                _MLS_CURRENT_LIGHTMAP_PAIR = Shader.PropertyToID("_MLS_CURRENT_LIGHTMAP_PAIR");
                _MLS_Lightmap_Color_Blend_From = Shader.PropertyToID("_MLS_Lightmap_Color_Blend_From");
                _MLS_Lightmap_Color_Blend_To = Shader.PropertyToID("_MLS_Lightmap_Color_Blend_To");
                _MLS_Lightmap_Directional_Blend_From = Shader.PropertyToID("_MLS_Lightmap_Dir_Blend_From");
                _MLS_Lightmap_Directional_Blend_To = Shader.PropertyToID("_MLS_Lightmap_Dir_Blend_To");
                _MLS_Lightmap_ShadowMask_Blend_From = Shader.PropertyToID("_MLS_Lightmap_ShadowMask_Blend_From");
                _MLS_Lightmap_ShadowMask_Blend_To = Shader.PropertyToID("_MLS_Lightmap_ShadowMask_Blend_To");
                _MLS_Reflection_Blend_From_0 = Shader.PropertyToID("_MLS_Reflection_Blend_From_0");
                _MLS_Reflection_Blend_To_0 = Shader.PropertyToID("_MLS_Reflection_Blend_To_0");
                _MLS_Reflection_Blend_From_1 = Shader.PropertyToID("_MLS_Reflection_Blend_From_1");
                _MLS_Reflection_Blend_To_1 = Shader.PropertyToID("_MLS_Reflection_Blend_To_1");
                _MLS_Lightmaps_Blend_Factor = Shader.PropertyToID("_MLS_Lightmaps_Blend_Factor");
                _MLS_Reflections_Blend_Factor = Shader.PropertyToID("_MLS_Reflections_Blend_Factor");
                _MLS_Sky_Cubemap_Blend_Factor = Shader.PropertyToID("_MLS_Sky_Cubemap_Blend_Factor");
                _MLS_Sky_Cubemap_Blend_From = Shader.PropertyToID("_MLS_Sky_Cubemap_Blend_From");
                _MLS_Sky_Cubemap_Blend_To = Shader.PropertyToID("_MLS_Sky_Cubemap_Blend_To");
                _MLS_Sky_Blend_From_Exposure = Shader.PropertyToID("_MLS_Sky_Blend_From_Exposure");
                _MLS_Sky_Blend_To_Exposure = Shader.PropertyToID("_MLS_Sky_Blend_To_Exposure");
                _MLS_Sky_Blend_From_Tint = Shader.PropertyToID("_MLS_Sky_Blend_From_Tint");
                _MLS_Sky_Blend_To_Tint = Shader.PropertyToID("_MLS_Sky_Blend_To_Tint");
                _MLS_SkyReflection_Blend_From = Shader.PropertyToID("_MLS_SkyReflection_Blend_From");
                _MLS_SkyReflection_Blend_To = Shader.PropertyToID("_MLS_SkyReflection_Blend_To");
                _MLS_LightCookie_Main_Blend_From = Shader.PropertyToID("_MLS_LightCookie_Main_Blend_From");
                _MLS_LightCookie_Main_Blend_To = Shader.PropertyToID("_MLS_LightCookie_Main_Blend_To");

                _MLS_Lightmaps_Count = Shader.PropertyToID("_MLS_Lightmaps_Count");
                _MLS_Reflection_Probe_Count = Shader.PropertyToID("_MLS_Reflection_Probe_Count");
                _MLS_Cubemap_Array = Shader.PropertyToID("_MLS_Cubemap_Array");
                _MLS_Reflection_Probe_Positions = Shader.PropertyToID("_MLS_Reflection_Probe_Positions");
                _MLS_Lightmap_Color_Array = new int[9];
                _MLS_Lightmap_Directional_Array = new int[9];
                _MLS_Lightmap_ShadowMask_Array = new int[9];
                _MLS_MainLight_Cookie_Array = new int[9];

                var resolution = 32;

                for (int i = 0; i < 9; i++)
                {
                    _MLS_Lightmap_Color_Array[i] = Shader.PropertyToID("_MLS_Lightmap_Color_Array_" + resolution);
                    _MLS_Lightmap_Directional_Array[i] = Shader.PropertyToID("_MLS_Lightmap_Directional_Array_" + resolution);
                    _MLS_Lightmap_ShadowMask_Array[i] = Shader.PropertyToID("_MLS_Lightmap_ShadowMask_Array_" + resolution);
                    _MLS_MainLight_Cookie_Array[i] = Shader.PropertyToID("_MLS_MainLight_Cookie_Array" + resolution);

                    resolution *= 2;
                }

#if BAKERY_INCLUDED
                _MLS_BakeryRNM0_Array = Shader.PropertyToID("_MLS_BakeryRNM_0_Array");
                _MLS_BakeryRNM1_Array = Shader.PropertyToID("_MLS_BakeryRNM_1_Array");
                _MLS_BakeryRNM2_Array = Shader.PropertyToID("_MLS_BakeryRNM_2_Array");

                _MLS_BakeryRNM0_From = Shader.PropertyToID("_MLS_BakeryRNM0_From");
                _MLS_BakeryRNM0_To = Shader.PropertyToID("_MLS_BakeryRNM0_To");
                _MLS_BakeryRNM1_From = Shader.PropertyToID("_MLS_BakeryRNM1_From");
                _MLS_BakeryRNM1_To = Shader.PropertyToID("_MLS_BakeryRNM1_To");
                _MLS_BakeryRNM2_From = Shader.PropertyToID("_MLS_BakeryRNM2_From");
                _MLS_BakeryRNM2_To = Shader.PropertyToID("_MLS_BakeryRNM2_To");

                _MLS_BakeryVolume0_From = Shader.PropertyToID("_MLS_BakeryVolume0_From");
                _MLS_BakeryVolume0_To = Shader.PropertyToID("_MLS_BakeryVolume0_To");
                _MLS_BakeryVolume1_From = Shader.PropertyToID("_MLS_BakeryVolume1_From");
                _MLS_BakeryVolume1_To = Shader.PropertyToID("_MLS_BakeryVolume1_To");
                _MLS_BakeryVolume2_From = Shader.PropertyToID("_MLS_BakeryVolume2_From");
                _MLS_BakeryVolume2_To = Shader.PropertyToID("_MLS_BakeryVolume2_To");
                _MLS_BakeryVolumeMask_From = Shader.PropertyToID("_MLS_BakeryVolumeMask_From");
                _MLS_BakeryVolumMask_To = Shader.PropertyToID("_MLS_BakeryVolumeMask_To");
                _MLS_BakeryVolumeCompressed_From = Shader.PropertyToID("_MLS_BakeryVolumeCompressed_From");
                _MLS_BakeryVolumeCompressed_To = Shader.PropertyToID("_MLS_BakeryVolumeCompressed_To");
#endif

#if MT_HDRP_7_INCLUDED || MT_HDRP_8_INCLUDED || MT_HDRP_9_INCLUDED || MT_HDRP_10_INCLUDED || MT_HDRP_11_INCLUDED || MT_HDRP_12_INCLUDED || MT_HDRP_13_INCLUDED || MT_HDRP_14_INCLUDED
                hdRenderPipelineAsset = (HDRenderPipelineAsset) GraphicsSettings.renderPipelineAsset;
#endif
                _propertyIdsInitialized = true;
            }
        }

        public static void UpdateBlendingOperationalData(string targetScene)
        {
#if UNITY_2020_1_OR_NEWER
            MagicLightmapSwitcher[] magicLightmapSwitchers = GameObject.FindObjectsOfType<MagicLightmapSwitcher>();
            int totalProbesOnScene = 0;

            for (int i = 0; i < magicLightmapSwitchers.Length; i++)
            {
                if (magicLightmapSwitchers[i].availableScenarios != null && magicLightmapSwitchers[i].availableScenarios.Count > 0 &&
                    magicLightmapSwitchers[i].availableScenarios[0].blendableLightmaps.Count > 0 &&
                    magicLightmapSwitchers[i].availableScenarios[0].blendableLightmaps[0].lightingData != null &&
                    magicLightmapSwitchers[i].availableScenarios[0].blendableLightmaps[0].lightingData.sceneLightingData != null)
                {
                    totalProbesOnScene += magicLightmapSwitchers[i].availableScenarios[0].blendableLightmaps[0].lightingData.sceneLightingData.initialLightProbesArrayPosition;
                    magicLightmapSwitchers[i].availableScenarios[0].lightProbesArrayPosition = totalProbesOnScene - magicLightmapSwitchers[i].availableScenarios[0].blendableLightmaps[0].lightingData.sceneLightingData.initialLightProbesArrayPosition;
                }
            }
#else
            if (!blendingOperationalDatas.ContainsKey(targetScene))
            {
                BlendingOperationalData blendingOperationalData = new BlendingOperationalData();

                blendingOperationalData.sceneName = targetScene;
                blendingOperationalData.loadIndex = blendingOperationalDatas.Count;
                blendingOperationalData.lightProbesArrayIndex = 0;

                blendingOperationalDatas.Add(targetScene, blendingOperationalData);
            }
#endif
        }

        public static void UpdateBlend()
        {
#if UNITY_EDITOR
#if BAKERY_INCLUDED
            if (Lightmapping.isRunning || ftRenderLightmap.bakeInProgress)
            {
                return;
            }
#else
            if (Lightmapping.isRunning)
            {
                return;
            }
#endif
#endif
            if (_currentSwitcherInstance == null)
            {
                _currentSwitcherInstance = RuntimeAPI.GetSwitcherInstanceStatic();
            }

            if (_currentSwitcherInstance.availableScenarios.Count > 0)
            {
                Blend(_currentSwitcherInstance, _currentBlendingValue, _currentScenario);
            }
        }

        public static void Blend(MagicLightmapSwitcher switcherInstance, float blendFactor, StoredLightingScenario storedLightmapScenario)
        {
            _currentSwitcherInstance = switcherInstance;
            _currentScenario = storedLightmapScenario;
            _currentBlendingValue = blendFactor;

            switcherInstance.blendingProcess = true;
            switcherInstance.currentLightmapScenario = storedLightmapScenario;
            switcherInstance.currentLightmapScenario.globalBlendFactor = blendFactor;

            if (!storedLightmapScenario.selfTestCompleted)
            {
                var selfTestResult = storedLightmapScenario.SelfTest();

                if (selfTestResult != "")
                {
                    Debug.LogErrorFormat($"<color=cyan>MLS:</color> {selfTestResult} \r\nScenario: {storedLightmapScenario.name}");
                    return;
                }
            }

            if (!switcherInstance.storedDataUpdated)
            {
                switcherInstance.UpdateStoredArray(SceneManager.GetActiveScene().name, true);
            }

            if (!storedLightmapScenario.selfTestSuccess)
            {
                return;
            }

            if (!switcherInstance.systemProperties.ignoreCameraError && Camera.main == null)
            {
                Debug.LogErrorFormat("<color=cyan>MLS:</color> You have not installed the main camera. Tag the main camera with \"MainCamera\".");
                return;
            }

            for (int i = 0; i < storedLightmapScenario.blendableLightmaps.Count; i++)
            {
                if (storedLightmapScenario.targetScene != storedLightmapScenario.blendableLightmaps[i].lightingData.dataPrefix)
                {
                    Debug.LogErrorFormat("<color=cyan>MLS:</color>The \"Blendable Lightmaps Queue\"" +
                        "contains invalid data. Make sure the queue contains the data stored for the current scene.");
                    return;
                }

                if (i < storedLightmapScenario.blendableLightmaps.Count - 2)
                {
                    if (blendFactor >= storedLightmapScenario.blendableLightmaps[i].startValue && blendFactor <= storedLightmapScenario.blendableLightmaps[i + 1].startValue)
                    {
                        storedLightmapScenario.lightingDataFromIndex =
                            storedLightmapScenario.blendableLightmaps[i].blendingIndex;
                        storedLightmapScenario.lightingDataToIndex =
                            storedLightmapScenario.blendableLightmaps[i + 1].blendingIndex;

                        storedLightmapScenario.localBlendFactor =
                            Mathf.Clamp((blendFactor - storedLightmapScenario.blendableLightmaps[storedLightmapScenario.lightingDataFromIndex].startValue) /
                            (storedLightmapScenario.blendableLightmaps[storedLightmapScenario.lightingDataToIndex].startValue - storedLightmapScenario.blendableLightmaps[storedLightmapScenario.lightingDataFromIndex].startValue), 0, 1);

                        break;
                    }
                }
                else
                {
                    if (blendFactor >= storedLightmapScenario.blendableLightmaps[i].startValue)
                    {
                        storedLightmapScenario.lightingDataFromIndex = storedLightmapScenario.blendableLightmaps[i].blendingIndex;
                        storedLightmapScenario.lightingDataToIndex = storedLightmapScenario.blendableLightmaps.Count - 1;

                        storedLightmapScenario.localBlendFactor =
                            Mathf.Clamp((blendFactor - storedLightmapScenario.blendableLightmaps[storedLightmapScenario.lightingDataFromIndex].startValue) /
                            (1 - storedLightmapScenario.blendableLightmaps[storedLightmapScenario.lightingDataFromIndex].startValue), 0, 1);

                        break;
                    }
                }
            }

            float reflectionsRangedBlend =
                    Mathf.Clamp((storedLightmapScenario.localBlendFactor - storedLightmapScenario.blendableLightmaps[storedLightmapScenario.lightingDataToIndex].reflectionsBlendingRange.x) /
                    (storedLightmapScenario.blendableLightmaps[storedLightmapScenario.lightingDataToIndex].reflectionsBlendingRange.y - storedLightmapScenario.blendableLightmaps[storedLightmapScenario.lightingDataToIndex].reflectionsBlendingRange.x), 0, 1);

            float lightmapsRangedBlend =
                    Mathf.Clamp((storedLightmapScenario.localBlendFactor - storedLightmapScenario.blendableLightmaps[storedLightmapScenario.lightingDataToIndex].lightmapBlendingRange.x) /
                    (storedLightmapScenario.blendableLightmaps[storedLightmapScenario.lightingDataToIndex].lightmapBlendingRange.y - storedLightmapScenario.blendableLightmaps[storedLightmapScenario.lightingDataToIndex].lightmapBlendingRange.x), 0, 1);

            storedLightmapScenario.reflectionsRangedBlendFactor = reflectionsRangedBlend;
            storedLightmapScenario.lightmapsRangedBlendFactor = lightmapsRangedBlend;

            BlendLightmapsData(switcherInstance, storedLightmapScenario);

            if ((storedLightmapScenario.blendingModules & (1 << 3)) > 0)
            {
                BlendLightProbesData(switcherInstance, storedLightmapScenario,
                        storedLightmapScenario.lightingDataFromIndex, storedLightmapScenario.lightingDataToIndex,
                        lightmapsRangedBlend);
            }

#if BAKERY_INCLUDED
            if ((storedLightmapScenario.blendingModules & (1 << 5)) > 0)
#else
            if ((storedLightmapScenario.blendingModules & (1 << 4)) > 0)
#endif
            {
                BlendLightSourcesData(storedLightmapScenario.localBlendFactor, blendFactor,
                    storedLightmapScenario.blendableLightmaps, storedLightmapScenario.lightingDataFromIndex,
                    storedLightmapScenario.lightingDataToIndex);
            }

#if BAKERY_INCLUDED
            if ((storedLightmapScenario.blendingModules & (1 << 6)) > 0)
#else
            if ((storedLightmapScenario.blendingModules & (1 << 5)) > 0)
#endif
            {
                BlendCustomData(storedLightmapScenario.localBlendFactor, blendFactor, reflectionsRangedBlend,
                    lightmapsRangedBlend, storedLightmapScenario, storedLightmapScenario.lightingDataFromIndex,
                    storedLightmapScenario.lightingDataToIndex);
            }

#if BAKERY_INCLUDED
            if ((storedLightmapScenario.blendingModules & (1 << 7)) > 0)
#else
            if ((storedLightmapScenario.blendingModules & (1 << 6)) > 0)
#endif
            {
                BlendGameObjectsData(storedLightmapScenario.localBlendFactor, blendFactor,
                    storedLightmapScenario.blendableLightmaps, storedLightmapScenario.lightingDataFromIndex,
                    storedLightmapScenario.lightingDataToIndex);
            }

#if BAKERY_INCLUDED
            if ((storedLightmapScenario.blendingModules & (1 << 8)) > 0)
#else
            if ((storedLightmapScenario.blendingModules & (1 << 7)) > 0)
#endif
            {
                BlendCommonLightingSettings(lightmapsRangedBlend, storedLightmapScenario.blendableLightmaps,
                    storedLightmapScenario.lightingDataFromIndex, storedLightmapScenario.lightingDataToIndex);
            }

            switcherInstance.lastLightmapScenario = storedLightmapScenario;

            if (switcherInstance.OnBlendingValueChanged.Count > 0)
            {
                switcherInstance.OnBlendingValueChanged[storedLightmapScenario.eventsListId]
                    .Invoke(storedLightmapScenario, blendFactor, reflectionsRangedBlend, lightmapsRangedBlend);
            }

            currentBlendingValue?.Invoke(blendFactor);

            switcherInstance.blendingProcess = false;
        }

        private static void SetReflectionsBlendingState(MagicLightmapSwitcher.AffectedObject targetObject, int val)
        {
            targetObject.SetShaderInt(_MLS_ENABLE_REFLECTIONS_BLENDING, val);
        }

        public static void BlendReflectionProbes(
            MagicLightmapSwitcher.AffectedObject targetObject,
            List<StoredLightingScenario.LightmapData> storedLightmapDatas,
            List<ReflectionProbeBlendInfo> closestReflectionProbes,
            int fromIndex,
            int toIndex)
        {
#if MT_HDRP_7_INCLUDED || MT_HDRP_8_INCLUDED || MT_HDRP_9_INCLUDED || MT_HDRP_10_INCLUDED || MT_HDRP_11_INCLUDED || MT_HDRP_12_INCLUDED || MT_HDRP_13_INCLUDED || MT_HDRP_14_INCLUDED
            if (hdRelectionProbeData == null)
            {
                hdRelectionProbeData = closestReflectionProbes[0].probe.gameObject
                    .GetComponent<HDAdditionalReflectionData>();
            }

            if (closestReflectionProbes[0].probe == null || hdRelectionProbeData.mode == ProbeSettings.Mode.Realtime)
#else
            if (closestReflectionProbes[0].probe == null || closestReflectionProbes[0].probe.mode == ReflectionProbeMode.Realtime)
#endif
            {
                return;
            }

            var firstProbe = closestReflectionProbes[0].probe.name;

            Cubemap blendFrom_0 =
                storedLightmapDatas[fromIndex].lightingData
                    .storedReflectionProbeDataDeserialized[firstProbe] as Cubemap;
            Cubemap blendTo_0 =
                storedLightmapDatas[toIndex].lightingData.storedReflectionProbeDataDeserialized[firstProbe] as Cubemap;

            if (blendFrom_0 == null || blendTo_0 == null)
            {
                SetReflectionsBlendingState(targetObject, 0);
            }
            else
            {
                SetReflectionsBlendingState(targetObject, 1);

                if (isDeferredMode)
                {
                    Shader.SetGlobalTexture(_MLS_Reflection_Blend_From_0, blendFrom_0);
                    Shader.SetGlobalTexture(_MLS_Reflection_Blend_To_0, blendTo_0);
                }
                else
                {
                    targetObject.SetShaderTexture(_MLS_Reflection_Blend_From_0, blendFrom_0);
                    targetObject.SetShaderTexture(_MLS_Reflection_Blend_To_0, blendTo_0);
                }

                if (closestReflectionProbes.Count > 1)
                {
                    if (closestReflectionProbes[0].probe == null ||
                        closestReflectionProbes[1].probe.mode == ReflectionProbeMode.Realtime)
                    {
                        return;
                    }

                    var secondProbe = closestReflectionProbes[1].probe.name;

                    Cubemap blendFrom_1 = storedLightmapDatas[fromIndex].lightingData
                        .storedReflectionProbeDataDeserialized[secondProbe] as Cubemap;
                    Cubemap blendTo_1 = storedLightmapDatas[toIndex].lightingData
                        .storedReflectionProbeDataDeserialized[secondProbe] as Cubemap;

                    if (blendFrom_0 == null || blendFrom_1 == null || blendTo_0 == null || blendTo_1 == null)
                    {
                        SetReflectionsBlendingState(targetObject, 0);
                    }
                    else
                    {
                        SetReflectionsBlendingState(targetObject, 1);

                        if (isDeferredMode)
                        {
                            Shader.SetGlobalTexture(_MLS_Reflection_Blend_From_1, blendFrom_1);
                            Shader.SetGlobalTexture(_MLS_Reflection_Blend_To_1, blendTo_1);
                        }
                        else
                        {
                            targetObject.SetShaderTexture(_MLS_Reflection_Blend_From_1, blendFrom_1);
                            targetObject.SetShaderTexture(_MLS_Reflection_Blend_To_1, blendTo_1);
                        }
                    }
                }
            }
        }

        private static void BlendSkyboxReflectionProbes(
            MagicLightmapSwitcher.AffectedObject targetObject,
            List<StoredLightingScenario.LightmapData> storedLightmapDatas,
            int fromIndex,
            int toIndex)

        {
            targetObject.SetShaderTexture(_MLS_SkyReflection_Blend_From, storedLightmapDatas[fromIndex].lightingData.sceneLightingData.skyboxReflectionTexture[0]);
            targetObject.SetShaderTexture(_MLS_SkyReflection_Blend_To, storedLightmapDatas[toIndex].lightingData.sceneLightingData.skyboxReflectionTexture[0]);
            targetObject.SetShaderInt(_MLS_REFLECTIONS_FLAG, 0);
        }

#if BAKERY_INCLUDED
        private static BakeryVolume[] sceneVolumes;

        private static BakeryVolume GetClosestBakeryVolume(MagicLightmapSwitcher.AffectedObject targetObject)
        {
            Dictionary<float, BakeryVolume> distances = new Dictionary<float, BakeryVolume>();

            //if (sceneVolumes == null)
            {
                sceneVolumes = UnityEngine.Object.FindObjectsOfType<BakeryVolume>();
            }

            for (int i = 0; i < sceneVolumes.Length; i++)
            {
                distances.Add(Vector3.Distance(targetObject.renderer.transform.position, sceneVolumes[i].transform.position), sceneVolumes[i]);
            }

            return distances.Count > 0 ? distances.Min().Value : null;
        }

        private static void ProcessBakeryVolumes(
            MagicLightmapSwitcher.AffectedObject targetObject,
            List<StoredLightingScenario.LightmapData> storedLightmapDatas,
            BakeryVolume closestVolume,
            int fromIndex,
            int toIndex)
        {
            List<Texture3D> blendFrom =
                storedLightmapDatas[fromIndex].lightingData.bakeryVolumeDataDeserialized[closestVolume.name] as List<Texture3D>;
            List<Texture3D> blendTo =
                storedLightmapDatas[toIndex].lightingData.bakeryVolumeDataDeserialized[closestVolume.name] as List<Texture3D>;

            if (blendFrom != null && blendTo != null)
            {
                targetObject.SetShaderTexture(_MLS_BakeryVolume0_From, blendFrom[0]);
                targetObject.SetShaderTexture(_MLS_BakeryVolume0_To, blendTo[0]);
                targetObject.SetShaderTexture(_MLS_BakeryVolume1_From, blendFrom[1]);
                targetObject.SetShaderTexture(_MLS_BakeryVolume1_To, blendTo[1]);
                targetObject.SetShaderTexture(_MLS_BakeryVolume2_From, blendFrom[2]);
                targetObject.SetShaderTexture(_MLS_BakeryVolume2_To, blendTo[2]);

                if (closestVolume.bakedMask != null)
                {
                    targetObject.SetShaderTexture(_MLS_BakeryVolumeMask_From, blendFrom[3]);
                    targetObject.SetShaderTexture(_MLS_BakeryVolumMask_To, blendTo[3]);
                }

                if (closestVolume.bakedTexture3 != null)
                {
                    targetObject.SetShaderTexture(_MLS_BakeryVolumeCompressed_From, blendFrom[4]);
                    targetObject.SetShaderTexture(_MLS_BakeryVolumeCompressed_To, blendTo[4]);
                }
            }
        }
#endif

        private static void ProcessReflectionProbes(
            ReflectionProbeUsage reflectionProbeUsage,
            MagicLightmapSwitcher.AffectedObject targetObject,
            List<StoredLightingScenario.LightmapData> storedLightmapDatas,
            int fromIndex,
            int toIndex)
        {
            if (targetObject.renderer != null)
            {
                targetObject.renderer.GetClosestReflectionProbes(targetObject.reflectionProbeBlendInfo);
            }
            else if (targetObject.terrain != null)
            {
                targetObject.terrain.GetClosestReflectionProbes(targetObject.reflectionProbeBlendInfo);
            }

            //BlendSkyboxReflectionProbes(
            //                targetObject,
            //                storedLightmapDatas,
            //                fromIndex,
            //                toIndex);

            switch (reflectionProbeUsage)
            {
                case ReflectionProbeUsage.Off:
                    BlendSkyboxReflectionProbes(
                        targetObject,
                        storedLightmapDatas,
                        fromIndex,
                        toIndex);

                    targetObject.SetShaderInt(_MLS_REFLECTIONS_FLAG, 0);
                    break;
                case ReflectionProbeUsage.BlendProbes:
                case ReflectionProbeUsage.Simple:
                    if (targetObject.reflectionProbeBlendInfo.Count > 0)
                    {
                        BlendReflectionProbes(
                            targetObject,
                            storedLightmapDatas,
                            targetObject.reflectionProbeBlendInfo,
                            fromIndex,
                            toIndex);

                        BlendSkyboxReflectionProbes(
                            targetObject,
                            storedLightmapDatas,
                            fromIndex,
                            toIndex);

                        targetObject.SetShaderInt(_MLS_REFLECTIONS_FLAG, 1);
                    }
                    else
                    {
                        BlendSkyboxReflectionProbes(
                            targetObject,
                            storedLightmapDatas,
                            fromIndex,
                            toIndex);

                        targetObject.SetShaderInt(_MLS_REFLECTIONS_FLAG, 0);
                    }
                    break;
                case ReflectionProbeUsage.BlendProbesAndSkybox:
                    if (targetObject.reflectionProbeBlendInfo.Count > 0)
                    {
                        BlendReflectionProbes(
                            targetObject,
                            storedLightmapDatas,
                            targetObject.reflectionProbeBlendInfo,
                            fromIndex,
                            toIndex);

                        BlendSkyboxReflectionProbes(
                            targetObject,
                            storedLightmapDatas,
                            fromIndex,
                            toIndex);

                        targetObject.SetShaderInt(_MLS_REFLECTIONS_FLAG, 2);
                    }
                    else
                    {
                        BlendSkyboxReflectionProbes(
                            targetObject,
                            storedLightmapDatas,
                            fromIndex,
                            toIndex);

                        targetObject.SetShaderInt(_MLS_REFLECTIONS_FLAG, 0);
                    }
                    break;
            }
        }

        private static void ProcessLightmapTexturesDebug(StoredLightingScenario storedLightingScenario)
        {
            var newLightmapData = new LightmapData[LightmapSettings.lightmaps.Length];
            var kernelIndex = _lightmapsProcessingComputeShader.FindKernel("CSMain");

            for (int i = 0; i < LightmapSettings.lightmaps.Length; i++)
            {
                newLightmapData[i] = new LightmapData();

                //Color
                _textureFrom[i] =
                    storedLightingScenario.blendableLightmaps[storedLightingScenario.lightingDataFromIndex].lightingData.sceneLightingData.lightmapsLight[i];
                _textureTo[i] =
                    storedLightingScenario.blendableLightmaps[storedLightingScenario.lightingDataToIndex].lightingData.sceneLightingData.lightmapsLight[i];

                if (_textureFrom[i] != null && _textureTo[i])
                {
                    _lightmapsProcessingComputeShader.SetFloat("_BlendFactor", storedLightingScenario.lightmapsRangedBlendFactor);
                    _lightmapsProcessingComputeShader.SetTexture(kernelIndex, "TextureFrom", _textureFrom[i]);
                    _lightmapsProcessingComputeShader.SetTexture(kernelIndex, "TextureTo", _textureTo[i]);
                    _lightmapsProcessingComputeShader.SetTexture(kernelIndex, "ResultTexture", _lightmapTextureOutRT[i]);

                    _lightmapsProcessingComputeShader.Dispatch(
                        kernelIndex,
                        Mathf.CeilToInt(_lightmapTextureOutRT[i].width / 8),
                        Mathf.CeilToInt(_lightmapTextureOutRT[i].width / 8),
                        1);

                    Graphics.CopyTexture(_lightmapTextureOutRT[i], _lightmapTextureColorOut[i]);

                    newLightmapData[i].lightmapColor = _lightmapTextureColorOut[i];
                }

                //Directional
                _textureFrom[i] =
                    storedLightingScenario.blendableLightmaps[storedLightingScenario.lightingDataFromIndex].lightingData.sceneLightingData.lightmapsDirectional[i];
                _textureTo[i] =
                    storedLightingScenario.blendableLightmaps[storedLightingScenario.lightingDataToIndex].lightingData.sceneLightingData.lightmapsDirectional[i];

                if (_textureFrom[i] != null && _textureTo[i])
                {
                    _lightmapsProcessingComputeShader.SetFloat("_BlendFactor", storedLightingScenario.lightmapsRangedBlendFactor);
                    _lightmapsProcessingComputeShader.SetTexture(kernelIndex, "TextureFrom", _textureFrom[i]);
                    _lightmapsProcessingComputeShader.SetTexture(kernelIndex, "TextureTo", _textureTo[i]);
                    _lightmapsProcessingComputeShader.SetTexture(kernelIndex, "ResultTexture", _lightmapTextureOutRT[i]);

                    _lightmapsProcessingComputeShader.Dispatch(
                        kernelIndex,
                        Mathf.CeilToInt(_lightmapTextureOutRT[i].width / 8),
                        Mathf.CeilToInt(_lightmapTextureOutRT[i].width / 8),
                        1);

                    Graphics.CopyTexture(_lightmapTextureOutRT[i], _lightmapTextureDirectionalOut[i]);

                    newLightmapData[i].lightmapDir = _lightmapTextureDirectionalOut[i];
                }

                //Shadowmask
                _textureFrom[i] =
                    storedLightingScenario.blendableLightmaps[storedLightingScenario.lightingDataFromIndex].lightingData.sceneLightingData.lightmapsShadowmask[i];
                _textureTo[i] =
                    storedLightingScenario.blendableLightmaps[storedLightingScenario.lightingDataToIndex].lightingData.sceneLightingData.lightmapsShadowmask[i];

                if (_textureFrom[i] != null && _textureTo[i])
                {
                    _lightmapsProcessingComputeShader.SetFloat("_BlendFactor", storedLightingScenario.lightmapsRangedBlendFactor);
                    _lightmapsProcessingComputeShader.SetTexture(kernelIndex, "TextureFrom", _textureFrom[i]);
                    _lightmapsProcessingComputeShader.SetTexture(kernelIndex, "TextureTo", _textureTo[i]);
                    _lightmapsProcessingComputeShader.SetTexture(kernelIndex, "ResultTexture", _lightmapTextureOutRT[i]);

                    _lightmapsProcessingComputeShader.Dispatch(
                        kernelIndex,
                        Mathf.CeilToInt(_lightmapTextureOutRT[i].width / 8),
                        Mathf.CeilToInt(_lightmapTextureOutRT[i].width / 8),
                        1);

                    Graphics.CopyTexture(_lightmapTextureOutRT[i], _lightmapTextureShadowmaskOut[i]);

                    newLightmapData[i].shadowMask = _lightmapTextureShadowmaskOut[i];
                }
            }

            LightmapSettings.lightmaps = newLightmapData;
        }

        private static void ProcessReflectionTextures(StoredLightingScenario storedLightingScenario)
        {
            //var reflectionProbesCount =
            //    storedLightingScenario.blendableLightmaps[storedLightingScenario.lightingDataFromIndex].lightingData.
            //    sceneLightingData.reflectionProbes.cubeReflectionTexture.Length;
            //int kernelIndex = _lightmapsProcessingComputeShader.FindKernel("CSMain");

            //ReflectionProbe[] reflectionProbes = Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);

            //for (int i = 0; i < reflectionProbes.Length; i++)
            //{
            //    _textureCubeFrom =
            //        storedLightingScenario.blendableLightmaps[storedLightingScenario.lightingDataFromIndex].lightingData.
            //        sceneLightingData.reflectionProbes.cubeReflectionTexture[i];
            //    _textureCubeTo =
            //        storedLightingScenario.blendableLightmaps[storedLightingScenario.lightingDataToIndex].lightingData.
            //        sceneLightingData.reflectionProbes.cubeReflectionTexture[i];

            //    _textureCubeOut = new Cubemap(_textureCubeFrom.width, GraphicsFormat.R32G32B32A32_SFloat, TextureCreationFlags.MipChain);
            //    _textureFrom = new Texture2D(_textureCubeOut.width, _textureCubeOut.width);
            //    _textureTo = new Texture2D(_textureCubeOut.width, _textureCubeOut.width);

            //    Graphics.CopyTexture(_textureCubeFrom, 0, _textureFrom, 0);
            //    Graphics.ConvertTexture(_textureCubeTo, _textureTo);

            //    _lightmapsProcessingComputeShader.SetFloat("_BlendFactor", storedLightingScenario.lightmapsRangedBlendFactor);
            //    _lightmapsProcessingComputeShader.SetTexture(kernelIndex, "TextureFrom", _textureFrom);
            //    _lightmapsProcessingComputeShader.SetTexture(kernelIndex, "TextureTo", _textureFrom);
            //    _lightmapsProcessingComputeShader.SetTexture(kernelIndex, "ResultTexture", _lightmapTextureOutRT);
            //    _lightmapsProcessingComputeShader.Dispatch(kernelIndex, Mathf.CeilToInt(_lightmapTextureOutRT.width / 8), Mathf.CeilToInt(_lightmapTextureOutRT.width / 8), 1);

            //    Graphics.CopyTexture(_lightmapTextureOutRT, _reflectionTextureOut);
            //    Graphics.ConvertTexture(_reflectionTextureOut, _textureCubeOut);

            //    reflectionProbes[i].bakedTexture = _textureCubeOut;
            //}
        }

        public static int GetSuitableTextureArray(Texture2DArray[] textureArrays, int resolution)
        {
            int resultIndex = -1;

            for (int i = 0; i < textureArrays.Length; i++)
            {
                if (textureArrays[i].width == resolution)
                {
                    resultIndex = i;
                    _currentResolution = resolution;
                    break;
                }
            }

            return resultIndex;
        }

        private static void BlendLightmapsData(
            MagicLightmapSwitcher switcherInstance,
            StoredLightingScenario storedLightingScenario
        )
        {
            InitiShaderProperties();

#if UNITY_EDITOR
            if (SceneView.lastActiveSceneView != null && 
                (SceneView.lastActiveSceneView.cameraMode.drawMode == DrawCameraMode.BakedLightmap ||
                SceneView.lastActiveSceneView.cameraMode.drawMode == DrawCameraMode.BakedDirectionality ||
                SceneView.lastActiveSceneView.cameraMode.drawMode == DrawCameraMode.ShadowMasks))
            {
                ProcessLightmapTexturesDebug(storedLightingScenario);
                return;
            }
#endif

#if MT_HDRP_7_INCLUDED || MT_HDRP_8_INCLUDED || MT_HDRP_9_INCLUDED || MT_HDRP_10_INCLUDED || MT_HDRP_11_INCLUDED || MT_HDRP_12_INCLUDED || MT_HDRP_13_INCLUDED || MT_HDRP_14_INCLUDED || MT_HDRP_15_INCLUDED || MT_HDRP_16_INCLUDED
            if (hdRenderPipelineAsset != null)
            {
                if ((hdRenderPipelineAsset.currentPlatformRenderPipelineSettings.supportedLitShaderMode ==
                     RenderPipelineSettings.SupportedLitShaderMode.Both ||
                     hdRenderPipelineAsset.currentPlatformRenderPipelineSettings.supportedLitShaderMode ==
                     RenderPipelineSettings.SupportedLitShaderMode.DeferredOnly)
                )
                {
                    isDeferredMode = true;
                }
            }
            else
            {
                Debug.LogWarningFormat("<color=cyan>MLS:</color>" + 
                                       "MLS is trying to work in HDRP mode because you installed this package, " +
                                       "but you did not assign an asset in the Graphics settings of your project." +
                                       "Assign asset, remove HDRP package or remove \"MT_HDRP_XX_INCLUDED\" directive from " +
                                       "Player settings of your project.");

                return;
            }
#endif

            #region Process Reflection Probes

            if (switcherInstance.useTextureArrays)
            {
                if ((storedLightingScenario.blendingModules & (1 << 1)) > 0)
                {
                    Shader.SetGlobalFloat(_MLS_Reflections_Blend_Factor, storedLightingScenario.reflectionsRangedBlendFactor);
                }

#if BAKERY_INCLUDED
                #region Process Bakery Volumes

                if ((storedLightingScenario.blendingModules & (1 << 4)) > 0)
                {
                    for (int i = 0; i < _resultDynamicAffectedObjects.Count; i++)
                    {
                        if (_resultDynamicAffectedObjects[i].mlsObject.isSkipped)
                        {
                            continue;
                        }

                        if (switcherInstance.lightmapper == MagicLightmapSwitcher.Lightmapper.BakeryLightmapper)
                        {
                            BakeryVolume closestVolume = GetClosestBakeryVolume(_resultDynamicAffectedObjects[i]);

                            if (closestVolume != null)
                            {
                                ProcessBakeryVolumes(
                                    _resultDynamicAffectedObjects[i],
                                    storedLightingScenario.blendableLightmaps,
                                    closestVolume,
                                    storedLightingScenario.lightingDataFromIndex,
                                    storedLightingScenario.lightingDataToIndex);
                            }
                        }
                    }
                }

                #endregion
#endif
            }
            else
            {
                switcherInstance.cubemapArrayInitialized = false;

                for (int i = 0; i < _resultDynamicAffectedObjects.Count; i++)
                {
                    if (_resultDynamicAffectedObjects[i].mlsObject.isSkipped)
                    {
                        continue;
                    }

                    if (_resultDynamicAffectedObjects[i].renderer != null)
                    {
                        _resultDynamicAffectedObjects[i].InitPropertyBlock();
                    }
                    else
                    {
                        _resultDynamicAffectedObjects.RemoveAt(i);
                        return;
                    }

                    if (storedLightingScenario.blendableLightmaps.Count < 3 ||
                        (_resultDynamicAffectedObjects[i].lastFromIndex !=
                         storedLightingScenario.lightingDataFromIndex ||
                         switcherInstance.lastLightmapScenario != switcherInstance.currentLightmapScenario))
                    {
                        if ((storedLightingScenario.blendingModules & (1 << 1)) > 0)
                        {
                            ProcessReflectionProbes(
                                _resultDynamicAffectedObjects[i].renderer.reflectionProbeUsage,
                                _resultDynamicAffectedObjects[i],
                                storedLightingScenario.blendableLightmaps,
                                storedLightingScenario.lightingDataFromIndex,
                                storedLightingScenario.lightingDataToIndex);
                        }
                        else
                        {
                            SetReflectionsBlendingState(_resultDynamicAffectedObjects[i], 0);
                        }

#if BAKERY_INCLUDED
                        #region Process Bakery Volumes

                        if ((storedLightingScenario.blendingModules & (1 << 4)) > 0)
                        {
                            if (switcherInstance.lightmapper == MagicLightmapSwitcher.Lightmapper.BakeryLightmapper)
                            {
                                BakeryVolume closestVolume =
                                    GetClosestBakeryVolume(_resultDynamicAffectedObjects[i]);

                                if (closestVolume != null)
                                {
                                    ProcessBakeryVolumes(
                                        _resultDynamicAffectedObjects[i],
                                        storedLightingScenario.blendableLightmaps,
                                        closestVolume,
                                        storedLightingScenario.lightingDataFromIndex,
                                        storedLightingScenario.lightingDataToIndex);
                                }
                            }
                        }

                        #endregion
#endif
                    }

                    if (!isDeferredMode)
                    {
                        if ((storedLightingScenario.blendingModules & (1 << 1)) > 0)
                        {
                            _resultDynamicAffectedObjects[i].SetShaderInt(_MLS_ENABLE_REFLECTIONS_BLENDING, 1);
                            _resultDynamicAffectedObjects[i].SetShaderFloat(_MLS_Reflections_Blend_Factor,
                                storedLightingScenario.reflectionsRangedBlendFactor);
                        }
                        else
                        {
                            Shader.SetGlobalFloat(_MLS_ENABLE_REFLECTIONS_BLENDING, 0);
                        }
                    }

                    //#if UNITY_EDITOR
                    _resultDynamicAffectedObjects[i].ApplyPropertyBlock();
                    //#endif

                    _resultDynamicAffectedObjects[i].lastFromIndex = storedLightingScenario.lightingDataFromIndex;
                }
            }

            #endregion

            #region Process Lightmaps

            if (switcherInstance.useTextureArrays)
            {
                if ((storedLightingScenario.blendingModules & (1 << 0)) > 0)
                {
                    Shader.SetGlobalFloat(_MLS_Lightmaps_Blend_Factor, storedLightingScenario.lightmapsRangedBlendFactor);

                    ProcessTerrainLightmaps(switcherInstance, storedLightingScenario);
                }
            }
            else
            {
                switcherInstance.lightmapArrayInitialized = false;

                ProcessTerrainLightmaps(switcherInstance, storedLightingScenario);

                for (int i = 0; i < _resultStaticAffectedObjects.Count; i++)
                {
                    if (_resultStaticAffectedObjects[i].renderer != null ||
                        _resultStaticAffectedObjects[i].terrain != null)
                    {
                        _resultStaticAffectedObjects[i].InitPropertyBlock();
                    }
                    else
                    {
                        _resultStaticAffectedObjects.RemoveAt(i);
                        return;
                    }

                    if (_resultStaticAffectedObjects[i].terrain == null)
                    {
                        if (_resultStaticAffectedObjects[i].mlsObject.isSkipped)
                        {
                            continue;
                        }

                        if (storedLightingScenario.blendableLightmaps.Count < 3 ||
                            _resultStaticAffectedObjects[i].lastFromIndex !=
                            storedLightingScenario.lightingDataFromIndex ||
                            switcherInstance.lastLightmapScenario != switcherInstance.currentLightmapScenario)
                        {
                            if ((storedLightingScenario.blendingModules & (1 << 1)) > 0)
                            {
                                ProcessReflectionProbes(
                                    _resultStaticAffectedObjects[i].renderer.reflectionProbeUsage,
                                    _resultStaticAffectedObjects[i],
                                    storedLightingScenario.blendableLightmaps,
                                    storedLightingScenario.lightingDataFromIndex,
                                    storedLightingScenario.lightingDataToIndex);
                            }
                            else
                            {
                                SetReflectionsBlendingState(_resultStaticAffectedObjects[i], 0);
                            }

                            if ((storedLightingScenario.blendingModules & (1 << 0)) > 0)
                            {
                                StoredLightmapData.RendererData rendererData =
                                    storedLightingScenario
                                            .blendableLightmaps[storedLightingScenario.lightingDataFromIndex]
                                            .lightingData
                                            .rendererDataDeserialized[_resultStaticAffectedObjects[i].objectId] as
                                        StoredLightmapData.RendererData;

                                if (rendererData == null)
                                {
                                    Debug.LogWarningFormat("<color=cyan>MLS:</color> " +
                                                           "The object \"" +
                                                           _resultStaticAffectedObjects[i].renderer.name + "\" " +
                                                           "is not present in the \"" +
                                                           storedLightingScenario
                                                               .blendableLightmaps[
                                                                   storedLightingScenario.lightingDataFromIndex]
                                                               .lightingData.name +
                                                           "\" lighting data, it is automatically isolated " +
                                                           "and will not participate in blending or switching lightmaps. \r\n" +
                                                           "Why did this happen? \r\n" +
                                                           "The object was active and marked as static during baking of the \"" +
                                                           storedLightingScenario
                                                               .blendableLightmaps[
                                                                   storedLightingScenario.lightingDataFromIndex]
                                                               .lightingData.name +
                                                           "\" preset, " +
                                                           "but was deactivated or marked as dynamic in the \"" +
                                                           storedLightingScenario
                                                               .blendableLightmaps[
                                                                   storedLightingScenario.lightingDataFromIndex]
                                                               .lightingData.name +
                                                           "\" preset. " +
                                                           "Object \"" +
                                                           _resultStaticAffectedObjects[i].renderer.name +
                                                           "\" might be getting deactivated by some other script.");

                                    _resultStaticAffectedObjects.RemoveAt(i);

                                    continue;
                                }

                                if (rendererData.lightmapIndex > -1)
                                {
                                    if (_resultStaticAffectedObjects[i].lastFromIndex !=
                                        storedLightingScenario.lightingDataFromIndex ||
                                        switcherInstance.lastLightmapScenario !=
                                        switcherInstance.currentLightmapScenario)
                                    {
                                        _resultStaticAffectedObjects[i].SetShaderTexture(
                                            _MLS_Lightmap_Color_Blend_From,
                                            storedLightingScenario
                                                .blendableLightmaps[storedLightingScenario.lightingDataFromIndex]
                                                .lightingData
                                                .sceneLightingData
                                                .lightmapsLight[rendererData.lightmapIndex]);

                                        _resultStaticAffectedObjects[i].SetShaderTexture(
                                            _MLS_Lightmap_Color_Blend_To,
                                            storedLightingScenario
                                                .blendableLightmaps[storedLightingScenario.lightingDataToIndex]
                                                .lightingData
                                                .sceneLightingData
                                                .lightmapsLight[rendererData.lightmapIndex]);

                                        if (storedLightingScenario
                                                .blendableLightmaps[storedLightingScenario.lightingDataFromIndex]
                                                .lightingData
                                                .sceneLightingData
                                                .lightmapsDirectional.Length > 0 &&
                                            storedLightingScenario
                                                .blendableLightmaps[storedLightingScenario.lightingDataFromIndex]
                                                .lightingData
                                                .sceneLightingData
                                                .lightmapsDirectional[rendererData.lightmapIndex] != null &&
                                            storedLightingScenario
                                                .blendableLightmaps[storedLightingScenario.lightingDataToIndex]
                                                .lightingData
                                                .sceneLightingData.lightmapsDirectional
                                                .Length > 0 &&
                                            storedLightingScenario
                                                .blendableLightmaps[storedLightingScenario.lightingDataToIndex]
                                                .lightingData
                                                .sceneLightingData
                                                .lightmapsDirectional[rendererData.lightmapIndex] != null)
                                        {
                                            _resultStaticAffectedObjects[i].SetShaderTexture(
                                                _MLS_Lightmap_Directional_Blend_From,
                                                storedLightingScenario
                                                    .blendableLightmaps[storedLightingScenario.lightingDataFromIndex]
                                                    .lightingData
                                                    .sceneLightingData
                                                    .lightmapsDirectional[rendererData.lightmapIndex]);

                                            _resultStaticAffectedObjects[i].SetShaderTexture(
                                                _MLS_Lightmap_Directional_Blend_To,
                                                storedLightingScenario
                                                    .blendableLightmaps[storedLightingScenario.lightingDataToIndex]
                                                    .lightingData
                                                    .sceneLightingData
                                                    .lightmapsDirectional[rendererData.lightmapIndex]);
                                        }

                                        if (storedLightingScenario
                                                .blendableLightmaps[storedLightingScenario.lightingDataFromIndex]
                                                .lightingData
                                                .sceneLightingData
                                                .lightmapsShadowmask.Length > 0 &&
                                            storedLightingScenario
                                                .blendableLightmaps[storedLightingScenario.lightingDataFromIndex]
                                                .lightingData
                                                .sceneLightingData
                                                .lightmapsShadowmask[rendererData.lightmapIndex] != null &&
                                            storedLightingScenario
                                                .blendableLightmaps[storedLightingScenario.lightingDataToIndex]
                                                .lightingData
                                                .sceneLightingData.lightmapsShadowmask
                                                .Length > 0 &&
                                            storedLightingScenario
                                                .blendableLightmaps[storedLightingScenario.lightingDataToIndex]
                                                .lightingData
                                                .sceneLightingData
                                                .lightmapsShadowmask[rendererData.lightmapIndex] != null)
                                        {
                                            _resultStaticAffectedObjects[i].SetShaderTexture(
                                                _MLS_Lightmap_ShadowMask_Blend_From,
                                                storedLightingScenario
                                                    .blendableLightmaps[storedLightingScenario.lightingDataFromIndex]
                                                    .lightingData
                                                    .sceneLightingData
                                                    .lightmapsShadowmask[rendererData.lightmapIndex]);

                                            _resultStaticAffectedObjects[i].SetShaderTexture(
                                                _MLS_Lightmap_ShadowMask_Blend_To,
                                                storedLightingScenario
                                                    .blendableLightmaps[storedLightingScenario.lightingDataToIndex]
                                                    .lightingData
                                                    .sceneLightingData
                                                    .lightmapsShadowmask[rendererData.lightmapIndex]);
                                        }

#if BAKERY_INCLUDED
                                        if (switcherInstance.lightmapper ==
                                            MagicLightmapSwitcher.Lightmapper.BakeryLightmapper)
                                        {
                                            if (storedLightingScenario
                                                .blendableLightmaps[storedLightingScenario.lightingDataFromIndex]
                                                .lightingData
                                                .sceneLightingData
                                                .lightmapsBakeryRNM0.Length > 0)
                                            {
                                                if (storedLightingScenario
                                                    .blendableLightmaps[storedLightingScenario.lightingDataFromIndex]
                                                    .lightingData
                                                    .sceneLightingData
                                                    .lightmapsBakeryRNM0[rendererData.lightmapIndex] != null)
                                                {
                                                    _resultStaticAffectedObjects[i].SetShaderTexture(
                                                        _MLS_BakeryRNM0_From,
                                                        storedLightingScenario
                                                            .blendableLightmaps[
                                                                storedLightingScenario.lightingDataFromIndex]
                                                            .lightingData.sceneLightingData
                                                            .lightmapsBakeryRNM0[rendererData.lightmapIndex]);
                                                    _resultStaticAffectedObjects[i].SetShaderTexture(
                                                        _MLS_BakeryRNM0_To,
                                                        storedLightingScenario
                                                            .blendableLightmaps[
                                                                storedLightingScenario.lightingDataToIndex]
                                                            .lightingData
                                                            .sceneLightingData
                                                            .lightmapsBakeryRNM0[rendererData.lightmapIndex]);
                                                }
                                            }

                                            if (storedLightingScenario
                                                .blendableLightmaps[storedLightingScenario.lightingDataFromIndex]
                                                .lightingData
                                                .sceneLightingData
                                                .lightmapsBakeryRNM1.Length > 0)
                                            {
                                                _resultStaticAffectedObjects[i].SetShaderTexture(
                                                    _MLS_BakeryRNM1_From,
                                                    storedLightingScenario
                                                        .blendableLightmaps[
                                                            storedLightingScenario.lightingDataFromIndex]
                                                        .lightingData
                                                        .sceneLightingData
                                                        .lightmapsBakeryRNM1[rendererData.lightmapIndex]);
                                                _resultStaticAffectedObjects[i].SetShaderTexture(
                                                    _MLS_BakeryRNM1_To,
                                                    storedLightingScenario
                                                        .blendableLightmaps[storedLightingScenario.lightingDataToIndex]
                                                        .lightingData
                                                        .sceneLightingData
                                                        .lightmapsBakeryRNM1[rendererData.lightmapIndex]);
                                            }

                                            if (storedLightingScenario
                                                .blendableLightmaps[storedLightingScenario.lightingDataFromIndex]
                                                .lightingData
                                                .sceneLightingData
                                                .lightmapsBakeryRNM2.Length > 0)
                                            {
                                                _resultStaticAffectedObjects[i].SetShaderTexture(
                                                    _MLS_BakeryRNM2_From,
                                                    storedLightingScenario
                                                        .blendableLightmaps[
                                                            storedLightingScenario.lightingDataFromIndex]
                                                        .lightingData
                                                        .sceneLightingData
                                                        .lightmapsBakeryRNM2[rendererData.lightmapIndex]);
                                                _resultStaticAffectedObjects[i].SetShaderTexture(
                                                    _MLS_BakeryRNM2_To,
                                                    storedLightingScenario
                                                        .blendableLightmaps[storedLightingScenario.lightingDataToIndex]
                                                        .lightingData
                                                        .sceneLightingData
                                                        .lightmapsBakeryRNM2[rendererData.lightmapIndex]);
                                            }
                                        }
#endif
                                        //LightSourceData lightFrom = 
                                        //    storedLightingScenario
                                        //    .blendableLightmaps[storedLightingScenario.lightingDataFromIndex]
                                        //    .lightingData.lightSourceDataDeserialized[_mainDirectionalLight.lightGUID] as LightSourceData;
                                        //LightSourceData lightTo =
                                        //    storedLightingScenario
                                        //    .blendableLightmaps[storedLightingScenario.lightingDataToIndex]
                                        //    .lightingData.lightSourceDataDeserialized[_mainDirectionalLight.lightGUID] as LightSourceData;

                                        //_resultStaticAffectedObjects[i].SetShaderTexture(
                                        //    _MLS_LightCookie_Main_Blend_From,
                                        //    lightFrom.lightCookie);

                                        //_resultStaticAffectedObjects[i].SetShaderTexture(
                                        //    _MLS_LightCookie_Main_Blend_To,
                                        //    lightTo.lightCookie);
                                    }
                                }
                            }
                        }
                    }


                    _resultStaticAffectedObjects[i].lastFromIndex = storedLightingScenario.lightingDataFromIndex;

                    if ((storedLightingScenario.blendingModules & (1 << 1)) > 0 || (storedLightingScenario.blendingModules & (1 << 4)) > 0)
                    {
                        _resultStaticAffectedObjects[i].SetShaderInt(_MLS_ENABLE_REFLECTIONS_BLENDING, 1);
                        _resultStaticAffectedObjects[i].SetShaderFloat(_MLS_Reflections_Blend_Factor,
                            storedLightingScenario.reflectionsRangedBlendFactor);
                    }
                    else
                    {
                        _resultStaticAffectedObjects[i].SetShaderInt(_MLS_ENABLE_REFLECTIONS_BLENDING, 0);
                    }

                    if ((storedLightingScenario.blendingModules & (1 << 0)) > 0)
                    {
                        _resultStaticAffectedObjects[i].SetShaderInt(_MLS_ENABLE_LIGHTMAPS_BLENDING, 1);
                        _resultStaticAffectedObjects[i].SetShaderFloat(_MLS_Lightmaps_Blend_Factor,
                            storedLightingScenario.lightmapsRangedBlendFactor);
                    }
                    else
                    {
                        _resultStaticAffectedObjects[i].SetShaderInt(_MLS_ENABLE_LIGHTMAPS_BLENDING, 0);
                    }

                    _resultStaticAffectedObjects[i].ApplyPropertyBlock();
                }

                if (isDeferredMode)
                {
                    if ((storedLightingScenario.blendingModules & (1 << 1)) > 0)
                    {
                        Shader.SetGlobalFloat(_MLS_ENABLE_REFLECTIONS_BLENDING, 1);
                        Shader.SetGlobalFloat(_MLS_Reflections_Blend_Factor, storedLightingScenario.reflectionsRangedBlendFactor);
                    }
                    else
                    {
                        Shader.SetGlobalFloat(_MLS_ENABLE_REFLECTIONS_BLENDING, 0);
                    }

                    if ((storedLightingScenario.blendingModules & (1 << 0)) > 0)
                    {
                        Shader.SetGlobalFloat(_MLS_ENABLE_LIGHTMAPS_BLENDING, 1);
                        Shader.SetGlobalFloat(_MLS_Lightmaps_Blend_Factor, storedLightingScenario.lightmapsRangedBlendFactor);
                    }
                    else
                    {
                        Shader.SetGlobalFloat(_MLS_ENABLE_LIGHTMAPS_BLENDING, 0);
                    }
                }
            }

            #endregion

            #region Process Skybox

            if ((storedLightingScenario.blendingModules & (1 << 2)) > 0)
            {
                var propsFrom = storedLightingScenario.blendableLightmaps[storedLightingScenario.lightingDataFromIndex]
                    .lightingData.sceneLightingData.skyboxSettings.skyboxShaderProperties;
                var propsTo = storedLightingScenario.blendableLightmaps[storedLightingScenario.lightingDataToIndex]
                    .lightingData.sceneLightingData.skyboxSettings.skyboxShaderProperties;

                for (int i = 0; i < propsFrom.Count; i++)
                {
                    switch (propsFrom[i].type)
                    {
                        case ShaderPropertyType.Float:
                        case ShaderPropertyType.Range:
                            RenderSettings.skybox.SetFloat(
                                propsFrom[i].name, Mathf.Lerp(propsFrom[i].floatValue, propsTo[i].floatValue, storedLightingScenario.reflectionsRangedBlendFactor));
                            break;
                        case ShaderPropertyType.Color:
                            RenderSettings.skybox.SetColor(
                                propsFrom[i].name, Color.Lerp(propsFrom[i].colorValue, propsTo[i].colorValue, storedLightingScenario.reflectionsRangedBlendFactor));
                            break;
                        case ShaderPropertyType.Texture:
                            Shader.SetGlobalTexture(_MLS_Sky_Cubemap_Blend_From, propsFrom[i].textureValue);
                            Shader.SetGlobalTexture(_MLS_Sky_Cubemap_Blend_To, propsTo[i].textureValue);
                            Shader.SetGlobalFloat(_MLS_Sky_Cubemap_Blend_Factor, storedLightingScenario.reflectionsRangedBlendFactor);
                            break;
                    }
                }

                RenderSettings.sun = storedLightingScenario.blendableLightmaps[storedLightingScenario.lightingDataToIndex]
                    .lightingData.sceneLightingData.environmentSettings.sunSource;
            }

            #endregion

            Shader.SetGlobalVector(_MLS_CURRENT_LIGHTMAP_PAIR,
                new Vector4(
                    storedLightingScenario.lightingDataFromIndex,
                    storedLightingScenario.lightingDataToIndex,
                    _lightmapCount,
                    _currentResolution));

            Shader.SetGlobalInt(_MLS_ENABLE_LIGHTMAPS_BLENDING,
                (storedLightingScenario.blendingModules & (1 << 0)) > 0 ? 1 : 0);
            Shader.SetGlobalInt(_MLS_ENABLE_REFLECTIONS_BLENDING,
                (storedLightingScenario.blendingModules & (1 << 1)) > 0 ? 1 : 0);
            Shader.SetGlobalInt(_MLS_ENABLE_SKY_CUBEMAPS_BLENDING,
                (storedLightingScenario.blendingModules & (1 << 2)) > 0 ? 1 : 0);
        }

        private static void ProcessTerrainLightmaps(MagicLightmapSwitcher switcherInstance, StoredLightingScenario storedLightingScenario)
        {
            for (int i = 0; i < _affectedTerrains.Count; i++)
            {
                _affectedTerrains[i].InitPropertyBlock();

                if (_affectedTerrains[i].terrain.isActiveAndEnabled)
                {
                    if (!isDeferredMode)
                    {
                        if (_affectedTerrains[i].lastFromIndex !=
                            storedLightingScenario.lightingDataFromIndex ||
                            switcherInstance.lastLightmapScenario !=
                            switcherInstance.currentLightmapScenario)
                        {
                            if ((storedLightingScenario.blendingModules & (1 << 1)) > 0)
                            {
                                ProcessReflectionProbes(
                                    _affectedTerrains[i].terrain.reflectionProbeUsage,
                                    _affectedTerrains[i],
                                    storedLightingScenario.blendableLightmaps,
                                    storedLightingScenario.lightingDataFromIndex,
                                    storedLightingScenario.lightingDataToIndex);
                            }

                            if ((storedLightingScenario.blendingModules & (1 << 0)) > 0)
                            {
                                StoredLightmapData.TerrainData terrainData =
                                            storedLightingScenario
                                                    .blendableLightmaps[storedLightingScenario.lightingDataFromIndex]
                                                    .lightingData
                                                    .terrainDataDeserialized[_affectedTerrains[i].objectId] as
                                                StoredLightmapData.TerrainData;

                                if (terrainData == null)
                                {
                                    _affectedTerrains.RemoveAt(i);
                                    Debug.LogWarningFormat("<color=cyan>MLS:</color> " +
                                                           "The object \"" +
                                                           _affectedTerrains[i].renderer.name +
                                                           "\" " +
                                                           "is not present in the \"" +
                                                           storedLightingScenario
                                                               .blendableLightmaps[
                                                                   storedLightingScenario.lightingDataFromIndex]
                                                               .lightingData.name +
                                                           "\" lighting data, it is automatically isolated " +
                                                           "and will not participate in blending or switching lightmaps. \r\n" +
                                                           "Why did this happen? \r\n" +
                                                           "The object was active and marked as static during baking of the \"" +
                                                           storedLightingScenario
                                                               .blendableLightmaps[
                                                                   storedLightingScenario.lightingDataFromIndex]
                                                               .lightingData.name +
                                                           "\" preset, " +
                                                           "but was deactivated or marked as dynamic in the \"" +
                                                           storedLightingScenario
                                                               .blendableLightmaps[
                                                                   storedLightingScenario.lightingDataFromIndex]
                                                               .lightingData.name +
                                                           "\" preset. " +
                                                           "Object \"" +
                                                           _affectedTerrains[i].renderer.name +
                                                           "\" might be getting deactivated by some other script.");
                                    return;
                                }

                                if (terrainData.lightmapIndex > -1)
                                {
                                    if (_affectedTerrains[i].lastFromIndex !=
                                        storedLightingScenario.lightingDataFromIndex ||
                                        switcherInstance.lastLightmapScenario !=
                                        switcherInstance.currentLightmapScenario)
                                    {
                                        _affectedTerrains[i].SetShaderTexture(
                                            _MLS_Lightmap_Color_Blend_From,
                                            storedLightingScenario
                                                .blendableLightmaps[
                                                    storedLightingScenario.lightingDataFromIndex]
                                                .lightingData
                                                .sceneLightingData
                                                .lightmapsLight[terrainData.lightmapIndex]);
                                        _affectedTerrains[i].SetShaderTexture(
                                            _MLS_Lightmap_Color_Blend_To,
                                            storedLightingScenario
                                                .blendableLightmaps[storedLightingScenario.lightingDataToIndex]
                                                .lightingData
                                                .sceneLightingData
                                                .lightmapsLight[terrainData.lightmapIndex]);

                                        if (storedLightingScenario
                                                .blendableLightmaps[
                                                    storedLightingScenario.lightingDataFromIndex]
                                                .lightingData
                                                .sceneLightingData
                                                .lightmapsDirectional.Length > 0 &&
                                            storedLightingScenario
                                                .blendableLightmaps[
                                                    storedLightingScenario.lightingDataFromIndex]
                                                .lightingData
                                                .sceneLightingData
                                                .lightmapsDirectional[terrainData.lightmapIndex] != null &&
                                            storedLightingScenario
                                                .blendableLightmaps[storedLightingScenario.lightingDataToIndex]
                                                .lightingData
                                                .sceneLightingData
                                                .lightmapsDirectional
                                                .Length > 0 &&
                                            storedLightingScenario
                                                .blendableLightmaps[storedLightingScenario.lightingDataToIndex]
                                                .lightingData
                                                .sceneLightingData
                                                .lightmapsDirectional[terrainData.lightmapIndex] != null)
                                        {
                                            _affectedTerrains[i].SetShaderTexture(
                                                _MLS_Lightmap_Directional_Blend_From,
                                                storedLightingScenario
                                                    .blendableLightmaps[
                                                        storedLightingScenario.lightingDataFromIndex]
                                                    .lightingData
                                                    .sceneLightingData
                                                    .lightmapsDirectional[terrainData.lightmapIndex]);
                                            _affectedTerrains[i].SetShaderTexture(
                                                _MLS_Lightmap_Directional_Blend_To,
                                                storedLightingScenario
                                                    .blendableLightmaps[
                                                        storedLightingScenario.lightingDataToIndex]
                                                    .lightingData
                                                    .sceneLightingData
                                                    .lightmapsDirectional[terrainData.lightmapIndex]);
                                        }

                                        if (storedLightingScenario
                                                .blendableLightmaps[
                                                    storedLightingScenario.lightingDataFromIndex]
                                                .lightingData
                                                .sceneLightingData
                                                .lightmapsShadowmask.Length > 0 &&
                                            storedLightingScenario
                                                .blendableLightmaps[
                                                    storedLightingScenario.lightingDataFromIndex]
                                                .lightingData
                                                .sceneLightingData
                                                .lightmapsShadowmask[terrainData.lightmapIndex] != null &&
                                            storedLightingScenario
                                                .blendableLightmaps[storedLightingScenario.lightingDataToIndex]
                                                .lightingData
                                                .sceneLightingData
                                                .lightmapsShadowmask
                                                .Length > 0 &&
                                            storedLightingScenario
                                                .blendableLightmaps[storedLightingScenario.lightingDataToIndex]
                                                .lightingData
                                                .sceneLightingData
                                                .lightmapsShadowmask[terrainData.lightmapIndex] != null)
                                        {
                                            _affectedTerrains[i].SetShaderTexture(
                                                _MLS_Lightmap_ShadowMask_Blend_From,
                                                storedLightingScenario
                                                    .blendableLightmaps[
                                                        storedLightingScenario.lightingDataFromIndex]
                                                    .lightingData
                                                    .sceneLightingData
                                                    .lightmapsShadowmask[terrainData.lightmapIndex]);
                                            _affectedTerrains[i].SetShaderTexture(
                                                _MLS_Lightmap_ShadowMask_Blend_To,
                                                storedLightingScenario
                                                    .blendableLightmaps[
                                                        storedLightingScenario.lightingDataToIndex]
                                                    .lightingData
                                                    .sceneLightingData
                                                    .lightmapsShadowmask[terrainData.lightmapIndex]);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                _affectedTerrains[i].lastFromIndex = storedLightingScenario.lightingDataFromIndex;

                if ((storedLightingScenario.blendingModules & (1 << 1)) > 0 || (storedLightingScenario.blendingModules & (1 << 4)) > 0)
                {
                    _affectedTerrains[i].SetShaderInt(_MLS_ENABLE_REFLECTIONS_BLENDING, 1);
                    _affectedTerrains[i].SetShaderFloat(_MLS_Reflections_Blend_Factor,
                        storedLightingScenario.reflectionsRangedBlendFactor);
                }
                else
                {
                    _affectedTerrains[i].SetShaderInt(_MLS_ENABLE_REFLECTIONS_BLENDING, 0);
                }

                if ((storedLightingScenario.blendingModules & (1 << 0)) > 0)
                {
                    _affectedTerrains[i].SetShaderInt(_MLS_ENABLE_LIGHTMAPS_BLENDING, 1);
                    _affectedTerrains[i].SetShaderFloat(_MLS_Lightmaps_Blend_Factor,
                        storedLightingScenario.lightmapsRangedBlendFactor);
                }
                else
                {
                    _affectedTerrains[i].SetShaderInt(_MLS_ENABLE_LIGHTMAPS_BLENDING, 0);
                }

                _affectedTerrains[i].ApplyPropertyBlock();
            }
        }

        private static void BlendCustomData(float localBlendFactor, float globalBlendFactor, float reflectionsBlendFactor, float lightmapsBlendFactor, StoredLightingScenario storedLightmapScenario, int fromIndex, int toIndex)
        {
            if (storedLightmapScenario.collectedCustomBlendableDatas.Count > 0)
            {
                if (storedLightmapScenario.collectedCustomBlendableDatas.Find(item => item.sourceScript == null) != null)
                {
                    storedLightmapScenario.SynchronizeCustomBlendableData();
                }
                else
                {
                    for (int i = 0; i < storedLightmapScenario.collectedCustomBlendableDatas.Count; i++)
                    {
                        if (storedLightmapScenario.collectedCustomBlendableDatas[i].blendableFloatFieldsDatas.Count > 0)
                        {
                            if (storedLightmapScenario.collectedCustomBlendableDatas[i].blendableFloatFieldsDatas.Find(item => item.sourceField == null) != null)
                            {
                                storedLightmapScenario.SynchronizeCustomBlendableData();
                            }
                        }

                        if (storedLightmapScenario.collectedCustomBlendableDatas[i].blendableColorFieldsDatas.Count > 0)
                        {
                            if (storedLightmapScenario.collectedCustomBlendableDatas[i].blendableColorFieldsDatas.Find(item => item.sourceField == null) != null)
                            {
                                storedLightmapScenario.SynchronizeCustomBlendableData();
                            }
                        }

                        if (storedLightmapScenario.collectedCustomBlendableDatas[i].blendableCubemapFieldsDatas.Count > 0)
                        {
                            if (storedLightmapScenario.collectedCustomBlendableDatas[i].blendableCubemapFieldsDatas.Find(item => item.sourceField == null) != null)
                            {
                                storedLightmapScenario.SynchronizeCustomBlendableData();
                            }
                        }
                    }
                }

                storedLightmapScenario.UpdateCustomBlendableData(localBlendFactor, globalBlendFactor, reflectionsBlendFactor, lightmapsBlendFactor, fromIndex, toIndex, 0);
            }
        }

        private static void BlendLightSourcesData(float localBlendFactor, float blendFactor, List<StoredLightingScenario.LightmapData> storedLightmapDatas, int fromIndex, int toIndex)
        {
            for (int i = 0; i < _resultAffectedLights.Count; i++)
            {
                if (!_resultAffectedLights[i].enabled)
                {
                    continue;
                }

                LightSourceData lightFrom =
                    storedLightmapDatas[fromIndex].lightingData.lightSourceDataDeserialized[_resultAffectedLights[i].lightGUID] as LightSourceData;
                LightSourceData lightTo =
                    storedLightmapDatas[toIndex].lightingData.lightSourceDataDeserialized[_resultAffectedLights[i].lightGUID] as LightSourceData;

                if (lightFrom == null || lightTo == null)
                {
                    continue;
                }

                _resultAffectedLights[i].sourceLight.transform.position = Vector3.Lerp(
                    lightFrom.position,
                    lightTo.position,
                    localBlendFactor);
                _resultAffectedLights[i].sourceLight.transform.rotation = Quaternion.Lerp(
                    lightFrom.rotation,
                    lightTo.rotation,
                    localBlendFactor);
                _resultAffectedLights[i].sourceLight.intensity = Mathf.Lerp(
                    lightFrom.intensity,
                    lightTo.intensity,
                    localBlendFactor);
                _resultAffectedLights[i].sourceLight.color = Color.Lerp(
                    lightFrom.color,
                    lightTo.color,
                    localBlendFactor);
                _resultAffectedLights[i].sourceLight.colorTemperature = Mathf.Lerp(
                    lightFrom.temperature,
                    lightTo.temperature,
                    localBlendFactor);
                _resultAffectedLights[i].sourceLight.range = Mathf.Lerp(
                    lightFrom.range,
                    lightTo.range,
                    localBlendFactor);
                _resultAffectedLights[i].sourceLight.spotAngle = Mathf.Lerp(
                    lightFrom.spotAngle,
                    lightTo.spotAngle,
                    localBlendFactor);
                _resultAffectedLights[i].sourceLight.shadows =
                    localBlendFactor > _resultAffectedLights[i].shadowTypeSwitchValue ? (LightShadows) lightTo.shadowType : (LightShadows) lightFrom.shadowType;
                _resultAffectedLights[i].sourceLight.shadowStrength = Mathf.Lerp(
                    lightFrom.shadowStrength,
                    lightTo.shadowStrength,
                    localBlendFactor);

                Shader.SetGlobalTexture(_MLS_LightCookie_Main_Blend_From, lightFrom.lightCookie);
                Shader.SetGlobalTexture(_MLS_LightCookie_Main_Blend_To, lightTo.lightCookie);
            }
        }

        private static void BlendGameObjectsData(float localBlendFactor, float blendFactor, List<StoredLightingScenario.LightmapData> storedLightmapDatas, int fromIndex, int toIndex)
        {
            for (int i = 0; i < _resultStaticAffectedObjects.Count; i++)
            {
                if (_resultStaticAffectedObjects[i] == null)
                {
                    continue;
                }

                if (_resultStaticAffectedObjects[i].terrain != null)
                {
                    return;
                }

                if (_resultStaticAffectedObjects[i].mlsObject.isSkipped)
                {
                    continue;
                }

                StoredLightmapData.RendererData rendererDataFrom =
                    storedLightmapDatas[fromIndex].lightingData.rendererDataDeserialized[_resultStaticAffectedObjects[i].objectId] as StoredLightmapData.RendererData;
                StoredLightmapData.RendererData rendererDataTo =
                    storedLightmapDatas[toIndex].lightingData.rendererDataDeserialized[_resultStaticAffectedObjects[i].objectId] as StoredLightmapData.RendererData;

                if (rendererDataFrom == null || rendererDataTo == null)
                {
                    Debug.LogWarningFormat("<color=cyan>MLS:</color> " +
                        "The object \"" + _resultStaticAffectedObjects[i].renderer.name + "\" " +
                        "is not present in the \"" + storedLightmapDatas[fromIndex].lightingData.name + "\" lighting data, it is automatically isolated " +
                        "and will not participate in blending or switching lightmaps. \r\n" +
                        "Why did this happen? \r\n" +
                        "The object was active and marked as static during baking of the \"" + storedLightmapDatas[fromIndex].lightingData.name + "\" preset, " +
                        "but was deactivated or marked as dynamic in the \"" + storedLightmapDatas[fromIndex].lightingData.name + "\" preset. " +
                        "Object \"" + _resultStaticAffectedObjects[i].renderer.name + "\" might be getting deactivated by some other script.");
                    _resultStaticAffectedObjects.RemoveAt(i);
                    return;
                }

                if (rendererDataTo.rendererShaderProperties.Count > 0)
                {
                    for (int p = 0; p < rendererDataTo.rendererShaderProperties.Count; p++)
                    {
                        switch (rendererDataTo.rendererShaderProperties[p].type)
                        {
                            case ShaderPropertyType.Float:
                            case ShaderPropertyType.Range:
                                _resultStaticAffectedObjects[i].SetShaderFloat(
                                    rendererDataTo.rendererShaderProperties[p].name,
                                    Mathf.Lerp(
                                        rendererDataFrom.rendererShaderProperties[p].floatValue,
                                        rendererDataTo.rendererShaderProperties[p].floatValue,
                                        localBlendFactor));
                                break;
                            case ShaderPropertyType.Color:
                                _resultStaticAffectedObjects[i].SetShaderColor(
                                    rendererDataTo.rendererShaderProperties[p].name,
                                    Color.Lerp(
                                        rendererDataFrom.rendererShaderProperties[p].colorValue,
                                        rendererDataTo.rendererShaderProperties[p].colorValue,
                                        localBlendFactor)
                                    );
                                break;
                            case ShaderPropertyType.Texture:
                                if (localBlendFactor > 0.5f)
                                {
                                    _resultStaticAffectedObjects[i].SetShaderTexture(
                                    rendererDataTo.rendererShaderProperties[p].name,
                                    rendererDataTo.rendererShaderProperties[p].textureValue);
                                }
                                else
                                {
                                    _resultStaticAffectedObjects[i].SetShaderTexture(
                                    rendererDataFrom.rendererShaderProperties[p].name,
                                    rendererDataFrom.rendererShaderProperties[p].textureValue);
                                }
                                break;
                        }
                    }

                    _resultStaticAffectedObjects[i].ApplyPropertyBlock();
                }

                if (rendererDataFrom.position != rendererDataTo.position)
                {
                    _resultStaticAffectedObjects[i].renderer.gameObject.transform.position = Vector3.Lerp(
                        rendererDataFrom.position,
                        rendererDataTo.position,
                        localBlendFactor);
                }

                if (rendererDataFrom.rotation != rendererDataTo.rotation)
                {
                    _resultStaticAffectedObjects[i].renderer.gameObject.transform.rotation = Quaternion.Lerp(
                        rendererDataFrom.rotation,
                        rendererDataTo.rotation,
                        localBlendFactor);
                }
            }

            for (int i = 0; i < _resultDynamicAffectedObjects.Count; i++)
            {
                if (_resultDynamicAffectedObjects[i] == null)
                {
                    continue;
                }

                if (_resultDynamicAffectedObjects[i].terrain != null)
                {
                    return;
                }

                if (!_resultDynamicAffectedObjects[i].renderer.isVisible)
                {
                    continue;
                }

                StoredLightmapData.RendererData rendererDataFrom =
                    storedLightmapDatas[fromIndex].lightingData.rendererDataDeserialized[_resultDynamicAffectedObjects[i].objectId] as StoredLightmapData.RendererData;
                StoredLightmapData.RendererData rendererDataTo =
                    storedLightmapDatas[toIndex].lightingData.rendererDataDeserialized[_resultDynamicAffectedObjects[i].objectId] as StoredLightmapData.RendererData;

                if (rendererDataFrom == null || rendererDataTo == null)
                {
                    Debug.LogWarningFormat("<color=cyan>MLS:</color> " +
                        "The object \"" + _resultDynamicAffectedObjects[i].renderer.name + "\" " +
                        "is not present in the \"" + storedLightmapDatas[fromIndex].lightingData.name + "\" lighting data, it is automatically isolated " +
                        "and will not participate in blending or switching lightmaps. \r\n" +
                        "Why did this happen? \r\n" +
                        "The object was active and marked as static during baking of the \"" + storedLightmapDatas[fromIndex].lightingData.name + "\" preset, " +
                        "but was deactivated or marked as dynamic in the \"" + storedLightmapDatas[fromIndex].lightingData.name + "\" preset. " +
                        "Object \"" + _resultDynamicAffectedObjects[i].renderer.name + "\" might be getting deactivated by some other script.");
                    _resultDynamicAffectedObjects.RemoveAt(i);
                    return;
                }

                if (rendererDataFrom.rendererShaderProperties.Count > 0)
                {
                    for (int p = 0; p < rendererDataFrom.rendererShaderProperties.Count; p++)
                    {
                        switch (rendererDataFrom.rendererShaderProperties[p].type)
                        {
                            case ShaderPropertyType.Float:
                            case ShaderPropertyType.Range:
                                _resultDynamicAffectedObjects[i].SetShaderFloat(
                                    rendererDataTo.rendererShaderProperties[p].name,
                                    Mathf.Lerp(
                                        rendererDataFrom.rendererShaderProperties[p].floatValue,
                                        rendererDataTo.rendererShaderProperties[p].floatValue,
                                        localBlendFactor));
                                break;
                            case ShaderPropertyType.Color:
                                _resultDynamicAffectedObjects[i].SetShaderColor(
                                    rendererDataTo.rendererShaderProperties[p].name,
                                    Color.Lerp(
                                        rendererDataFrom.rendererShaderProperties[p].colorValue,
                                        rendererDataTo.rendererShaderProperties[p].colorValue,
                                        localBlendFactor)
                                    );
                                break;
                            case ShaderPropertyType.Texture:
                                if (localBlendFactor > 0.5f)
                                {
                                    _resultDynamicAffectedObjects[i].SetShaderTexture(
                                    rendererDataTo.rendererShaderProperties[p].name,
                                    rendererDataTo.rendererShaderProperties[p].textureValue);
                                }
                                else
                                {
                                    _resultDynamicAffectedObjects[i].SetShaderTexture(
                                    rendererDataFrom.rendererShaderProperties[p].name,
                                    rendererDataFrom.rendererShaderProperties[p].textureValue);
                                }
                                break;
                        }
                    }

                    _resultDynamicAffectedObjects[i].ApplyPropertyBlock();
                }

                if (rendererDataFrom.position != rendererDataTo.position)
                {
                    _resultDynamicAffectedObjects[i].renderer.gameObject.transform.position = Vector3.Lerp(
                        rendererDataFrom.position,
                        rendererDataTo.position,
                        localBlendFactor);
                }

                if (rendererDataFrom.rotation != rendererDataTo.rotation)
                {
                    _resultDynamicAffectedObjects[i].renderer.gameObject.transform.rotation = Quaternion.Lerp(
                        rendererDataFrom.rotation,
                        rendererDataTo.rotation,
                        localBlendFactor);
                }
            }
        }

        private static void BlendCommonLightingSettings(float blendFactor, List<StoredLightingScenario.LightmapData> storedLightmapDatas, int fromIndex, int toIndex)
        {
            RenderSettings.fogColor = Color.Lerp(
                storedLightmapDatas[fromIndex].lightingData.sceneLightingData.fogSettings.fogColor,
                storedLightmapDatas[toIndex].lightingData.sceneLightingData.fogSettings.fogColor,
                blendFactor);
            RenderSettings.fogDensity = Mathf.Lerp(
                storedLightmapDatas[fromIndex].lightingData.sceneLightingData.fogSettings.fogDensity,
                storedLightmapDatas[toIndex].lightingData.sceneLightingData.fogSettings.fogDensity,
                blendFactor);
            RenderSettings.ambientMode = storedLightmapDatas[fromIndex].lightingData.sceneLightingData.environmentSettings.source;
            RenderSettings.ambientIntensity = Mathf.Lerp(
                storedLightmapDatas[fromIndex].lightingData.sceneLightingData.environmentSettings.intensityMultiplier,
                storedLightmapDatas[toIndex].lightingData.sceneLightingData.environmentSettings.intensityMultiplier,
                blendFactor);
            RenderSettings.ambientLight = Color.Lerp(
                storedLightmapDatas[fromIndex].lightingData.sceneLightingData.environmentSettings.ambientColor,
                storedLightmapDatas[toIndex].lightingData.sceneLightingData.environmentSettings.ambientColor,
                blendFactor);
            RenderSettings.ambientSkyColor = Color.Lerp(
                storedLightmapDatas[fromIndex].lightingData.sceneLightingData.environmentSettings.skyColor,
                storedLightmapDatas[toIndex].lightingData.sceneLightingData.environmentSettings.skyColor,
                blendFactor);
            RenderSettings.ambientEquatorColor = Color.Lerp(
                storedLightmapDatas[fromIndex].lightingData.sceneLightingData.environmentSettings.equatorColor,
                storedLightmapDatas[toIndex].lightingData.sceneLightingData.environmentSettings.equatorColor,
                blendFactor);
            RenderSettings.ambientGroundColor = Color.Lerp(
                storedLightmapDatas[fromIndex].lightingData.sceneLightingData.environmentSettings.groundColor,
                storedLightmapDatas[toIndex].lightingData.sceneLightingData.environmentSettings.groundColor,
                blendFactor);
            //RenderSettings.sun = Color.Lerp(
            //    storedLightmapDatas[fromIndex].lightingData.sceneLightingData.environmentSettings.groundColor,
            //    storedLightmapDatas[toIndex].lightingData.sceneLightingData.environmentSettings.groundColor,
            //    blendFactor);
        }

        private static void BlendLightProbesThread(object data)
        {
            BlendProbesThreadData threadData = data as BlendProbesThreadData;

            int counter = 0;

            if (threadData != null)
            {
                float[] exit = new float[threadData.blendFromArray.Length];
                float[][] combinedTemp = new float[Mathf.RoundToInt(exit.Length / 27)][];

                Parallel.For(0, threadData.blendFromArray.Length, (i =>
                {
                    exit[i] = Mathf.Lerp(
                        threadData.blendFromArray[i],
                        threadData.blendToArray[i],
                        threadData.blendFactor);
                }));

                for (int i = 0; i < exit.Length; i += 27)
                {
                    float[] temp = new float[27];
                    Array.Copy(exit, i, temp, 0, 27);
                    combinedTemp[counter] = temp;
                    counter++;
                }

                Parallel.For(0, combinedTemp.Length, (i, state) =>
                {
                    for (int j = 0; j < 3; j++)
                    {
                        for (int k = 0; k < 9; k++)
                        {
                            threadData.sphericalHarmonicsArray[i][j, k] = combinedTemp[i][j * 9 + k];
                        }
                    }
                });
            }

            blendProbesThreadsQueue.Enqueue(threadData);
            //System.GC.Collect(1, GCCollectionMode.Forced);
        }

        private static void LightProbesReplacingThread(object data)
        {
            ProbesReplacingThreadData threadData = data as ProbesReplacingThreadData;

            if (threadData != null)
            {
                SphericalHarmonicsL2[] finalArray = threadData.sphericalHarmonicsArray;

                if (finalArray.Length == threadData.lastProbesData.sphericalHarmonicsArray.Length)
                {
                    Array.Copy(
                        threadData.lastProbesData.sphericalHarmonicsArray,
                        0,
                        finalArray,
                        threadData.lastProbesData.lightProbesArrayPosition,
                        threadData.lastProbesData.sphericalHarmonicsArray.Length);
                }
            }

            probesReplacingThreadsQueue.Enqueue(threadData);
            //System.GC.Collect(1, GCCollectionMode.Forced);
        }

        private class ProbesReplacingThreadsPool
        {
            private List<ProbesReplacingThreadData> _objects;

            public ProbesReplacingThreadsPool(ProbesReplacingThreadData newObj)
            {
                _objects = new List<ProbesReplacingThreadData>();
            }

            public bool TryGet(out ProbesReplacingThreadData item)
            {
                if (_objects.Count > 0)
                {
                    int counter = -1;

                    for (int i = 0; i < _objects.Count; i++)
                    {
                        if (!_objects[i].isBusy)
                        {
                            counter = i;
                        }
                    }

                    if (counter != -1)
                    {
                        item = _objects[counter];
                        return true;
                    }
                    else
                    {
                        _objects.Add(item = new ProbesReplacingThreadData());
                        return false;
                    }
                }
                else
                {
                    _objects.Add(item = new ProbesReplacingThreadData());
                    return false;
                }
            }
        }

        private class BlendProbesThreadsPool
        {
            private List<BlendProbesThreadData> _objects;

            public BlendProbesThreadsPool(BlendProbesThreadData newObj)
            {
                _objects = new List<BlendProbesThreadData>();
            }

            public bool TryGet(out BlendProbesThreadData item)
            {
                if (_objects.Count > 0)
                {
                    int counter = -1;

                    for (int i = 0; i < _objects.Count; i++)
                    {
                        if (!_objects[i].isBusy)
                        {
                            counter = i;
                        }
                    }

                    if (counter != -1)
                    {
                        item = _objects[counter];
                        return true;
                    }
                    else
                    {
                        _objects.Add(item = new BlendProbesThreadData());
                        return false;
                    }
                }
                else
                {
                    _objects.Add(item = new BlendProbesThreadData());
                    return false;
                }
            }
        }

        private static ProbesReplacingThreadsPool _probesReplacingThreadDataPool = null;
        private static BlendProbesThreadsPool _blendProbesThreadDataPool = null;

        private static void BlendLightProbesData(MagicLightmapSwitcher switcherInstance,
            StoredLightingScenario storedLightmapScenario, int from, int to, float blendFactor)
        {
            LightProbes sceneProbesObject = LightmapSettings.lightProbes;

            if (_blendProbesThreadDataPool == null)
            {
                _blendProbesThreadDataPool = new BlendProbesThreadsPool(new BlendProbesThreadData());
            }

            if (_probesReplacingThreadDataPool == null)
            {
                _probesReplacingThreadDataPool = new ProbesReplacingThreadsPool(new ProbesReplacingThreadData());
            }

            if (sceneProbesObject == null)
            {
                return;
            }

            if (switcherInstance.stopProbesBlending)
            {
                return;
            }

            if (blendProbesThreadsQueue.Count > 3 ||
                probesReplacingThreadsQueue.Count > 3 ||
                sceneProbesObject == null)
            {
                blendProbesThreadsQueue.Clear();
                probesReplacingThreadsQueue.Clear();
                lightProbesArrayProcessing = false;
                return;
            }

            if (probesReplacingThreadsQueue.Count > 0)
            {
                lastReplacedProbesData = probesReplacingThreadsQueue.Dequeue();

                if (lastReplacedProbesData != null && lastReplacedProbesData.sphericalHarmonicsArray != null && lastReplacedProbesData.sphericalHarmonicsArray.Length > 0)
                {
                    if (LightmapSettings.lightProbes.bakedProbes.Length ==
                        lastReplacedProbesData.sphericalHarmonicsArray.Length)
                    {
                        LightmapSettings.lightProbes.bakedProbes = lastReplacedProbesData.sphericalHarmonicsArray;
                        lastReplacedProbesData.isBusy = false;
                    }
                }
            }

            if (!lightProbesArrayProcessing)
            {
                lightProbesArrayProcessing = true;

                if (blendProbesThreadsQueue.Count > 0)
                {
                    BlendProbesThreadData lastProbesData = blendProbesThreadsQueue.Dequeue();

                    if (lastProbesData != null)
                    {
                        lastProbesData.isBusy = false;

                        //_probesReplacingThreadDataPool.TryGet(out var probesReplacingThreadData);

                        ProbesReplacingThreadData probesReplacingThreadData = new ProbesReplacingThreadData();

                        probesReplacingThreadData.isBusy = true;
                        probesReplacingThreadData.switcherInstance = switcherInstance;
                        probesReplacingThreadData.lastProbesData = lastProbesData;
                        probesReplacingThreadData.sphericalHarmonicsArray = LightmapSettings.lightProbes.bakedProbes;

                        ThreadPool.QueueUserWorkItem(LightProbesReplacingThread, probesReplacingThreadData);
                    }
                }

                lightProbesArrayProcessing = false;
            }

            //_blendProbesThreadDataPool.TryGet(out var blendProbesThreadData);

            BlendProbesThreadData blendProbesThreadData = new BlendProbesThreadData();

            blendProbesThreadData.isBusy = true;
            blendProbesThreadData.switcherInstance = switcherInstance;
            blendProbesThreadData.lightProbesArrayPosition = storedLightmapScenario.lightProbesArrayPosition;
            blendProbesThreadData.blendFromArray = storedLightmapScenario.blendableLightmaps[from].lightingData
                .sceneLightingData.lightProbes1D;
            blendProbesThreadData.blendToArray = storedLightmapScenario.blendableLightmaps[to].lightingData
                .sceneLightingData.lightProbes1D;
            blendProbesThreadData.sphericalHarmonicsArray = new SphericalHarmonicsL2[storedLightmapScenario
                .blendableLightmaps[to].lightingData.sceneLightingData.lightProbes.Length];
            blendProbesThreadData.blendFactor = blendFactor;

            ThreadPool.QueueUserWorkItem(BlendLightProbesThread, blendProbesThreadData);

            if (switcherInstance.lightingDataSwitching)
            {
                switcherInstance.lightingDataSwitching = false;
                switcherInstance.StartCoroutine(_DoLightprobesBlendQueue(switcherInstance));

                switcherInstance.lightprobesBlendingStarted = false;
            }
        }

        private static IEnumerator _DoLightprobesBlendQueue(MagicLightmapSwitcher switcherInstance)
        {
            while (blendProbesThreadsQueue.Count == 0)
            {
                yield return null;
            }

            BlendProbesThreadData lastProbesData = blendProbesThreadsQueue.Dequeue();

            if (lastProbesData != null)
            {
                ProbesReplacingThreadData probesReplacingThreadData = new ProbesReplacingThreadData();

                probesReplacingThreadData.switcherInstance = switcherInstance;
                probesReplacingThreadData.lastProbesData = lastProbesData;
                probesReplacingThreadData.sphericalHarmonicsArray = LightmapSettings.lightProbes.bakedProbes;

                ThreadPool.QueueUserWorkItem(LightProbesReplacingThread, probesReplacingThreadData);
            }

            while (probesReplacingThreadsQueue.Count == 0)
            {
                yield return null;
            }

            while (probesReplacingThreadsQueue.Count > 0)
            {
                lastReplacedProbesData = probesReplacingThreadsQueue.Dequeue();

                if (lastReplacedProbesData != null && lastReplacedProbesData.sphericalHarmonicsArray != null)
                {
                    if (LightmapSettings.lightProbes.bakedProbes.Length == lastReplacedProbesData.sphericalHarmonicsArray.Length)
                    {
                        LightmapSettings.lightProbes.bakedProbes = lastReplacedProbesData.sphericalHarmonicsArray;
                    }
                }

                yield return null;
            }
        }
    }
}