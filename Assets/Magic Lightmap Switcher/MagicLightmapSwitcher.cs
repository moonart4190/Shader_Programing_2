using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using Object = UnityEngine.Object;
using System.Threading.Tasks;
using UnityEngine.Experimental.Rendering;

#if MT_HDRP_7_INCLUDED || MT_HDRP_8_INCLUDED || MT_HDRP_9_INCLUDED || MT_HDRP_10_INCLUDED || MT_HDRP_11_INCLUDED || MT_HDRP_12_INCLUDED || MT_HDRP_13_INCLUDED || MT_HDRP_14_INCLUDED
using UnityEngine.Rendering.HighDefinition;
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Callbacks;
using static MagicLightmapSwitcher.StoredLightmapData;
#endif

namespace MagicLightmapSwitcher
{
    [ExecuteInEditMode, System.Serializable, DefaultExecutionOrder(-2000)]
    public class MagicLightmapSwitcher : MonoBehaviour
    {
        #region Runtime Variables  
        public RuntimeAPI runtimeAPI;
        public class DynamicRendererAddedEvent : UnityEvent<GameObject, MLSDynamicRenderer> { }
        public class DynamicRendererRemoveEvent : UnityEvent<GameObject, AffectedObject> { }

        public static DynamicRendererAddedEvent OnDynamicRendererAdded;
        public static DynamicRendererRemoveEvent OnDynamicRendererRemoved;

        [System.Serializable]
        public class BlendingValueChanged : UnityEvent<StoredLightingScenario, float, float, float> { }
        [System.Serializable]
        public class LoadedLightmapChanged : UnityEvent<StoredLightingScenario, int> { }

        [SerializeField]
        public List<BlendingValueChanged> OnBlendingValueChanged = new List<BlendingValueChanged>();
        [SerializeField]
        public List<LoadedLightmapChanged> OnLoadedLightmapChanged = new List<LoadedLightmapChanged>();

        public enum Lightmapper
        {
            UnityLightmapper,
            BakeryLightmapper
        }
        
        public enum Workflow
        {
            MultiScene,
            SingleScene
        }

        public enum BlendingState
        {
            Enabled,
            Disabled
        }

        public enum BlendingOptions
        {
            All,
            Lightmaps,
            Reflections,
            None
        }

        [System.Serializable]
        public class BakedGroup
        {
            [SerializeField]
            public GameObject rootObject;
            [SerializeField]
            public List<Light> affectedLights = new List<Light>();
            [SerializeField]
            public List<GameObject> affectedObjects = new List<GameObject>();
        }

        public class AffectedObject
        {
            public MLSObject mlsObject;
            public Terrain terrain;
            public Renderer renderer;
            public ReflectionProbeUsage reflectionProbeUsage;
            public Material material;
            public int materialsCount;
            public bool isStatic;
            public string objectId;
            public MaterialPropertyBlock _propBlock;
            public List<ReflectionProbeBlendInfo> reflectionProbeBlendInfo = new List<ReflectionProbeBlendInfo>();
            public int lastFromIndex = -1;
            public List<Vector2> originalUVs = new List<Vector2>();

            public void InitPropertyBlock()
            {
                if (_propBlock == null)
                {
                    _propBlock = new MaterialPropertyBlock();
                }

                if (renderer != null)
                {
                    renderer.GetPropertyBlock(_propBlock);
                }
                else if (terrain != null)
                {
                    terrain.GetSplatMaterialPropertyBlock(_propBlock);
                }
            }

            public void SetShaderFloat(string property, float value)
            {                
                if (_propBlock != null)
                {
                    _propBlock.SetFloat(property, value);
                }
                else

                {
                    InitPropertyBlock();
                }
            }

            public void SetShaderVector(string property, Vector2 value)
            {
                if (_propBlock != null)
                {
                    _propBlock.SetVector(property, value);
                }
                else

                {
                    InitPropertyBlock();
                }
            }

            public void SetShaderColor(string property, Color value)
            {
                if (_propBlock != null)
                {
                    _propBlock.SetColor(property, value);
                }
                else
                {
                    InitPropertyBlock();
                }
            }

            public void SetShaderFloat(int nameID, float value)
            {
                if (_propBlock != null)
                {
                    _propBlock.SetFloat(nameID, value);
                }
            }

            public void SetShaderInt(string property, int value)
            {
                if (_propBlock != null)
                {
                    _propBlock.SetInt(property, value);
                }
            }

            public void SetShaderInt(int nameID, int value)
            {
                if (_propBlock != null)
                {
                    _propBlock.SetInt(nameID, value);
                }
            }

            public void SetShaderTexture(string property, Texture value)
            {
                if (_propBlock != null && value != null)
                {
                    _propBlock.SetTexture(property, value);
                }
            }

            public void SetShaderTexture(int nameID, Texture value)
            {
                if (_propBlock != null && value != null)
                {
                    _propBlock.SetTexture(nameID, value);
                }
            }

            public void ApplyPropertyBlock()
            {
                if (_propBlock != null)
                {
                    if (renderer != null)
                    {
                        renderer.SetPropertyBlock(_propBlock);
                    }
                    else if (terrain != null)
                    {
                        terrain.SetSplatMaterialPropertyBlock(_propBlock);
                    }
                }
            }
        }

        #region Multi-Scene Workflow
        public Dictionary<string, List<AffectedObject>> staticAffectedObjects = new Dictionary<string, List<AffectedObject>>();
        public Dictionary<string, List<AffectedObject>> dynamicAffectedObjects = new Dictionary<string, List<AffectedObject>>();
        public Dictionary<string, List<StoredLightmapData>> orderedStoredLightmapDatas = new Dictionary<string, List<StoredLightmapData>>();
        public Dictionary<string, List<StoredLightmapData>> storedLightmapDatas = new Dictionary<string, List<StoredLightmapData>>();
        public Dictionary<string, List<StoredLightingScenario>> storedLightmapScenarios = new Dictionary<string, List<StoredLightingScenario>>();
        public Dictionary<string, List<MLSLight>> storedLights = new Dictionary<string, List<MLSLight>>();
        public Dictionary<string, List<ReflectionProbe>> storedReflectionProbes = new Dictionary<string, List<ReflectionProbe>>();
        #endregion

        #region Single Scene Workflow
        public List<AffectedObject> affectedTerrains = new List<AffectedObject>();
        public List<AffectedObject> sceneStaticAffectedObjects = new List<AffectedObject>();
        public List<AffectedObject> sceneDynamicAffectedObjects = new List<AffectedObject>();
        public List<StoredLightmapData> sceneLightmapDatas = new List<StoredLightmapData>();
        public List<StoredLightingScenario> sceneLightmapScenarios = new List<StoredLightingScenario>();
        public List<MLSLight> sceneAffectedLightSources = new List<MLSLight>();
        public List<ReflectionProbe> sceneReflectionProbes = new List<ReflectionProbe>();
        public List<Vector3> sceneReflectionProbePositions = new List<Vector3>();
        #endregion

        public List<BakedGroup> bakedGroup = new List<BakedGroup>();
        public List<StoredLightingScenario> availableScenarios = new List<StoredLightingScenario>();
        public StoredLightingScenario currentLightmapScenario;
        public StoredLightingScenario lastLightmapScenario;

        public Lightmapper lightmapper = Lightmapper.UnityLightmapper;
        public Workflow workflow;
        public Workflow lastWorkflow;
        public BlendingOptions currentBlendingState;
        public string currentDataPath;
        public bool loadFromAssetBundles;
        public int storedAssetsCount;
        public static Cubemap defaultCubeBlack;
        public bool resetAffectedObjects;
        public SystemProperties systemProperties;
        public int lastSelectedScene;
        public string sceneToUnload;
        public string lastLoadedscene;
        public bool storedDataUpdated = false;
        public bool storedDataUpdatingProcess = false;
        public bool lightingDataSwitching;
        public bool stopProbesBlending;
        public bool lightmapArrayInitialized;
        public bool cubemapArrayInitialized;
        public bool useTextureArrays;
        public bool blendingProcess;
        public bool globalDataArraysInitialized;
        public bool loadScenarioOnStart;
        public int onStartLightingScenario;
        public int onStartPreset;
        public int becameVisibleObjects;
        private int lastSceneCount;
        private string workPath;
        #endregion

#if UNITY_EDITOR
        #region Editor Variables 
        public SerializedObject switcherSerializedObject;

        public enum StoringMode
        {
            Once,
            Queue
        }

        [System.Serializable]
        public class SceneLightingPreset
        {
            [System.Serializable]
            public class LightSourceSettings
            {
#if MT_HDRP_7_INCLUDED || MT_HDRP_8_INCLUDED || MT_HDRP_9_INCLUDED || MT_HDRP_10_INCLUDED || MT_HDRP_11_INCLUDED || MT_HDRP_12_INCLUDED || MT_HDRP_13_INCLUDED || MT_HDRP_14_INCLUDED
                public HDAdditionalLightData additionalLightData;
#endif
                public Light light;
                public MLSLight mlsLight;
                public string mlsLightUID;
                public LightType lightType;
                public Vector3 position;
                public Vector3 rotation;
                public Color color;
                public float colorTemperature;
                public float intensity;
                public float indirectMultiplier;
                public float range;
                public float radius;
                public float spotOuterAngle;
                public LightShadows shadowsType;
                public float shadowStrength;
                public float spotInnerAngle;
                public float bakedShadowsRadius;
                public float directionalBakedShadowAngle;
                public float areaWidth;
                public float areaHeight;
                public bool globalFoldoutEnabled;
                public bool transformFoldoutEnabled;
                public bool settingsFoldoutEnabled;
                public bool justAdded = true;
                public Texture lightCookie;
                public Vector2 lightCookieSize;
                public Vector2 lightCookieOffset;

#if BAKERY_INCLUDED
                [System.Serializable]
                public class BakeryDirectLightsSettings
                {
                    public GameObject parentGameObject;
                    public BakeryDirectLight bakeryDirect;
                    public int UID;
                    public Color color;
                    public float intensity;
                    public float shadowSpread;
                    public int samples;
                    public int bitmask;
                    public bool bakeToIndirect;
                    public bool shadowmask;
                    public bool shadowmaskDenoise;
                    public float indirectIntensity;
                    public Texture2D cloudShadow;
                    public float cloudShadowTilingX;
                    public float cloudShadowTilingY;
                    public float cloudShadowOffsetX, cloudShadowOffsetY;
                    public bool bakeryDirectFoldoutEnabled;
                }

                [System.Serializable]
                public class BakeryPointLightsSettings
                {
                    public GameObject parentGameObject;
                    public BakeryPointLight bakeryPoint;
                    public int UID;
                    public Color color;
                    public float intensity;
                    public float shadowSpread;
                    public float cutoff;
                    public bool realisticFalloff;
                    public int samples;
                    public BakeryPointLight.ftLightProjectionMode projMode;
                    public Texture2D cookie;
                    public float angle;
                    public float innerAngle;
                    public Cubemap cubemap;
                    public UnityEngine.Object iesFile;
                    public int bitmask;
                    public bool bakeToIndirect;
                    public bool shadowmask;
                    public float indirectIntensity;
                    public float falloffMinRadius;
                    public float screenRadius;
                    public bool bakeryPointFoldoutEnabled;
                }

                [System.Serializable]
                public class BakeryLightMeshesSettings
                {
                    public GameObject parentGameObject;
                    public BakeryLightMesh bakeryLightMesh;
                    public int UID;
                    public List<MeshFilter> All;
                    public Color color;
                    public float intensity;
                    public Texture2D texture;
                    public float cutoff;
                    public int samples;
                    public int samples2;
                    public int bitmask;
                    public bool selfShadow;
                    public bool bakeToIndirect;
                    public float indirectIntensity;
                    public int lmid;
                    public bool bakeryLightMeshFoldoutEnabled;
                }                

                [SerializeField]
                public BakeryDirectLightsSettings bakeryDirectLightsSettings;
                [SerializeField]
                public BakeryPointLightsSettings bakeryPointLightsSettings;
                [SerializeField]
                public BakeryLightMeshesSettings bakeryLightMeshesSettings;
#endif
            }

            [System.Serializable]
            public class GameObjectSettings
            {
                [System.Serializable]
                public class ObjectShaderProperty
                {
                    [SerializeField]
                    public bool trackable;
                    [SerializeField]
                    public string name;
                    [SerializeField]
                    public ShaderPropertyType type;
                    [SerializeField]
                    public float floatValue;
                    [SerializeField]
                    public Color colorValue;
                    [SerializeField]
                    public Texture textureValue;
                    [SerializeField]
                    public float min;
                    [SerializeField]
                    public float max;

                    public ObjectShaderProperty(ObjectShaderProperty copyFrom = null)
                    {
                        if (copyFrom != null)
                        {
                            trackable = copyFrom.trackable;
                            name = copyFrom.name;
                            type = copyFrom.type;
                            floatValue = copyFrom.floatValue;
                            colorValue = copyFrom.colorValue;
                            textureValue = copyFrom.textureValue;
                            min = copyFrom.min;
                            max = copyFrom.max;
                        }
                    }

                    public void Create(Material inputMaterial, Shader inputShader, int propertyIndex)
                    {
                        name = inputShader.GetPropertyName(propertyIndex);
                        type = inputShader.GetPropertyType(propertyIndex);

                        switch (type)
                        {
                            case ShaderPropertyType.Float:
                                floatValue = inputMaterial.GetFloat(name);
                                break;
                            case ShaderPropertyType.Color:
                                colorValue = inputMaterial.GetColor(name);
                                break;
                            case ShaderPropertyType.Range:
                                var minMax = inputMaterial.shader.GetPropertyRangeLimits(propertyIndex);

                                floatValue = inputMaterial.GetFloat(name);
                                min = minMax.x;
                                max = minMax.y;
                                break;
                            case ShaderPropertyType.Texture:
                                textureValue = inputMaterial.GetTexture(name);
                                break;
                        }
                    }
                }

                public GameObjectSettings(GameObjectSettings copyFrom = null)
                {
                    if (copyFrom != null)
                    {
                        currentMaterial = copyFrom.currentMaterial;
                        lastMaterial = copyFrom.lastMaterial;
                        gameObject = copyFrom.gameObject;
                        instanceId = copyFrom.instanceId;
                        enabled = copyFrom.enabled;
                        justAdded = copyFrom.enabled;
                        position = copyFrom.position;
                        rotation = copyFrom.rotation;
                        tempRotation = copyFrom.tempRotation;
                        globalFoldoutEnabled = copyFrom.globalFoldoutEnabled;
                        transformFoldoutEnabled = copyFrom.globalFoldoutEnabled;
                        materialFoldoutEnabled = copyFrom.materialFoldoutEnabled;

                        if (copyFrom.objectShaderProperties != null)
                        {
                            objectShaderProperties = new List<ObjectShaderProperty>(copyFrom.objectShaderProperties.Count);

                            copyFrom.objectShaderProperties.ForEach((item) =>
                            {
                                objectShaderProperties.Add(new ObjectShaderProperty(item));
                            });
                        }
                    }
                }

                public bool UpdatePropertyBlock()
                {
                    //if (propertyBlock == null)
                    {
                        if (meshRenderer == null)
                        {
                            return false;
                        }

                        propertyBlock = new MaterialPropertyBlock();
                        meshRenderer.GetPropertyBlock(propertyBlock);
                    }

                    return true;
                }

                [SerializeField]
                public List<ObjectShaderProperty> objectShaderProperties = new List<ObjectShaderProperty>();
                [SerializeField]
                public Material currentMaterial;
                [SerializeField]
                public Material lastMaterial;
                [SerializeField]
                public MeshRenderer meshRenderer;
                [SerializeField]
                public MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
                [SerializeField]
                public GameObject gameObject;
                [SerializeField]
                public int instanceId;
                [SerializeField]
                public bool enabled;
                [SerializeField]
                public bool justAdded = true;
                [SerializeField]
                public Vector3 position;
                [SerializeField]
                public Quaternion rotation;
                [SerializeField]
                public Vector3 tempRotation;
                [SerializeField]
                public bool globalFoldoutEnabled;
                [SerializeField]
                public bool transformFoldoutEnabled;
                [SerializeField]
                public bool materialFoldoutEnabled;
                [SerializeField]
                public List<string> options = new List<string>();
                [SerializeField]
                public LayerMask trackableShaderOptions = 1 << 0;
            }

            [System.Serializable]
            public class FogSettings
            {
                [SerializeField]
                public bool enabled;
                [SerializeField]
                public Color fogColor;
                [SerializeField]
                public float fogDensity;
                [SerializeField]
                public bool globalFoldoutEnabled;

                public FogSettings() { }

                public FogSettings(FogSettings source)
                {
                    this.enabled = source.enabled;
                    this.fogColor = source.fogColor;
                    this.fogDensity = source.fogDensity;
                }
            }

            [System.Serializable]
            public class CustomBlendablesSettings
            {
                public CustomBlendablesSettings(CustomBlendablesSettings copyFrom = null)
                {
                    if (copyFrom != null)
                    {
                        sourceScript = copyFrom.sourceScript;
                        sourceScriptId = copyFrom.sourceScriptId;

                        List<string> blendableFloatParametersClone = new List<string>(copyFrom.blendableFloatParameters.Count);
                        List<float> blendableFloatParametersValuesClone = new List<float>(copyFrom.blendableFloatParametersValues.Count);
                        List<string> blendableCubemapParametersClone = new List<string>(copyFrom.blendableCubemapParameters.Count);
                        List<Cubemap> blendableCubemapParametersValuesClone = new List<Cubemap>(copyFrom.blendableCubemapParametersValues.Count);
                        List<string> blendableColorParametersClone = new List<string>(copyFrom.blendableColorParameters.Count);
                        List<Color> blendableColorParametersValuesClone = new List<Color>(copyFrom.blendableColorParametersValues.Count);

                        copyFrom.blendableFloatParameters.ForEach((item) =>
                        {
                            blendableFloatParametersClone.Add(item);
                        });                       
                        
                        copyFrom.blendableFloatParametersValues.ForEach((item) =>
                        {
                            blendableFloatParametersValuesClone.Add(item);
                        });

                        copyFrom.blendableCubemapParameters.ForEach((item) =>
                        {
                            blendableCubemapParametersClone.Add(item);
                        });

                        copyFrom.blendableCubemapParametersValues.ForEach((item) =>
                        {
                            blendableCubemapParametersValuesClone.Add(item);
                        });

                        copyFrom.blendableColorParameters.ForEach((item) =>
                        {
                            blendableColorParametersClone.Add(item);
                        });

                        copyFrom.blendableColorParametersValues.ForEach((item) =>
                        {
                            blendableColorParametersValuesClone.Add(item);
                        });

                        blendableFloatParameters = blendableFloatParametersClone;
                        blendableFloatParametersValues = blendableFloatParametersValuesClone;
                        blendableCubemapParameters = blendableCubemapParametersClone;
                        blendableCubemapParametersValues = blendableCubemapParametersValuesClone;
                        blendableColorParameters = blendableColorParametersClone;
                        blendableColorParametersValues = blendableColorParametersValuesClone;
                        globalFoldoutEnabled = copyFrom.globalFoldoutEnabled;
                    }
                }

                [SerializeField]
                public MLSCustomBlendable sourceScript;
                [SerializeField]
                public string sourceScriptId;
                [SerializeField]
                public List<string> blendableFloatParameters = new List<string>();
                [SerializeField]
                public List<float> blendableFloatParametersValues = new List<float>();
                [SerializeField]
                public List<string> blendableCubemapParameters = new List<string>();
                [SerializeField]
                public List<Cubemap> blendableCubemapParametersValues = new List<Cubemap>();
                [SerializeField]
                public List<string> blendableColorParameters = new List<string>();
                [SerializeField]
                public List<Color> blendableColorParametersValues = new List<Color>();
                [SerializeField]
                public List<string> blendableBoolParameters = new List<string>();
                [SerializeField]
                public List<bool> blendableBoolParametersValues = new List<bool>();
                [SerializeField]
                public bool globalFoldoutEnabled;
            }

            [System.Serializable]
            public class SkyboxSettings
            {
                public enum SkyboxType
                {
                    Custom,
                    SimpleCubemap
                }

                [System.Serializable]
                public class SkyboxShaderProperty
                {
                    [SerializeField]
                    public bool trackable;
                    [SerializeField]
                    public string name;
                    [SerializeField]
                    public ShaderPropertyType type;
                    [SerializeField]
                    public float floatValue;
                    [SerializeField]
                    public Color colorValue;
                    [SerializeField]
                    public Texture textureValue;
                    [SerializeField]
                    public float min;
                    [SerializeField]
                    public float max;

                    public SkyboxShaderProperty(SkyboxShaderProperty copyFrom = null)
                    {
                        if (copyFrom != null)
                        {
                            trackable = copyFrom.trackable;
                            name = copyFrom.name;
                            type = copyFrom.type;
                            floatValue = copyFrom.floatValue;
                            colorValue = copyFrom.colorValue;
                            textureValue = copyFrom.textureValue;
                            min = copyFrom.min;
                            max = copyFrom.max;
                        }
                    }

                    public void Create(Material inputMaterial, Shader inputShader, int propertyIndex)
                    {
                        name = inputShader.GetPropertyName(propertyIndex);
                        type = inputShader.GetPropertyType(propertyIndex);

                        switch (type)
                        {
                            case ShaderPropertyType.Float:
                                floatValue = inputMaterial.GetFloat(name);
                                break;
                            case ShaderPropertyType.Color:
                                colorValue = inputMaterial.GetColor(name);
                                break;
                            case ShaderPropertyType.Range:
                                var minMax = inputMaterial.shader.GetPropertyRangeLimits(propertyIndex);

                                floatValue = inputMaterial.GetFloat(name);
                                min = minMax.x;
                                max = minMax.y;
                                break;
                            case ShaderPropertyType.Texture:
                                textureValue = inputMaterial.GetTexture(name);
                                break;
                        }
                    }
                }

                public SkyboxSettings(SkyboxSettings copyFrom = null)
                {
                    if (copyFrom != null)
                    {
                        skyboxType = copyFrom.skyboxType;
                        skyboxTexture = copyFrom.skyboxTexture;
                        exposure = copyFrom.exposure;
                        tintColor = copyFrom.tintColor;
                        globalFoldoutEnabled = copyFrom.globalFoldoutEnabled;

                        if (copyFrom.skyboxShaderProperties != null)
                        {
                            skyboxShaderProperties = new List<SkyboxShaderProperty>(copyFrom.skyboxShaderProperties.Count);

                            copyFrom.skyboxShaderProperties.ForEach((item) =>
                            {
                                skyboxShaderProperties.Add(new SkyboxShaderProperty(item));
                            });
                        }
                    }
                }

                [SerializeField]
                public List<SkyboxShaderProperty> skyboxShaderProperties;
                [SerializeField]
                public SkyboxType skyboxType;
                [SerializeField]
                public Cubemap skyboxTexture;
                [SerializeField]
                public float exposure;
                [SerializeField]
                public Color tintColor;
                [SerializeField]
                public bool globalFoldoutEnabled;

#if BAKERY_INCLUDED
                [System.Serializable]
                public class BakerySkyLightsSettings
                {
                    public BakerySkyLightsSettings(BakerySkyLightsSettings copyFrom = null)
                    {
                        if (copyFrom != null)
                        {
                            parentGameObject = copyFrom.parentGameObject;
                            bakerySky = copyFrom.bakerySky;
                            UID = copyFrom.UID;
                            texName = copyFrom.texName;
                            color = copyFrom.color;
                            intensity = copyFrom.intensity;
                            samples = copyFrom.samples;
                            hemispherical = copyFrom.hemispherical;
                            bitmask = copyFrom.bitmask;
                            bakeToIndirect = copyFrom.bakeToIndirect;
                            indirectIntensity = copyFrom.indirectIntensity;
                            tangentSH = copyFrom.tangentSH;
                            cubemap = copyFrom.cubemap;
                            correctRotation = copyFrom.correctRotation;
                            cubemapAngles = copyFrom.cubemapAngles;
                            bakerySkylightFoldoutEnabled = copyFrom.bakerySkylightFoldoutEnabled;
                            contrib = copyFrom.contrib;
                        }
                    }

                    public BakerySkyLightsSettings(BakerySkyLight input = null)
                    {
                        if (input != null)
                        {                            
                            bakerySky = input;
                            UID = input.UID;
                            texName = input.texName;
                            color = input.color;
                            intensity = input.intensity;
                            samples = input.samples;
                            hemispherical = input.hemispherical;
                            bitmask = input.bitmask;
                            bakeToIndirect = input.bakeToIndirect;
                            indirectIntensity = input.indirectIntensity;
                            tangentSH = input.tangentSH;
                            cubemap = input.cubemap;
                            correctRotation = input.correctRotation;
                            //cubemapAngles = input.;
                            bakerySkylightFoldoutEnabled = false;
                            contrib = ftDirectLightInspector.BakeWhat.DirectAndIndirect;
                        }
                    }

                    public GameObject parentGameObject;
                    public BakerySkyLight bakerySky;
                    public int UID;
                    public string texName;
                    public Color color;
                    public float intensity;
                    public int samples;
                    public bool hemispherical;
                    public int bitmask;
                    public bool bakeToIndirect;
                    public float indirectIntensity;
                    public bool tangentSH;
                    public Cubemap cubemap;
                    public bool correctRotation = false;                    
                    public Quaternion cubemapAngles;
                    public bool bakerySkylightFoldoutEnabled;
                    public ftDirectLightInspector.BakeWhat contrib;                    
                }                

                [SerializeField]
                public BakerySkyLightsSettings bakerySkyLightsSettings = null;
#endif
            }

            [System.Serializable]
            public class EnvironmentSettings
            {
                [SerializeField]
                public AmbientMode source;
                [SerializeField]
                public Color ambientColor;
                [SerializeField]
                public Color skyColor;
                [SerializeField]
                public Color equatorColor;
                [SerializeField]
                public Color groundColor;
                [SerializeField]
                public float intensityMultiplier;
                [SerializeField]
                public bool globalFoldoutEnabled;
				[SerializeField]
                public Light sun;

                public EnvironmentSettings() { }

                public EnvironmentSettings(EnvironmentSettings source)
                {
                    this.source = source.source;
                    this.ambientColor = source.ambientColor;
                    this.skyColor = source.skyColor;
                    this.equatorColor = source.equatorColor;
                    this.groundColor = source.groundColor;
                    this.intensityMultiplier = source.intensityMultiplier;
                    this.globalFoldoutEnabled = source.globalFoldoutEnabled;
					this.sun = source.sun;
                }
            }

            [SerializeField]
            public string name;
            [SerializeField]
            public bool included = true;
            [SerializeField]
            public List<LightSourceSettings> lightSourceSettings = new List<LightSourceSettings>();
            [SerializeField]
            public List<CustomBlendablesSettings> customBlendablesSettings = new List<CustomBlendablesSettings>();
            [SerializeField]
            public List<GameObjectSettings> gameObjectsSettings = new List<GameObjectSettings>();
            [SerializeField]
            public SkyboxSettings skyboxSettings = new SkyboxSettings();
            [SerializeField]
            public EnvironmentSettings environmentSettings = new EnvironmentSettings();
            [SerializeField]
            public FogSettings fogSettings = new FogSettings();
            [SerializeField]
            public LightmapParameters lightmapParameters;
            [SerializeField]
            public bool foldoutEnabled;

#if BAKERY_INCLUDED
            [SerializeField]
            public List<LightSourceSettings.BakeryLightMeshesSettings> bakeryLightMeshesSettings = new List<LightSourceSettings.BakeryLightMeshesSettings>();
#endif

            public void MatchSceneSettings()
            {
                Shader.SetGlobalInt("_MLS_ENABLE_LIGHTMAPS_BLENDING", 0);
                Shader.SetGlobalInt("_MLS_ENABLE_REFLECTIONS_BLENDING", 0);
                Shader.SetGlobalInt("_MLS_ENABLE_SKY_CUBEMAPS_BLENDING", 0);

                GameObject[] gameObjects = Resources.FindObjectsOfTypeAll<GameObject>();

                for (int i = 0; i < gameObjects.Length; i++)
                {
                    GameObjectSettings currentSettings = gameObjectsSettings.Find(item => item.gameObject == gameObjects[i]);

                    if (currentSettings != null)
                    {
                        gameObjects[i].SetActive(currentSettings.enabled);
                        gameObjects[i].transform.localPosition = currentSettings.position;
                        gameObjects[i].transform.rotation = currentSettings.rotation;

                        if (currentSettings.objectShaderProperties == null || currentSettings.objectShaderProperties.Count == 0)
                        {
                            continue;
                        }

                        if (!currentSettings.UpdatePropertyBlock())
                        {
                            continue;
                        }

                        for (int p = 0; p < currentSettings.objectShaderProperties.Count; p++)
                        {
                            if (currentSettings.objectShaderProperties[p].trackable)
                            {
                                switch (currentSettings.objectShaderProperties[p].type)
                                {
                                    case ShaderPropertyType.Float:
                                    case ShaderPropertyType.Range:
                                        currentSettings.propertyBlock.SetFloat(
                                            currentSettings.objectShaderProperties[p].name,
                                            currentSettings.objectShaderProperties[p].floatValue);
                                        break;
                                    case ShaderPropertyType.Color:
                                        currentSettings.propertyBlock.SetColor(
                                            currentSettings.objectShaderProperties[p].name,
                                            currentSettings.objectShaderProperties[p].colorValue);
                                        break;
                                    case ShaderPropertyType.Texture:
                                        // currentSettings.propertyBlock.SetTexture(
                                        //     currentSettings.objectShaderProperties[p].name,
                                        //     currentSettings.objectShaderProperties[p].textureValue);
                                        break;
                                }

                                SceneView.RepaintAll();
                            }
                        }

                        currentSettings.meshRenderer.SetPropertyBlock(currentSettings.propertyBlock);                        
                    }
                }

                Light[] sceneLights = FindObjectsOfType<Light>();

                for (int i = 0; i < sceneLights.Length; i++)
                {                    
                    LightSourceSettings currentSettings = lightSourceSettings.Find(item => item.light == sceneLights[i]);

                    if (currentSettings != null)
                    {
                        if (!currentSettings.mlsLight.editedDirectly)
                        {
                            if (sceneLights[i].transform != null)
                            {
                                sceneLights[i].transform.localPosition = currentSettings.position;
                            }
                            else
                            {
                                sceneLights[i].transform.position = currentSettings.position;
                            }

                            TransformUtils.SetInspectorRotation(sceneLights[i].transform, currentSettings.rotation);
                            sceneLights[i].color = currentSettings.color;
                            sceneLights[i].colorTemperature = currentSettings.colorTemperature;
                            sceneLights[i].range = currentSettings.range;

                            if (currentSettings.lightType == LightType.Rectangle)
                            {
                                currentSettings.lightType = LightType.Point;
                                sceneLights[i].type = LightType.Point;
                                sceneLights[i].range = currentSettings.range;

                                sceneLights[i].type = LightType.Rectangle;
                                currentSettings.lightType = LightType.Rectangle;
                            }
                            else
                            {
                                sceneLights[i].range = currentSettings.range;
                            }

                            sceneLights[i].spotAngle = currentSettings.spotOuterAngle;
                            sceneLights[i].innerSpotAngle = currentSettings.spotInnerAngle;
                            sceneLights[i].areaSize = new Vector2(currentSettings.areaWidth, currentSettings.areaHeight);
                            sceneLights[i].bounceIntensity = currentSettings.indirectMultiplier;

#if MT_HDRP_7_INCLUDED || MT_HDRP_8_INCLUDED || MT_HDRP_9_INCLUDED || MT_HDRP_10_INCLUDED || MT_HDRP_11_INCLUDED || MT_HDRP_12_INCLUDED || MT_HDRP_13_INCLUDED || MT_HDRP_14_INCLUDED
                            if (currentSettings.additionalLightData != null)
                            {
                                sceneLights[i].GetComponent<HDAdditionalLightData>().intensity = currentSettings.intensity;
                                sceneLights[i].GetComponent<HDAdditionalLightData>().type = currentSettings.additionalLightData.type;
                            }
#else                            
                            sceneLights[i].type = currentSettings.lightType;
                            sceneLights[i].intensity = currentSettings.intensity;
                            sceneLights[i].shadowRadius = currentSettings.bakedShadowsRadius;
                            sceneLights[i].shadowAngle = currentSettings.directionalBakedShadowAngle;
                            sceneLights[i].shadows = currentSettings.shadowsType;
                            sceneLights[i].shadowStrength = currentSettings.shadowStrength;
                            sceneLights[i].cookie = currentSettings.lightCookie;
#endif

#if BAKERY_INCLUDED
                            if (currentSettings.bakeryDirectLightsSettings != null)
                            {
                                if (currentSettings.bakeryDirectLightsSettings.bakeryDirect != null)
                                {
                                    currentSettings.bakeryDirectLightsSettings.bakeryDirect.color = currentSettings.color;
                                    currentSettings.bakeryDirectLightsSettings.bakeryDirect.intensity = currentSettings.intensity;
                                    currentSettings.bakeryDirectLightsSettings.bakeryDirect.shadowSpread = currentSettings.bakeryDirectLightsSettings.shadowSpread;
                                    currentSettings.bakeryDirectLightsSettings.bakeryDirect.samples = currentSettings.bakeryDirectLightsSettings.samples;
                                    currentSettings.bakeryDirectLightsSettings.bakeryDirect.bitmask = currentSettings.bakeryDirectLightsSettings.bitmask;
                                    currentSettings.bakeryDirectLightsSettings.bakeryDirect.bakeToIndirect = currentSettings.bakeryDirectLightsSettings.bakeToIndirect;
                                    currentSettings.bakeryDirectLightsSettings.bakeryDirect.shadowmask = currentSettings.bakeryDirectLightsSettings.shadowmask;
                                    currentSettings.bakeryDirectLightsSettings.bakeryDirect.shadowmaskDenoise = currentSettings.bakeryDirectLightsSettings.shadowmaskDenoise;
                                    currentSettings.bakeryDirectLightsSettings.bakeryDirect.indirectIntensity = currentSettings.bakeryDirectLightsSettings.indirectIntensity;
                                    currentSettings.bakeryDirectLightsSettings.bakeryDirect.cloudShadow = currentSettings.bakeryDirectLightsSettings.cloudShadow;
                                    currentSettings.bakeryDirectLightsSettings.bakeryDirect.cloudShadowTilingX = currentSettings.bakeryDirectLightsSettings.cloudShadowTilingX;
                                    currentSettings.bakeryDirectLightsSettings.bakeryDirect.cloudShadowTilingY = currentSettings.bakeryDirectLightsSettings.cloudShadowTilingY;
                                    currentSettings.bakeryDirectLightsSettings.bakeryDirect.cloudShadowOffsetX = currentSettings.bakeryDirectLightsSettings.cloudShadowOffsetX;
                                    currentSettings.bakeryDirectLightsSettings.bakeryDirect.cloudShadowOffsetY = currentSettings.bakeryDirectLightsSettings.cloudShadowOffsetY;

                                    //EditorUtility.SetDirty(currentSettings.bakeryDirectLightsSettings.bakeryDirect);
                                }
                                else
                                {
                                    currentSettings.bakeryDirectLightsSettings = null;
                                }
                            }

                            if (currentSettings.bakeryPointLightsSettings != null)
                            {
                                if (currentSettings.bakeryPointLightsSettings.bakeryPoint != null)
                                {
                                    currentSettings.bakeryPointLightsSettings.bakeryPoint.color = currentSettings.color;
                                    currentSettings.bakeryPointLightsSettings.bakeryPoint.intensity = currentSettings.intensity;
                                    currentSettings.bakeryPointLightsSettings.bakeryPoint.shadowSpread = currentSettings.bakeryPointLightsSettings.shadowSpread;
                                    currentSettings.bakeryPointLightsSettings.bakeryPoint.cutoff = currentSettings.bakeryPointLightsSettings.cutoff;
                                    currentSettings.bakeryPointLightsSettings.bakeryPoint.realisticFalloff = currentSettings.bakeryPointLightsSettings.realisticFalloff;
                                    currentSettings.bakeryPointLightsSettings.bakeryPoint.samples = currentSettings.bakeryPointLightsSettings.samples;
                                    currentSettings.bakeryPointLightsSettings.bakeryPoint.projMode = currentSettings.bakeryPointLightsSettings.projMode;
                                    currentSettings.bakeryPointLightsSettings.bakeryPoint.cookie = currentSettings.bakeryPointLightsSettings.cookie;
                                    currentSettings.bakeryPointLightsSettings.bakeryPoint.angle = currentSettings.bakeryPointLightsSettings.angle;
                                    currentSettings.bakeryPointLightsSettings.bakeryPoint.innerAngle = currentSettings.bakeryPointLightsSettings.innerAngle;
                                    currentSettings.bakeryPointLightsSettings.bakeryPoint.cubemap = currentSettings.bakeryPointLightsSettings.cubemap;
                                    currentSettings.bakeryPointLightsSettings.bakeryPoint.iesFile = currentSettings.bakeryPointLightsSettings.iesFile;
                                    currentSettings.bakeryPointLightsSettings.bakeryPoint.bitmask = currentSettings.bakeryPointLightsSettings.bitmask;
                                    currentSettings.bakeryPointLightsSettings.bakeryPoint.bakeToIndirect = currentSettings.bakeryPointLightsSettings.bakeToIndirect;
                                    currentSettings.bakeryPointLightsSettings.bakeryPoint.shadowmask = currentSettings.bakeryPointLightsSettings.shadowmask;
                                    currentSettings.bakeryPointLightsSettings.bakeryPoint.indirectIntensity = currentSettings.bakeryPointLightsSettings.indirectIntensity;
                                    currentSettings.bakeryPointLightsSettings.bakeryPoint.falloffMinRadius = currentSettings.bakeryPointLightsSettings.falloffMinRadius;

                                    EditorUtility.SetDirty(currentSettings.bakeryPointLightsSettings.bakeryPoint);
                                }
                                else
                                {
                                    currentSettings.bakeryPointLightsSettings = null;
                                }
                            }

                            if (currentSettings.bakeryLightMeshesSettings != null)
                            {
                                if (currentSettings.bakeryLightMeshesSettings.bakeryLightMesh != null)
                                {
                                    currentSettings.bakeryLightMeshesSettings.bakeryLightMesh.color = currentSettings.color;
                                    currentSettings.bakeryLightMeshesSettings.bakeryLightMesh.intensity = currentSettings.intensity;
                                    currentSettings.bakeryLightMeshesSettings.bakeryLightMesh.texture = currentSettings.bakeryLightMeshesSettings.texture;
                                    currentSettings.bakeryLightMeshesSettings.bakeryLightMesh.cutoff = currentSettings.bakeryLightMeshesSettings.cutoff;
                                    currentSettings.bakeryLightMeshesSettings.bakeryLightMesh.samples = currentSettings.bakeryLightMeshesSettings.samples;
                                    currentSettings.bakeryLightMeshesSettings.bakeryLightMesh.samples2 = currentSettings.bakeryLightMeshesSettings.samples2;
                                    currentSettings.bakeryLightMeshesSettings.bakeryLightMesh.bitmask = currentSettings.bakeryLightMeshesSettings.bitmask;
                                    currentSettings.bakeryLightMeshesSettings.bakeryLightMesh.selfShadow = currentSettings.bakeryLightMeshesSettings.selfShadow;
                                    currentSettings.bakeryLightMeshesSettings.bakeryLightMesh.bakeToIndirect = currentSettings.bakeryLightMeshesSettings.bakeToIndirect;
                                    currentSettings.bakeryLightMeshesSettings.bakeryLightMesh.indirectIntensity = currentSettings.bakeryLightMeshesSettings.indirectIntensity;
                                    currentSettings.bakeryLightMeshesSettings.bakeryLightMesh.lmid = currentSettings.bakeryLightMeshesSettings.lmid;

                                    //EditorUtility.SetDirty(currentSettings.bakeryLightMeshesSettings.bakeryLightMesh);
                                }
                                else
                                {
                                    currentSettings.bakeryLightMeshesSettings = null;
                                }
                            }
#endif
                        }
                        else
                        {
                            if (EditorUtility.DisplayDialog("Magic Lightmap Switcher", 
                                "The light source has settings unaccounted for by the preset manager. Update settings?", "Update", "Revert"))
                            {
                                currentSettings.mlsLight.editedDirectly = false;
                                UpdatePresetData();
                            }
                        }
                    }
                }

                if (skyboxSettings.skyboxShaderProperties == null)
                {
                    return;
                }

                for (int i = 0; i < skyboxSettings.skyboxShaderProperties.Count; i++)
                {
                    switch (skyboxSettings.skyboxShaderProperties[i].type)
                    {
                        case ShaderPropertyType.Float:
                        case ShaderPropertyType.Range:
                            RenderSettings.skybox.SetFloat(
                                skyboxSettings.skyboxShaderProperties[i].name,
                                skyboxSettings.skyboxShaderProperties[i].floatValue);
                            break;
                        case ShaderPropertyType.Color:
                            RenderSettings.skybox.SetColor(
                                skyboxSettings.skyboxShaderProperties[i].name,
                                skyboxSettings.skyboxShaderProperties[i].colorValue);
                            break;
                        case ShaderPropertyType.Texture:
                            RenderSettings.skybox.SetTexture(
                                skyboxSettings.skyboxShaderProperties[i].name,
                                skyboxSettings.skyboxShaderProperties[i].textureValue);
                            break;
                    }
                }

                RenderSettings.fog = fogSettings.enabled;
                RenderSettings.fogColor = fogSettings.fogColor;
                RenderSettings.fogDensity = fogSettings.fogDensity;

				RenderSettings.sun = environmentSettings.sun;
                RenderSettings.ambientMode = environmentSettings.source;
                RenderSettings.ambientIntensity = environmentSettings.intensityMultiplier;
                RenderSettings.ambientLight = environmentSettings.ambientColor;
                RenderSettings.ambientSkyColor = environmentSettings.skyColor;
                RenderSettings.ambientEquatorColor = environmentSettings.equatorColor;
                RenderSettings.ambientGroundColor = environmentSettings.groundColor;

#if BAKERY_INCLUDED
                if (skyboxSettings.bakerySkyLightsSettings != null)
                {
                    if (skyboxSettings.bakerySkyLightsSettings.bakerySky != null)
                    {
                        skyboxSettings.bakerySkyLightsSettings.bakerySky.texName = skyboxSettings.bakerySkyLightsSettings.texName;
                        skyboxSettings.bakerySkyLightsSettings.bakerySky.color = skyboxSettings.bakerySkyLightsSettings.color;
                        skyboxSettings.bakerySkyLightsSettings.bakerySky.intensity = skyboxSettings.bakerySkyLightsSettings.intensity;
                        skyboxSettings.bakerySkyLightsSettings.bakerySky.samples = skyboxSettings.bakerySkyLightsSettings.samples;
                        skyboxSettings.bakerySkyLightsSettings.bakerySky.hemispherical = skyboxSettings.bakerySkyLightsSettings.hemispherical;
                        skyboxSettings.bakerySkyLightsSettings.bakerySky.bitmask = skyboxSettings.bakerySkyLightsSettings.bitmask;
                        skyboxSettings.bakerySkyLightsSettings.bakerySky.bakeToIndirect = skyboxSettings.bakerySkyLightsSettings.bakeToIndirect;
                        skyboxSettings.bakerySkyLightsSettings.bakerySky.indirectIntensity = skyboxSettings.bakerySkyLightsSettings.indirectIntensity;
                        skyboxSettings.bakerySkyLightsSettings.bakerySky.tangentSH = skyboxSettings.bakerySkyLightsSettings.tangentSH;
                        skyboxSettings.bakerySkyLightsSettings.bakerySky.cubemap = skyboxSettings.bakerySkyLightsSettings.cubemap;
                        skyboxSettings.bakerySkyLightsSettings.bakerySky.correctRotation = skyboxSettings.bakerySkyLightsSettings.correctRotation;

                        //EditorUtility.SetDirty(skyboxSettings.bakerySkyLightsSettings.bakerySky);
                    }
                    else
                    {
                        skyboxSettings.bakerySkyLightsSettings = null;
                    }
                }

                BakeryLightMesh[] bakeryLightMeshes = FindObjectsOfType<BakeryLightMesh>();

                for (int i = 0; i < bakeryLightMeshes.Length; i++)
                {
                    LightSourceSettings.BakeryLightMeshesSettings currentSettings = bakeryLightMeshesSettings.Find(item => item.bakeryLightMesh == bakeryLightMeshes[i]);

                    if (currentSettings != null)
                    {
                        currentSettings.bakeryLightMesh.color = currentSettings.color;
                        currentSettings.bakeryLightMesh.intensity = currentSettings.intensity;
                        currentSettings.bakeryLightMesh.texture = currentSettings.texture;
                        currentSettings.bakeryLightMesh.cutoff = currentSettings.cutoff;
                        currentSettings.bakeryLightMesh.samples = currentSettings.samples;
                        currentSettings.bakeryLightMesh.samples2 = currentSettings.samples2;
                        currentSettings.bakeryLightMesh.bitmask = currentSettings.bitmask;
                        currentSettings.bakeryLightMesh.selfShadow = currentSettings.selfShadow;
                        currentSettings.bakeryLightMesh.bakeToIndirect = currentSettings.bakeToIndirect;
                        currentSettings.bakeryLightMesh.indirectIntensity = currentSettings.indirectIntensity;
                        currentSettings.bakeryLightMesh.lmid = currentSettings.lmid;

                        EditorUtility.SetDirty(currentSettings.bakeryLightMesh);
                    }
                }
#endif

                MLSCustomBlendable[] customBlendables = FindObjectsOfType<MLSCustomBlendable>();

                for (int i = 0; i < customBlendables.Length; i++)
                {
                    CustomBlendablesSettings currentSettings = customBlendablesSettings.Find(item => item.sourceScriptId == customBlendables[i].sourceScriptId);

                    if (currentSettings != null)
                    {
                        customBlendables[i].GetSharedParameters();

                        SerializedObject serializedObject = new SerializedObject(currentSettings.sourceScript);

                        for (int k = 0; k < currentSettings.blendableFloatParameters.Count; k++)
                        {
                            SerializedProperty floatProperty = serializedObject.FindProperty(currentSettings.blendableFloatParameters[k]);
                            floatProperty.floatValue = currentSettings.blendableFloatParametersValues[k];
                        }

                        for (int k = 0; k < currentSettings.blendableCubemapParameters.Count; k++)
                        {
                            SerializedProperty cubemapProperty = serializedObject.FindProperty(currentSettings.blendableCubemapParameters[k]);
                            cubemapProperty.objectReferenceValue = currentSettings.blendableCubemapParametersValues[k];
                        }

                        for (int k = 0; k < currentSettings.blendableColorParameters.Count; k++)
                        {
                            SerializedProperty colorProperty = serializedObject.FindProperty(currentSettings.blendableColorParameters[k]);
                            colorProperty.colorValue = currentSettings.blendableColorParametersValues[k];
                        }

                        for (int k = 0; k < currentSettings.blendableBoolParameters.Count; k++)
                        {
                            SerializedProperty boolProperty = serializedObject.FindProperty(currentSettings.blendableBoolParameters[k]);
                            boolProperty.boolValue = currentSettings.blendableBoolParametersValues[k];
                        }

                        serializedObject.ApplyModifiedProperties();
                    }
                }
            }

            public void UpdatePresetData()
            {
                Shader.SetGlobalInt("_MLS_ENABLE_LIGHTMAPS_BLENDING", 0);
                Shader.SetGlobalInt("_MLS_ENABLE_REFLECTIONS_BLENDING", 0);
                Shader.SetGlobalInt("_MLS_ENABLE_SKY_CUBEMAPS_BLENDING", 0);

                GameObject[] gameObjects = Resources.FindObjectsOfTypeAll<GameObject>();

                for (int i = 0; i < gameObjects.Length; i++)
                {
                    GameObjectSettings currentSettings = gameObjectsSettings.Find(item => item.gameObject == gameObjects[i]);

                    if (currentSettings != null)
                    {
                        currentSettings.enabled = gameObjects[i].activeSelf;
                        currentSettings.position = gameObjects[i].transform.localPosition;
                        currentSettings.rotation = gameObjects[i].transform.rotation;
                        currentSettings.tempRotation = TransformUtils.GetInspectorRotation(gameObjects[i].transform);

                        if (gameObjects[i].GetComponent<MeshRenderer>() == null)
                        {
                            continue;
                        }

                        if (gameObjects[i].GetComponent<MeshRenderer>().sharedMaterial != null &&
                            currentSettings.objectShaderProperties != null &&
                            currentSettings.propertyBlock != null)
                        {
                            for (int p = 0; p < currentSettings.objectShaderProperties.Count; p++)
                            {
                                if (currentSettings.objectShaderProperties[p].trackable)
                                {
                                    switch (currentSettings.objectShaderProperties[p].type)
                                    {
                                        case ShaderPropertyType.Float:
                                        case ShaderPropertyType.Range:
                                            currentSettings.objectShaderProperties[p].floatValue =
                                                currentSettings.propertyBlock.GetFloat(currentSettings.objectShaderProperties[p].name);
                                            break;
                                        case ShaderPropertyType.Color:
                                            currentSettings.objectShaderProperties[p].colorValue =
                                                currentSettings.propertyBlock.GetColor(currentSettings.objectShaderProperties[p].name);
                                            break;
                                        case ShaderPropertyType.Texture:
                                            currentSettings.objectShaderProperties[p].textureValue =
                                                currentSettings.propertyBlock.GetTexture(currentSettings.objectShaderProperties[p].name);
                                            break;
                                    }

                                    SceneView.RepaintAll();
                                }
                            }
                        }
                    }
                }

                Light[] sceneLights = FindObjectsOfType<Light>();

                for (int i = 0; i < sceneLights.Length; i++)
                {
                    LightSourceSettings currentSettings = lightSourceSettings.Find(item => item.light == sceneLights[i]);

                    if (currentSettings != null)
                    {
                        currentSettings.mlsLight.editedDirectly = false;
                        currentSettings.light = sceneLights[i];

                        if (sceneLights[i].transform.parent != null)
                        {
                            currentSettings.position = sceneLights[i].transform.localPosition;
                        }
                        else
                        {
                            currentSettings.position = sceneLights[i].transform.position;
                        }

                        currentSettings.rotation = TransformUtils.GetInspectorRotation(sceneLights[i].transform);
                        currentSettings.color = sceneLights[i].color;
                        currentSettings.colorTemperature = sceneLights[i].colorTemperature;
                        currentSettings.intensity = sceneLights[i].intensity;
                        currentSettings.range = sceneLights[i].range;
                        currentSettings.spotOuterAngle = sceneLights[i].spotAngle;
                        currentSettings.spotInnerAngle = sceneLights[i].innerSpotAngle;
                        currentSettings.areaWidth = sceneLights[i].areaSize.x;
                        currentSettings.areaHeight = sceneLights[i].areaSize.y;
                        currentSettings.indirectMultiplier = sceneLights[i].bounceIntensity;

#if MT_HDRP_7_INCLUDED || MT_HDRP_8_INCLUDED || MT_HDRP_9_INCLUDED || MT_HDRP_10_INCLUDED || MT_HDRP_11_INCLUDED || MT_HDRP_12_INCLUDED || MT_HDRP_13_INCLUDED || MT_HDRP_14_INCLUDED
                        currentSettings.intensity = sceneLights[i].GetComponent<HDAdditionalLightData>().intensity;
                        currentSettings.additionalLightData.type = sceneLights[i].GetComponent<HDAdditionalLightData>().type;
#else
                        currentSettings.intensity = sceneLights[i].intensity;
                        currentSettings.lightType = sceneLights[i].type;
                        currentSettings.bakedShadowsRadius = sceneLights[i].shadowRadius;
                        currentSettings.directionalBakedShadowAngle = sceneLights[i].shadowAngle;
                        currentSettings.shadowsType = sceneLights[i].shadows;
                        currentSettings.shadowStrength = sceneLights[i].shadowStrength;
                        currentSettings.lightCookie = sceneLights[i].cookie;
#endif

#if BAKERY_INCLUDED
                        if (currentSettings.bakeryDirectLightsSettings != null && currentSettings.bakeryDirectLightsSettings.bakeryDirect != null)
                        {
                            currentSettings.bakeryDirectLightsSettings.color = currentSettings.bakeryDirectLightsSettings.bakeryDirect.color;
                            currentSettings.bakeryDirectLightsSettings.intensity = currentSettings.bakeryDirectLightsSettings.bakeryDirect.intensity;
                            currentSettings.bakeryDirectLightsSettings.shadowSpread = currentSettings.bakeryDirectLightsSettings.bakeryDirect.shadowSpread;
                            currentSettings.bakeryDirectLightsSettings.samples = currentSettings.bakeryDirectLightsSettings.bakeryDirect.samples;
                            currentSettings.bakeryDirectLightsSettings.bitmask = currentSettings.bakeryDirectLightsSettings.bakeryDirect.bitmask;
                            currentSettings.bakeryDirectLightsSettings.bakeToIndirect = currentSettings.bakeryDirectLightsSettings.bakeryDirect.bakeToIndirect;
                            currentSettings.bakeryDirectLightsSettings.shadowmask = currentSettings.bakeryDirectLightsSettings.bakeryDirect.shadowmask;
                            currentSettings.bakeryDirectLightsSettings.shadowmaskDenoise = currentSettings.bakeryDirectLightsSettings.bakeryDirect.shadowmaskDenoise;
                            currentSettings.bakeryDirectLightsSettings.indirectIntensity = currentSettings.bakeryDirectLightsSettings.bakeryDirect.indirectIntensity;
                            currentSettings.bakeryDirectLightsSettings.cloudShadow = currentSettings.bakeryDirectLightsSettings.bakeryDirect.cloudShadow;
                            currentSettings.bakeryDirectLightsSettings.cloudShadowTilingX = currentSettings.bakeryDirectLightsSettings.bakeryDirect.cloudShadowTilingX;
                            currentSettings.bakeryDirectLightsSettings.cloudShadowTilingY = currentSettings.bakeryDirectLightsSettings.bakeryDirect.cloudShadowTilingY;
                            currentSettings.bakeryDirectLightsSettings.cloudShadowOffsetX = currentSettings.bakeryDirectLightsSettings.bakeryDirect.cloudShadowOffsetX;
                            currentSettings.bakeryDirectLightsSettings.cloudShadowOffsetY = currentSettings.bakeryDirectLightsSettings.bakeryDirect.cloudShadowOffsetY;
                        }

                        if (currentSettings.bakeryPointLightsSettings != null && currentSettings.bakeryPointLightsSettings.bakeryPoint != null)
                        {
                            currentSettings.bakeryPointLightsSettings.color = currentSettings.bakeryPointLightsSettings.bakeryPoint.color;
                            currentSettings.bakeryPointLightsSettings.intensity = currentSettings.bakeryPointLightsSettings.bakeryPoint.intensity;
                            currentSettings.bakeryPointLightsSettings.shadowSpread = currentSettings.bakeryPointLightsSettings.bakeryPoint.shadowSpread;
                            currentSettings.bakeryPointLightsSettings.cutoff = currentSettings.bakeryPointLightsSettings.bakeryPoint.cutoff;
                            currentSettings.bakeryPointLightsSettings.realisticFalloff = currentSettings.bakeryPointLightsSettings.bakeryPoint.realisticFalloff;
                            currentSettings.bakeryPointLightsSettings.samples = currentSettings.bakeryPointLightsSettings.bakeryPoint.samples;
                            currentSettings.bakeryPointLightsSettings.projMode = currentSettings.bakeryPointLightsSettings.bakeryPoint.projMode;
                            currentSettings.bakeryPointLightsSettings.cookie = currentSettings.bakeryPointLightsSettings.bakeryPoint.cookie;
                            currentSettings.bakeryPointLightsSettings.angle = currentSettings.bakeryPointLightsSettings.bakeryPoint.angle;
                            currentSettings.bakeryPointLightsSettings.innerAngle = currentSettings.bakeryPointLightsSettings.bakeryPoint.innerAngle;
                            currentSettings.bakeryPointLightsSettings.cubemap = currentSettings.bakeryPointLightsSettings.bakeryPoint.cubemap;
                            currentSettings.bakeryPointLightsSettings.iesFile = currentSettings.bakeryPointLightsSettings.bakeryPoint.iesFile;
                            currentSettings.bakeryPointLightsSettings.bitmask = currentSettings.bakeryPointLightsSettings.bakeryPoint.bitmask;
                            currentSettings.bakeryPointLightsSettings.bakeToIndirect = currentSettings.bakeryPointLightsSettings.bakeryPoint.bakeToIndirect;
                            currentSettings.bakeryPointLightsSettings.shadowmask = currentSettings.bakeryPointLightsSettings.bakeryPoint.shadowmask;
                            currentSettings.bakeryPointLightsSettings.indirectIntensity = currentSettings.bakeryPointLightsSettings.bakeryPoint.indirectIntensity;
                            currentSettings.bakeryPointLightsSettings.falloffMinRadius = currentSettings.bakeryPointLightsSettings.bakeryPoint.falloffMinRadius;
                        }

                        if (currentSettings.bakeryLightMeshesSettings != null && currentSettings.bakeryLightMeshesSettings.bakeryLightMesh != null)
                        {
                            currentSettings.bakeryLightMeshesSettings.color = currentSettings.bakeryLightMeshesSettings.bakeryLightMesh.color;
                            currentSettings.bakeryLightMeshesSettings.intensity = currentSettings.bakeryLightMeshesSettings.bakeryLightMesh.intensity;
                            currentSettings.bakeryLightMeshesSettings.texture = currentSettings.bakeryLightMeshesSettings.bakeryLightMesh.texture;
                            currentSettings.bakeryLightMeshesSettings.cutoff = currentSettings.bakeryLightMeshesSettings.bakeryLightMesh.cutoff;
                            currentSettings.bakeryLightMeshesSettings.samples = currentSettings.bakeryLightMeshesSettings.bakeryLightMesh.samples;
                            currentSettings.bakeryLightMeshesSettings.samples2 = currentSettings.bakeryLightMeshesSettings.bakeryLightMesh.samples2;
                            currentSettings.bakeryLightMeshesSettings.bitmask = currentSettings.bakeryLightMeshesSettings.bakeryLightMesh.bitmask;
                            currentSettings.bakeryLightMeshesSettings.selfShadow = currentSettings.bakeryLightMeshesSettings.bakeryLightMesh.selfShadow;
                            currentSettings.bakeryLightMeshesSettings.bakeToIndirect = currentSettings.bakeryLightMeshesSettings.bakeryLightMesh.bakeToIndirect;
                            currentSettings.bakeryLightMeshesSettings.indirectIntensity = currentSettings.bakeryLightMeshesSettings.bakeryLightMesh.indirectIntensity;
                            currentSettings.bakeryLightMeshesSettings.lmid = currentSettings.bakeryLightMeshesSettings.bakeryLightMesh.lmid;
                        }
#endif

                        sceneLights[i].GetComponent<MLSLight>().lastEditedBy = name;
                    }
                }

                if (RenderSettings.skybox != null && skyboxSettings.skyboxShaderProperties != null)
                {
                    for (int i = 0; i < skyboxSettings.skyboxShaderProperties.Count; i++)
                    {
                        switch (skyboxSettings.skyboxShaderProperties[i].type)
                        {
                            case ShaderPropertyType.Float:
                            case ShaderPropertyType.Range:
                                skyboxSettings.skyboxShaderProperties[i].floatValue =
                                    RenderSettings.skybox.GetFloat(skyboxSettings.skyboxShaderProperties[i].name);
                                break;
                            case ShaderPropertyType.Color:
                                skyboxSettings.skyboxShaderProperties[i].colorValue =
                                    RenderSettings.skybox.GetColor(skyboxSettings.skyboxShaderProperties[i].name);
                                break;
                            case ShaderPropertyType.Texture:
                                skyboxSettings.skyboxShaderProperties[i].textureValue =
                                    RenderSettings.skybox.GetTexture(skyboxSettings.skyboxShaderProperties[i].name);
                                break;
                        }
                    }
                }

                fogSettings.enabled = RenderSettings.fog;
                fogSettings.fogColor = RenderSettings.fogColor;
                fogSettings.fogDensity = RenderSettings.fogDensity;

				environmentSettings.sun = RenderSettings.sun;
                environmentSettings.source = RenderSettings.ambientMode;
                environmentSettings.intensityMultiplier = RenderSettings.ambientIntensity;
                environmentSettings.ambientColor = RenderSettings.ambientLight;
                environmentSettings.skyColor = RenderSettings.ambientSkyColor;
                environmentSettings.equatorColor = RenderSettings.ambientEquatorColor;
                environmentSettings.groundColor = RenderSettings.ambientGroundColor;

#if BAKERY_INCLUDED
                if (skyboxSettings.bakerySkyLightsSettings != null && skyboxSettings.bakerySkyLightsSettings.bakerySky != null)
                {
                    skyboxSettings.bakerySkyLightsSettings.texName = skyboxSettings.bakerySkyLightsSettings.bakerySky.texName;
                    skyboxSettings.bakerySkyLightsSettings.color = skyboxSettings.bakerySkyLightsSettings.bakerySky.color;
                    skyboxSettings.bakerySkyLightsSettings.intensity = skyboxSettings.bakerySkyLightsSettings.bakerySky.intensity;
                    skyboxSettings.bakerySkyLightsSettings.samples = skyboxSettings.bakerySkyLightsSettings.bakerySky.samples;
                    skyboxSettings.bakerySkyLightsSettings.hemispherical = skyboxSettings.bakerySkyLightsSettings.bakerySky.hemispherical;
                    skyboxSettings.bakerySkyLightsSettings.bitmask = skyboxSettings.bakerySkyLightsSettings.bakerySky.bitmask;
                    skyboxSettings.bakerySkyLightsSettings.bakeToIndirect = skyboxSettings.bakerySkyLightsSettings.bakerySky.bakeToIndirect;
                    skyboxSettings.bakerySkyLightsSettings.indirectIntensity = skyboxSettings.bakerySkyLightsSettings.bakerySky.indirectIntensity;
                    skyboxSettings.bakerySkyLightsSettings.tangentSH = skyboxSettings.bakerySkyLightsSettings.bakerySky.tangentSH;
                    skyboxSettings.bakerySkyLightsSettings.cubemap = skyboxSettings.bakerySkyLightsSettings.bakerySky.cubemap;
                }

                BakeryLightMesh[] bakeryLightMeshes = FindObjectsOfType<BakeryLightMesh>();

                for (int i = 0; i < bakeryLightMeshes.Length; i++)
                {
                    LightSourceSettings.BakeryLightMeshesSettings currentSettings = bakeryLightMeshesSettings.Find(item => item.bakeryLightMesh == bakeryLightMeshes[i]);

                    if (currentSettings != null)
                    {
                        currentSettings.color = currentSettings.bakeryLightMesh.color;
                        currentSettings.intensity = currentSettings.bakeryLightMesh.intensity;
                        currentSettings.texture = currentSettings.bakeryLightMesh.texture;
                        currentSettings.cutoff = currentSettings.bakeryLightMesh.cutoff;
                        currentSettings.samples = currentSettings.bakeryLightMesh.samples;
                        currentSettings.samples2 = currentSettings.bakeryLightMesh.samples2;
                        currentSettings.bitmask = currentSettings.bakeryLightMesh.bitmask;
                        currentSettings.selfShadow = currentSettings.bakeryLightMesh.selfShadow;
                        currentSettings.bakeToIndirect = currentSettings.bakeryLightMesh.bakeToIndirect;
                        currentSettings.indirectIntensity = currentSettings.bakeryLightMesh.indirectIntensity;
                        currentSettings.lmid = currentSettings.bakeryLightMesh.lmid;
                    }
                }
#endif

                MLSCustomBlendable[] customBlendables = FindObjectsOfType<MLSCustomBlendable>();

                for (int i = 0; i < customBlendables.Length; i++)
                {
                    CustomBlendablesSettings currentSettings = customBlendablesSettings.Find(item => item.sourceScriptId == customBlendables[i].sourceScriptId);

                    if (currentSettings != null)
                    {
                        customBlendables[i].GetSharedParameters();

                        for (int j = 0; j < customBlendables[i].blendableFloatFields.Count; j++)
                        {
                            for (int k = 0; k < currentSettings.blendableFloatParameters.Count; k++)
                            {
                                if (customBlendables[i].blendableFloatFields[j].Name == currentSettings.blendableFloatParameters[k])
                                {
                                    currentSettings.blendableFloatParametersValues[k] = (float) customBlendables[i].blendableFloatFields[k].GetValue(customBlendables[i]);
                                }
                            }
                        }

                        for (int j = 0; j < customBlendables[i].blendableCubemapFields.Count; j++)
                        {
                            for (int k = 0; k < currentSettings.blendableCubemapParameters.Count; k++)
                            {
                                if (customBlendables[i].blendableCubemapFields[j].Name == currentSettings.blendableCubemapParameters[k])
                                {
                                    currentSettings.blendableCubemapParametersValues[k] = (Cubemap) customBlendables[i].blendableCubemapFields[k].GetValue(customBlendables[i]);
                                }
                            }
                        }

                        for (int j = 0; j < customBlendables[i].blendableColorFields.Count; j++)
                        {
                            for (int k = 0; k < currentSettings.blendableColorParameters.Count; k++)
                            {
                                if (customBlendables[i].blendableColorFields[j].Name == currentSettings.blendableColorParameters[k])
                                {
                                    currentSettings.blendableColorParametersValues[k] = (Color) customBlendables[i].blendableColorFields[k].GetValue(customBlendables[i]);
                                }
                            }
                        }

                        for (int j = 0; j < customBlendables[i].blendableBoolFields.Count; j++)
                        {
                            for (int k = 0; k < currentSettings.blendableBoolParameters.Count; k++)
                            {
                                if (customBlendables[i].blendableBoolFields[j].Name == currentSettings.blendableBoolParameters[k])
                                {
                                    currentSettings.blendableBoolParametersValues[k] = (bool) customBlendables[i].blendableBoolFields[k].GetValue(customBlendables[i]);
                                }
                            }
                        }
                    }
                }
            }
        }

        public List<SceneLightingPreset> lightingPresets = new List<SceneLightingPreset>();
        public List<string> presetNames = new List<string>();
        public StoringMode storingMode;
        public bool deferredRenderingWarning;
        public List<float> sizeGroups = new List<float>();
        public List<float> distanceGroups = new List<float>();
        public List<GameObject> appropriateObjects = new List<GameObject>();
        #endregion

        public static bool CheckIfStatic(GameObject gameObject)
        {
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(gameObject);
            bool isStatic = false;

#if UNITY_2019_2_OR_NEWER
            if ((flags & StaticEditorFlags.ContributeGI) != 0)
            {
                isStatic = true;
            }
#else
            if ((flags & StaticEditorFlags.LightmapStatic) != 0)
            {
                isStatic = true;
            }
#endif

            return isStatic;
        }
#endif

        public void OnSceneLoadComplete(string targetScene)
        {
            stopProbesBlending = false;
            UpdateStoredArray(targetScene);
        }

        public void OnSceneUnloadComplete(string sceneToUnload)
        {
            //StartCoroutine(UpdateStoredArray(sceneToUnload, true));
        }

        public List<StoredLightmapData> DeserializeStoredData(string targetSceneName)
        {
            List<StoredLightmapData> tempLightmapDataList = new List<StoredLightmapData>();
            List<StoredLightmapData> exitLightmapDataList = new List<StoredLightmapData>();

            if (loadFromAssetBundles)
            {
                /*
                 * Use your own code here to load the assets from the AssetBundle. 
                 * Load Data Type: List<StoredLightmapData>.
                 * 
                 * tempLightmapDataList = AssetBundleData;
                 */

                Debug.LogFormat("<color=cyan>MLS:</color> Use your own code to load \"Stored Lightmap Data\" here.");

                if (tempLightmapDataList == null || tempLightmapDataList.Count == 0)
                {
                    return null;
                }
            }
            else
            {
                #if UNITY_EDITOR
                if (!Directory.Exists(currentDataPath))
                {
                    return null;
                }

                string[] storedLightmapDataPaths = Directory.GetFiles(currentDataPath);

                for (int i = 0; i < storedLightmapDataPaths.Length; i++)
                {
                    if (!storedLightmapDataPaths[i].EndsWith("meta", System.StringComparison.Ordinal))
                    {
                        StoredLightmapData data = AssetDatabase.LoadAssetAtPath<StoredLightmapData>(storedLightmapDataPaths[i]);

                        if (data != null)
                        {
                            tempLightmapDataList.Add(data);
                        }
                    }
                }
                #else
                tempLightmapDataList = sceneLightmapDatas;
                #endif
            }

            if (tempLightmapDataList.Count > 0)
            {
                for (int i = 0; i < tempLightmapDataList.Count; i++)
                {
                    if (tempLightmapDataList[i].workflow == workflow)
                    {
                        #if BAKERY_INCLUDED
                        if (lightmapper == Lightmapper.BakeryLightmapper)
                        {
                            tempLightmapDataList[i].bakeryVolumeDataDeserialized = new Hashtable();

                            for (int j = 0;
                                j < tempLightmapDataList[i].sceneLightingData.bakeryVolumes.name.Length;
                                j++)
                            {
                                try
                                {
                                    List<Texture3D> volumeTextures = new List<Texture3D>();

                                    volumeTextures.Add(tempLightmapDataList[i].sceneLightingData.bakeryVolumes
                                        .volumeTexture0[j]);
                                    volumeTextures.Add(tempLightmapDataList[i].sceneLightingData.bakeryVolumes
                                        .volumeTexture1[j]);
                                    volumeTextures.Add(tempLightmapDataList[i].sceneLightingData.bakeryVolumes
                                        .volumeTexture2[j]);

                                    if (tempLightmapDataList[i].sceneLightingData.bakeryVolumes.volumeTexture3.Length >
                                        0)
                                    {
                                        volumeTextures.Add(tempLightmapDataList[i].sceneLightingData.bakeryVolumes
                                            .volumeTexture3[j]);
                                    }
                                    else
                                    {
                                        volumeTextures.Add(new Texture3D(1, 1, 1, TextureFormat.RGBAHalf, false));
                                    }

                                    if (tempLightmapDataList[i].sceneLightingData.bakeryVolumes.volumeTexture4.Length >
                                        0)
                                    {
                                        volumeTextures.Add(tempLightmapDataList[i].sceneLightingData.bakeryVolumes
                                            .volumeTexture4[j]);
                                    }
                                    else
                                    {
                                        volumeTextures.Add(new Texture3D(1, 1, 1, TextureFormat.RGBAHalf, false));
                                    }

                                    tempLightmapDataList[i].bakeryVolumeDataDeserialized.Add(
                                        tempLightmapDataList[i].sceneLightingData.bakeryVolumes.name[j],
                                        volumeTextures);
                                }
                                catch (System.Exception ae)
                                {
                                    Debug.Log("The probability of this event is extremely small, " +
                                              "however, if it did happen, please report it to the developer.\r\n" +
                                              ae.ToString());
                                }
                            }
                        }
#endif
                        
                        tempLightmapDataList[i].storedReflectionProbeDataDeserialized = new Hashtable();

                        if (tempLightmapDataList[i].sceneLightingData.reflectionProbes.name != null)
                        {
                            for (int j = 0; j < tempLightmapDataList[i].sceneLightingData.reflectionProbes.name.Length; j++)
                            {
                                try
                                {
                                    tempLightmapDataList[i].storedReflectionProbeDataDeserialized.Add(
                                        tempLightmapDataList[i].sceneLightingData.reflectionProbes.name[j],
                                        tempLightmapDataList[i].sceneLightingData.reflectionProbes.cubeReflectionTexture[j]);
                                }
                                catch (System.Exception ae)
                                {
                                    Debug.Log("The probability of this event is extremely small, " +
                                        "however, if it did happen, please report it to the developer.\r\n" +
                                        ae.ToString());
                                }
                            }
                        }

                        tempLightmapDataList[i].rendererDataDeserialized = new Hashtable();

                        for (int j = 0; j < tempLightmapDataList[i].sceneLightingData.rendererDatas.Length; j++)
                        {
                            try
                            {
                                tempLightmapDataList[i].rendererDataDeserialized.Add(
                                    tempLightmapDataList[i].sceneLightingData.rendererDatas[j].objectId,
                                    tempLightmapDataList[i].sceneLightingData.rendererDatas[j]);
                            }
                            catch (System.Exception ae)
                            {
                                Debug.Log("The probability of this event is extremely small, " +
                                    "however, if it did happen, please report it to the developer.\r\n" +
                                    ae.ToString());
                            }
                        }

                        tempLightmapDataList[i].terrainDataDeserialized = new Hashtable();

                        for (int j = 0; j < tempLightmapDataList[i].sceneLightingData.terrainDatas.Length; j++)
                        {
                            try
                            {
                                tempLightmapDataList[i].terrainDataDeserialized.Add(
                                    tempLightmapDataList[i].sceneLightingData.terrainDatas[j].objectId,
                                    tempLightmapDataList[i].sceneLightingData.terrainDatas[j]);
                            }
                            catch (System.Exception ae)
                            {
                                Debug.Log("The probability of this event is extremely small, " +
                                    "however, if it did happen, please report it to the developer.\r\n" +
                                    ae.ToString());
                            }
                        }

                        tempLightmapDataList[i].lightSourceDataDeserialized = new Hashtable();

                        for (int j = 0; j < tempLightmapDataList[i].sceneLightingData.lightSourceDatas.Length; j++)
                        {
                            try
                            {
                                tempLightmapDataList[i].lightSourceDataDeserialized.Add(
                                    tempLightmapDataList[i].sceneLightingData.lightSourceDatas[j].lightUID,
                                    tempLightmapDataList[i].sceneLightingData.lightSourceDatas[j]);
                            }
                            catch (System.Exception ae)
                            {
                                Debug.Log("The probability of this event is extremely small, " +
                                    "however, if it did happen, please report it to the developer.\r\n" +
                                    ae.ToString());
                            }
                        }

                        exitLightmapDataList.Add(tempLightmapDataList[i]);
                    }
                }
            }

            return exitLightmapDataList;
        }

        public List<StoredLightingScenario> UpdateLightingScenarios(string targetSceneName, bool forceUpdateStoredData = false)
        {
            List<StoredLightingScenario> tempLightmapScenariosList = new List<StoredLightingScenario>();
            List<StoredLightingScenario> exitLightmapScenariosList = new List<StoredLightingScenario>();
            Blending.BlendingOperationalData blendingOperationalData;

            if (loadFromAssetBundles)
            {
                /*
                 * Use your own code here to load the assets from the AssetBundle. 
                 * Load Data Type: List<StoredLightingScenario>
                 * 
                 * tempLightmapScenariosList = AssetBundleData;
                 */

                Debug.LogFormat("<color=cyan>MLS:</color> Use your own code to load \"Lightmap Scenarios\" here.");

                if (tempLightmapScenariosList == null || tempLightmapScenariosList.Count == 0)
                {
                    return null;
                }
            }
            else
            {
                #if UNITY_EDITOR
                if (!Directory.Exists(currentDataPath))
                {
                    return null;
                }

                string[] storedLightmapScenariosPaths = Directory.GetFiles(currentDataPath);

                for (int i = 0; i < storedLightmapScenariosPaths.Length; i++)
                {
                    if (!storedLightmapScenariosPaths[i].EndsWith("meta", System.StringComparison.Ordinal))
                    {
                        StoredLightingScenario scenario = AssetDatabase.LoadAssetAtPath<StoredLightingScenario>(storedLightmapScenariosPaths[i]);

                        if (scenario != null)
                        {
                            tempLightmapScenariosList.Add(scenario);
                        }
                    }
                }
                #else
                tempLightmapScenariosList = sceneLightmapScenarios;
                #endif
            }

            if (tempLightmapScenariosList.Count > 0)
            {                
                Blending.blendingOperationalDatas.TryGetValue(targetSceneName, out blendingOperationalData);
                
                for (int i = 0; i < tempLightmapScenariosList.Count; i++)
                {
                    // if (!OnBlendingValueChanged.Contains(tempLightmapScenariosList[i].OnBlendingValueChanged))
                    // {
                    //     OnBlendingValueChanged.Add(tempLightmapScenariosList[i].OnBlendingValueChanged);
                    // }
                    //
                    // if (!OnLoadedLightmapChanged.Contains(tempLightmapScenariosList[i].OnLoadedLightmapChanged))
                    // {
                    //     OnLoadedLightmapChanged.Add(tempLightmapScenariosList[i].OnLoadedLightmapChanged);
                    // }
                    
                    if (tempLightmapScenariosList[i].workflow == workflow)
                    {
                        tempLightmapScenariosList[i].eventsListId = OnBlendingValueChanged.Count - 1;
                        
                        if (tempLightmapScenariosList[i].blendableLightmaps.Count > 0)
                        {
                            if (LightmapSettings.lightProbes != null)
                            {
                                switch (workflow)
                                {
                                    case Workflow.MultiScene:
#if !UNITY_2020_1_OR_NEWER
                                        //if (blendingOperationalData.loadIndex > 0)
                                        //{
                                        //    if (blendingOperationalData.lightProbesArrayIndex == 0)
                                        //    {
                                        //        blendingOperationalData.lightProbesArrayIndex = LightmapSettings.lightProbes.bakedProbes.Length - tempLightmapScenariosList[i].blendableLightmaps[0].lightingData.sceneLightingData.lightProbes.Length;
                                        //        tempLightmapScenariosList[i].lightProbesArrayPosition = blendingOperationalData.lightProbesArrayIndex;
                                        //    }
                                        //    else
                                        //    {
                                        //        tempLightmapScenariosList[i].lightProbesArrayPosition = blendingOperationalData.lightProbesArrayIndex;
                                        //    }
                                        //}
                                        //else
                                        {
                                            tempLightmapScenariosList[i].lightProbesArrayPosition = 0;
                                        }
#endif
                                        break;
                                    case Workflow.SingleScene:
                                        tempLightmapScenariosList[i].lightProbesArrayPosition = 0;
                                        break;
                                }
                                
                            }
                        }

                        exitLightmapScenariosList.Add(tempLightmapScenariosList[i]);
                    }
                }

                availableScenarios = exitLightmapScenariosList;
            }

            return exitLightmapScenariosList;
        }

        private List<ReflectionProbe> UpdateReflectionProbes(string targetScene)
        {
#if UNITY_6000_0_OR_NEWER
            ReflectionProbe[] reflectionProbes = FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.InstanceID);
#else
            ReflectionProbe[] reflectionProbes = FindObjectsOfType<ReflectionProbe>();
#endif

            List<ReflectionProbe> resultProbesList = new List<ReflectionProbe>();

            for (int i = 0; i < reflectionProbes.Length; i++)
            {
                if (reflectionProbes[i].gameObject.scene.name == targetScene)
                {
                    resultProbesList.Add(reflectionProbes[i]);
                    sceneReflectionProbePositions.Add(reflectionProbes[i].transform.position);
                }
            }

            return resultProbesList;
        }

        private List<MLSLight> UpdateLights(string targetScene)
        {
#if UNITY_6000_0_OR_NEWER
            MLSLight[] lights = FindObjectsByType<MLSLight>(FindObjectsSortMode.InstanceID);
#else
            MLSLight[] lights = FindObjectsOfType<MLSLight>();
#endif

            List<MLSLight> resultLightsList = new List<MLSLight>();

            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].gameObject.scene.name == targetScene)
                {
                    resultLightsList.Add(lights[i]);
                }
            }

            return resultLightsList;
        }

        public async void UpdateStoredArray(string targetScene, bool forceUpdateStoredData = false)
        {
            if (storedDataUpdatingProcess)
            {
                return;
            }

            storedDataUpdatingProcess = true;
            storedDataUpdated = false;

#if UNITY_EDITOR
            FindSystemProperies();
#endif

            if (systemProperties == null)
            {
                Debug.LogFormat("<color=cyan>MLS:</color> " +
                    "The \"System Properties\" file was not found. " +
                    "Go to Tools->Magic Tools->Magic Lightmap Switcher->Prepare Shaders menu item. " +
                    "The file will be created automatically.");

                return;
            }

#if UNITY_EDITOR
#if BAKERY_INCLUDED
            if (Lightmapping.isRunning || ftRenderLightmap.bakeInProgress)
            {
#else
            if (Lightmapping.isRunning)
            {
#endif
                storedDataUpdatingProcess = false;
                forceUpdateStoredData = true;
                return;
            }
#endif

            if (lastSceneCount != SceneManager.sceneCount || forceUpdateStoredData)
            {
                if (string.IsNullOrEmpty(targetScene))
                {
                    storedDataUpdatingProcess = false;
                    return;
                }

                bool removeOrAdd = false || lastSceneCount < SceneManager.sceneCount;

                lastSceneCount = SceneManager.sceneCount;  

                switch (workflow)
                {
                    case Workflow.MultiScene:
                        if (removeOrAdd || forceUpdateStoredData)
                        {
#if !UNITY_2020_1_OR_NEWER
                            Blending.UpdateBlendingOperationalData(targetScene);
#endif

                            if (!storedLightmapDatas.ContainsKey(targetScene))
                            {
                                storedLightmapDatas.Add(targetScene, DeserializeStoredData(targetScene));
                            }
                            else
                            {
                                storedLightmapDatas[targetScene] = DeserializeStoredData(targetScene);

                                if (storedLightmapDatas[targetScene] != null)
                                {
                                    if (storedLightmapDatas[targetScene].Count == 0)
                                    {
                                        storedLightmapDatas.Remove(targetScene);
                                    }
                                }
                            }

                            if (!storedLightmapScenarios.ContainsKey(targetScene))
                            {
                                storedLightmapScenarios.Add(targetScene, UpdateLightingScenarios(targetScene, forceUpdateStoredData));
                            }
                            else
                            {
                                storedLightmapScenarios[targetScene] = UpdateLightingScenarios(targetScene, forceUpdateStoredData);

                                if (storedLightmapScenarios[targetScene] != null)
                                {
                                    if (storedLightmapScenarios[targetScene].Count == 0)
                                    {
                                        storedLightmapScenarios.Remove(targetScene);
                                    }
                                }
                            }

                            if (!storedReflectionProbes.ContainsKey(targetScene))
                            {
                                storedReflectionProbes.Add(targetScene, UpdateReflectionProbes(targetScene));
                            }
                            else
                            {
                                storedReflectionProbes[targetScene] = UpdateReflectionProbes(targetScene);
                            }

                            if (!storedLights.ContainsKey(targetScene))
                            {
                                storedLights.Add(targetScene, UpdateLights(targetScene));
                            }
                            else
                            {
                                storedLights[targetScene] = UpdateLights(targetScene);
                            }
                        }
                        else
                        {
                            storedLightmapScenarios.TryGetValue(targetScene, out List<StoredLightingScenario> sourceScenariosSet);

                            if (storedLightmapDatas.ContainsKey(targetScene))
                            {
                                storedLightmapDatas.Remove(targetScene);
                            }

                            if (storedLightmapScenarios.ContainsKey(targetScene))
                            {
                                storedLightmapScenarios.Remove(targetScene);
                            }                            

                            if (storedReflectionProbes.ContainsKey(targetScene))
                            {
                                 storedReflectionProbes.Remove(targetScene);
                            }

                            if (storedLights.ContainsKey(targetScene))
                            {
                                storedLights.Remove(targetScene);
                            }
                        }
                        break;
                    case Workflow.SingleScene:
                        sceneLightmapDatas = DeserializeStoredData(targetScene);
                        sceneLightmapScenarios = UpdateLightingScenarios(targetScene);
                        sceneAffectedLightSources = new List<MLSLight>(FindObjectsOfType<MLSLight>());
                        sceneReflectionProbes = UpdateReflectionProbes(targetScene);
                        break;
                }
            }

            if (loadFromAssetBundles)
            {
                var notificationsIsShown = false;

                while (sceneLightmapDatas == null || sceneLightmapScenarios == null)
                {
                    if (!notificationsIsShown)
                    {
                        Debug.LogFormat("<color=cyan>MLS:</color> Stored Data Array has not been updated.");
                        notificationsIsShown = true;
                    }
                    
                    storedDataUpdatingProcess = false;
                    await Task.Yield();
                }
            }
            else
            {
                if (sceneLightmapDatas == null || sceneLightmapScenarios == null)
                {
                    Debug.LogFormat("<color=cyan>MLS:</color> Stored Data Array has not been updated.");
                    storedDataUpdatingProcess = false;
					SetBlendingOptionsGlobal(BlendingOptions.None);
                    return;
                }
            }

            if (sceneLightmapScenarios.Count > 0)
            {
                currentLightmapScenario = sceneLightmapScenarios[0];
                currentLightmapScenario.SynchronizeCustomBlendableData(true);
            }
            else
            {
                //yield break;
            }

#if UNITY_2020_1_OR_NEWER
            Blending.UpdateBlendingOperationalData(targetScene);
#endif

            forceUpdateStoredData = false;

            ConfigureAffectedObjects(targetScene);

            if (workflow == Workflow.MultiScene)
            {
                staticAffectedObjects.TryGetValue(targetScene, out Blending._resultStaticAffectedObjects);
                dynamicAffectedObjects.TryGetValue(targetScene, out Blending._resultDynamicAffectedObjects);
                storedLights.TryGetValue(targetScene, out Blending._resultAffectedLights);
            }
            else
            {
                Blending._affectedTerrains = affectedTerrains;
                Blending._resultStaticAffectedObjects = sceneStaticAffectedObjects;
                Blending._resultDynamicAffectedObjects = sceneDynamicAffectedObjects;
                Blending._resultAffectedLights = sceneAffectedLightSources;
                Blending._mainDirectionalLight = sceneAffectedLightSources.Find(item => item.sourceLight.type == LightType.Directional);
            }

            if (useTextureArrays && !globalDataArraysInitialized)
            {
                ConfigureGlobalDataArrays(targetScene);
            }

            storedDataUpdated = true;
            storedDataUpdatingProcess = false;
            SceneManagment.sceneProcessing = false;

            if (workflow == Workflow.MultiScene)
            {
                LightProbes.TetrahedralizeAsync();
            }

#if UNITY_EDITOR
            SetBlendingOptionsGlobal(MagicLightmapSwitcher.BlendingOptions.All);

            await Task.Yield();

            if (currentLightmapScenario != null)
            {
                if (currentLightmapScenario.blendableLightmaps != null && currentLightmapScenario.blendableLightmaps.Count > 0)
                {
                    Blending.Blend(this, 0, currentLightmapScenario);
                }
            }
            else if (availableScenarios.Count > 0)
            {
                if (availableScenarios[0].blendableLightmaps != null && availableScenarios[0].blendableLightmaps.Count > 0)
                {
                    Blending.Blend(this, 0, availableScenarios[0]);
                }
            }
#endif
        }

        private void ConfigureGlobalDataArrays(string targetScene)
        {
            if (sceneLightmapDatas == null || sceneLightmapDatas.Count == 0)
            {
                return;
            }

            Blending.InitiShaderProperties();

            #region Reflection Probes Array
            if (currentLightmapScenario.blendableLightmaps[0].lightingData
                .sceneLightingData.reflectionProbes.cubeReflectionTexture != null &&
                currentLightmapScenario.blendableLightmaps[0].lightingData
                .sceneLightingData.reflectionProbes.cubeReflectionTexture.Length > 0)
            {
                Cubemap referenceCubemap = currentLightmapScenario.blendableLightmaps[0].lightingData
                                .sceneLightingData.reflectionProbes.cubeReflectionTexture[0];

                Blending._cubemapWidth = referenceCubemap.width;
                Blending._cubemapCount = currentLightmapScenario.blendableLightmaps[0].lightingData.storedReflectionProbeDataDeserialized.Count;
                Blending._cubemapFormat = referenceCubemap.graphicsFormat;
                Blending._cubemapArray = new CubemapArray(
                    Blending._cubemapWidth,
                    currentLightmapScenario.blendableLightmaps.Count * Blending._cubemapCount,
                    referenceCubemap.format,
                    true);
                Blending._reflectionProbePositionsArray = new Vector4[currentLightmapScenario.blendableLightmaps.Count * Blending._cubemapCount];

                int globalCounter = 0;
                int localCounter = 0;
                int totalCounter = 0;

                for (int i = 0; i < currentLightmapScenario.blendableLightmaps.Count; i++)
                {
                    for (int j = 0; j < Blending._cubemapCount; j++)
                    {
                        Cubemap cubemap = currentLightmapScenario.blendableLightmaps[i].lightingData
                                        .sceneLightingData.reflectionProbes.cubeReflectionTexture[j];

                        for (int k = 0; k < 6; k++)
                        {
                            localCounter = 6 * j + k;
                            Graphics.CopyTexture(cubemap, k, Blending._cubemapArray, globalCounter + localCounter);
                        }

                        ReflectionProbe rp = GameObject.Find(
                                        currentLightmapScenario.blendableLightmaps[i].lightingData.sceneLightingData.reflectionProbes.name[j]).
                                        GetComponent<ReflectionProbe>();

                        Blending._reflectionProbePositionsArray[totalCounter] = new Vector4(
                            rp.transform.position.x,
                            rp.transform.position.y,
                            rp.transform.position.z, totalCounter);

                        totalCounter++;
                    }

                    globalCounter += localCounter + 1;
                }

                cubemapArrayInitialized = true;

                Shader.SetGlobalVectorArray(Blending._MLS_Reflection_Probe_Positions, Blending._reflectionProbePositionsArray);
            }

            Shader.EnableKeyword("MLS_REFLECTIONS_BLENDING_ON");
            Shader.SetGlobalInt(Blending._MLS_Reflection_Probe_Count, Blending._cubemapCount);
            Shader.SetGlobalTexture(Blending._MLS_Cubemap_Array, Blending._cubemapArray);
            #endregion

            #region Lightmaps Array
            Blending._lightmapCount = currentLightmapScenario.blendableLightmaps[0].lightingData.sceneLightingData.lightmapsLight.Length;
            Blending._arrayDepth = currentLightmapScenario.blendableLightmaps.Count * Blending._lightmapCount;

            var hasColorLightmaps = currentLightmapScenario.blendableLightmaps[0].lightingData.sceneLightingData.lightmapsLight[0] == null ? false : true;
            var hasDirectionalLightmaps = currentLightmapScenario.blendableLightmaps[0].lightingData.sceneLightingData.lightmapsDirectional[0] == null ? false : true;
            var hasShadowmasks = currentLightmapScenario.blendableLightmaps[0].lightingData.sceneLightingData.lightmapsShadowmask[0] == null ? false : true;

            Shader.SetGlobalInt(Blending._MLS_Lightmaps_Count, Blending._lightmapCount);

            Blending._lightmapLightArray = new Texture2DArray[9];
            Blending._lightmapDirArray = new Texture2DArray[9];
            Blending._lightmapShadowMaskArray = new Texture2DArray[9];

            var resolution = 32;

            for (int i = 0; i < 9; i++)
            {
                if (hasColorLightmaps)
                {
                    var _colorFormat = currentLightmapScenario.blendableLightmaps[0].lightingData.sceneLightingData
                        .lightmapsLight[0].format;

                    var _graphicsFormat = currentLightmapScenario.blendableLightmaps[0].lightingData.sceneLightingData
                        .lightmapsLight[0].graphicsFormat;

                    var isSRGB = GraphicsFormatUtility.IsSRGBFormat(_graphicsFormat);

                    Blending._lightmapLightArray[i] = new Texture2DArray(
                        resolution, resolution, Blending._arrayDepth, _colorFormat, false, !isSRGB);

                    // Blending._lightmapLightArray[i] = new Texture2DArray(
                    //     resolution, resolution, Blending._arrayDepth, _colorFormat, TextureCreationFlags.None);
                }

                if (hasDirectionalLightmaps)
                {
                    var _directionalFormat = currentLightmapScenario.blendableLightmaps[0].lightingData.sceneLightingData
                        .lightmapsDirectional[0].format;

                    var _graphicsFormat = currentLightmapScenario.blendableLightmaps[0].lightingData.sceneLightingData
                        .lightmapsDirectional[0].graphicsFormat;

                    var isSRGB = GraphicsFormatUtility.IsSRGBFormat(_graphicsFormat);

                    Blending._lightmapDirArray[i] = new Texture2DArray(
                        resolution, resolution, Blending._arrayDepth, _directionalFormat, false, !isSRGB);

                    // Blending._lightmapDirArray[i] = new Texture2DArray(
                    //     resolution, resolution, Blending._arrayDepth, _directionalFormat, TextureCreationFlags.None);
                }

                if (hasShadowmasks)
                {
                    var _shadowmaskFormat = currentLightmapScenario.blendableLightmaps[0].lightingData.sceneLightingData
                        .lightmapsShadowmask[0].format;

                    var _graphicsFormat = currentLightmapScenario.blendableLightmaps[0].lightingData.sceneLightingData
                        .lightmapsShadowmask[0].graphicsFormat;

                    var isSRGB = GraphicsFormatUtility.IsSRGBFormat(_graphicsFormat);

                    Blending._lightmapShadowMaskArray[i] = new Texture2DArray(
                        resolution, resolution, Blending._arrayDepth, _shadowmaskFormat, false, !isSRGB);

                    // Blending._lightmapShadowMaskArray[i] = new Texture2DArray(
                    //     resolution, resolution, Blending._arrayDepth, _graphicsFormat, TextureCreationFlags.None);
                }

                resolution *= 2;
            }

            for (int i = 0; i < currentLightmapScenario.blendableLightmaps.Count; i++)
            {
                if (hasColorLightmaps)
                {
                    for (int j = 0;
                        j < currentLightmapScenario.blendableLightmaps[i].lightingData.sceneLightingData.lightmapsLight.Length;
                        j++)
                    {
                        Texture2D source = currentLightmapScenario.blendableLightmaps[i].lightingData.sceneLightingData.lightmapsLight[j];

                        var targetIndex = Blending.GetSuitableTextureArray(Blending._lightmapLightArray, source.width);

                        Graphics.CopyTexture(
                            source, 0, 0,
                            Blending._lightmapLightArray[targetIndex], (i * Blending._lightmapCount) + j, 0);
                    }
                }

                if (hasDirectionalLightmaps)
                {
                    for (int j = 0;
                        j < currentLightmapScenario.blendableLightmaps[i].lightingData.sceneLightingData.lightmapsDirectional.Length;
                        j++)
                    {
                        Texture2D source = currentLightmapScenario.blendableLightmaps[i].lightingData
                            .sceneLightingData
                            .lightmapsDirectional[j];

                        var targetIndex = Blending.GetSuitableTextureArray(Blending._lightmapDirArray, source.width);

                        Graphics.CopyTexture(
                            source, 0, 0,
                            Blending._lightmapDirArray[targetIndex], (i * Blending._lightmapCount) + j, 0);
                    }
                }

                if (hasShadowmasks)
                {
                    for (int j = 0;
                        j < currentLightmapScenario.blendableLightmaps[i].lightingData.sceneLightingData.lightmapsShadowmask.Length;
                        j++)
                    {
                        Texture2D source = currentLightmapScenario.blendableLightmaps[i].lightingData
                            .sceneLightingData
                            .lightmapsShadowmask[j];

                        var targetIndex = Blending.GetSuitableTextureArray(Blending._lightmapShadowMaskArray, source.width);

                        Graphics.CopyTexture(
                            source, 0, 0,
                            Blending._lightmapShadowMaskArray[targetIndex], (i * Blending._lightmapCount) + j, 0);
                    }
                }
            }

            for (int i = 0; i < Blending._resultStaticAffectedObjects.Count; i++)
            {
                var lmIndex = 0;
                Mesh resultMesh = null;
                MeshFilter meshFilter = null;
                UnityEngine.TerrainData terrainData = null;

                if (Blending._resultStaticAffectedObjects[i].renderer != null)
                {
                    lmIndex = Blending._resultStaticAffectedObjects[i].renderer.lightmapIndex;
                    meshFilter = Blending._resultStaticAffectedObjects[i].renderer.GetComponent<MeshFilter>();

                    var originalMesh = meshFilter.sharedMesh;

                    if (originalMesh.name.Contains("_mls_clone"))
                    {
                        resultMesh = originalMesh;
                    }
                    else
                    {
                        resultMesh = new Mesh();

                        resultMesh.name = originalMesh.name + "_mls_clone";
                        resultMesh.vertices = originalMesh.vertices;
                        resultMesh.triangles = originalMesh.triangles;
                        resultMesh.normals = originalMesh.normals;
                        resultMesh.uv = originalMesh.uv;
                        resultMesh.uv2 = originalMesh.uv2;
                        resultMesh.subMeshCount = originalMesh.subMeshCount;

                        for (int j = 0; j < resultMesh.subMeshCount; j++)
                        {
                            resultMesh.SetIndices(originalMesh.GetIndices(j), originalMesh.GetTopology(j), j);
                            resultMesh.SetSubMesh(j, originalMesh.GetSubMesh(j));
                        }

                        meshFilter.sharedMesh = resultMesh;
                    }

                    var targetTextureArrayIndex = Blending.GetSuitableTextureArray(
                    Blending._lightmapLightArray,
                    currentLightmapScenario.blendableLightmaps[0].lightingData.sceneLightingData.lightmapsLight[lmIndex].width);

                    Blending._resultStaticAffectedObjects[i].renderer.realtimeLightmapScaleOffset = new Vector4(targetTextureArrayIndex, lmIndex);

                    var objectData = new Color[resultMesh.uv.Length];

                    Parallel.For(0, objectData.Length, j =>
                    {
                        objectData[j] = new Color((float) targetTextureArrayIndex, (float) lmIndex, 0, 0);
                    });
                    
                    //var test = new List<Vector2>();
                    //test.Add(new Vector2((float) targetTextureArrayIndex, (float) lmIndex));

                    resultMesh.SetColors(objectData);
                    //resultMesh.SetUVs(7, test);
                }
                else if (Blending._resultStaticAffectedObjects[i].terrain != null)
                {
                    lmIndex = Blending._resultStaticAffectedObjects[i].terrain.lightmapIndex;
                    terrainData = Blending._resultStaticAffectedObjects[i].terrain.terrainData;
                    continue;
                }
            }

#if BAKERY_INCLUDED
                    Shader.SetGlobalTexture(Blending._MLS_BakeryRNM0_Array, Blending._lightmapBakeryRNM0Array);
                    Shader.SetGlobalTexture(Blending._MLS_BakeryRNM1_Array, Blending._lightmapBakeryRNM1Array);
                    Shader.SetGlobalTexture(Blending._MLS_BakeryRNM2_Array, Blending._lightmapBakeryRNM2Array);
#endif

            for (int i = 0; i < 9; i++)
            {
                Shader.SetGlobalTexture(Blending._MLS_Lightmap_Color_Array[i], Blending._lightmapLightArray[i]);
                Shader.SetGlobalTexture(Blending._MLS_Lightmap_Directional_Array[i], Blending._lightmapDirArray[i]);
                Shader.SetGlobalTexture(Blending._MLS_Lightmap_ShadowMask_Array[i], Blending._lightmapShadowMaskArray[i]);
            }
            #endregion

            globalDataArraysInitialized = true;
        }

        private void ConfigureAffectedObjects(string targetScene)
        {
            List<StoredLightmapData> sourceData = new List<StoredLightmapData>();

            if (workflow == Workflow.MultiScene)
            {
                storedLightmapDatas.TryGetValue(targetScene, out sourceData);
            }
            else
            {
                sourceData = sceneLightmapDatas;
            }

            if (sourceData != null && sourceData.Count > 0)
            {
                StoreAffectableObjects(targetScene);

                if (sceneStaticAffectedObjects.Count > 0)
                {
                    for (int i = 0; i < sceneStaticAffectedObjects.Count; i++)
                    {
                        if (sceneStaticAffectedObjects[i].terrain == null)
                        {
                            if (sceneStaticAffectedObjects[i].isStatic)
                            {
                                SetBlendingOptions(sceneStaticAffectedObjects[i], BlendingOptions.All);
                            }
                            else
                            {
                                SetBlendingOptions(sceneStaticAffectedObjects[i], BlendingOptions.Reflections);
                            }
                        }
                        else
                        {
                            SetBlendingOptions(sceneStaticAffectedObjects[i], BlendingOptions.All);
                        }
                    }
                }
            }
            else
            {
                if (sceneStaticAffectedObjects.Count > 0)
                {
                    for (int i = 0; i < sceneStaticAffectedObjects.Count; i++)
                    {
                        if (sceneStaticAffectedObjects[i].terrain == null)
                        {
                            SetBlendingOptions(sceneStaticAffectedObjects[i], BlendingOptions.None);
                        }
                        else
                        {
                            SetBlendingOptions(sceneStaticAffectedObjects[i], BlendingOptions.None);
                        }
                    }
                }
            }

            resetAffectedObjects = false;
        }        

        private void SetBlendingOptions(AffectedObject affectableObject, BlendingOptions blendingOptions)
        {
            // ----- Outdated code

            //Blending.InitiShaderProperties();

            //if (affectableObject.renderer != null || affectableObject.terrain != null)
            //{
            //    affectableObject.InitPropertyBlock();
            //}
            //else
            //{
            //    sceneStaticAffectedObjects.Remove(affectableObject);
            //    return;
            //}

            //if (defaultCubeBlack == null)
            //{
            //    CreateDefaultCubemap();
            //}

            //if (availableScenarios.Count > 0)
            //{
            //    currentLightmapScenario = availableScenarios[0];
            //    lastLightmapScenario = currentLightmapScenario;
            //}

            //if (currentLightmapScenario != null)
            //{
            //    if (currentLightmapScenario.blendableLightmaps.Count > 1)
            //    {
            //        switch (blendingOptions)
            //        {
            //            case BlendingOptions.All:
            //                affectableObject.SetShaderFloat(Blending._MLS_ENABLE_LIGHTMAPS_BLENDING, 1);
            //                affectableObject.SetShaderFloat(Blending._MLS_ENABLE_REFLECTIONS_BLENDING, 1);
            //                break;
            //            case BlendingOptions.Lightmaps:
            //                affectableObject.SetShaderFloat(Blending._MLS_ENABLE_LIGHTMAPS_BLENDING, 1);
            //                affectableObject.SetShaderFloat(Blending._MLS_ENABLE_REFLECTIONS_BLENDING, 0);

            //                affectableObject.SetShaderTexture(Blending._MLS_Reflection_Blend_From_0, defaultCubeBlack);
            //                affectableObject.SetShaderTexture(Blending._MLS_Reflection_Blend_To_0, defaultCubeBlack);
            //                affectableObject.SetShaderTexture(Blending._MLS_Reflection_Blend_From_1, defaultCubeBlack);
            //                affectableObject.SetShaderTexture(Blending._MLS_Reflection_Blend_To_1, defaultCubeBlack);
            //                affectableObject.SetShaderTexture(Blending._MLS_SkyReflection_Blend_From, defaultCubeBlack);
            //                affectableObject.SetShaderTexture(Blending._MLS_SkyReflection_Blend_To, defaultCubeBlack);
            //                break;
            //            case BlendingOptions.Reflections:
            //                affectableObject.SetShaderFloat(Blending._MLS_ENABLE_LIGHTMAPS_BLENDING, 0);
            //                affectableObject.SetShaderFloat(Blending._MLS_ENABLE_REFLECTIONS_BLENDING, 1);
            //                break;
            //            case BlendingOptions.None:
            //                affectableObject.SetShaderFloat(Blending._MLS_ENABLE_LIGHTMAPS_BLENDING, 0);
            //                affectableObject.SetShaderFloat(Blending._MLS_ENABLE_REFLECTIONS_BLENDING, 0);

            //                affectableObject.SetShaderTexture(Blending._MLS_Reflection_Blend_From_0, defaultCubeBlack);
            //                affectableObject.SetShaderTexture(Blending._MLS_Reflection_Blend_To_0, defaultCubeBlack);
            //                affectableObject.SetShaderTexture(Blending._MLS_Reflection_Blend_From_1, defaultCubeBlack);
            //                affectableObject.SetShaderTexture(Blending._MLS_Reflection_Blend_To_1, defaultCubeBlack);
            //                affectableObject.SetShaderTexture(Blending._MLS_SkyReflection_Blend_From, defaultCubeBlack);
            //                affectableObject.SetShaderTexture(Blending._MLS_SkyReflection_Blend_To, defaultCubeBlack);
            //                break;
            //        }
            //    }
            //    else
            //    {
            //        affectableObject.SetShaderFloat(Blending._MLS_ENABLE_LIGHTMAPS_BLENDING, 0);
            //        affectableObject.SetShaderFloat(Blending._MLS_ENABLE_REFLECTIONS_BLENDING, 0);

            //        affectableObject.SetShaderTexture(Blending._MLS_Reflection_Blend_From_0, defaultCubeBlack);
            //        affectableObject.SetShaderTexture(Blending._MLS_Reflection_Blend_To_0, defaultCubeBlack);
            //        affectableObject.SetShaderTexture(Blending._MLS_Reflection_Blend_From_1, defaultCubeBlack);
            //        affectableObject.SetShaderTexture(Blending._MLS_Reflection_Blend_To_1, defaultCubeBlack);
            //        affectableObject.SetShaderTexture(Blending._MLS_SkyReflection_Blend_From, defaultCubeBlack);
            //        affectableObject.SetShaderTexture(Blending._MLS_SkyReflection_Blend_To, defaultCubeBlack);
            //    }
            //}
            //else
            //{
            //    affectableObject.SetShaderFloat(Blending._MLS_ENABLE_LIGHTMAPS_BLENDING, 0);
            //    affectableObject.SetShaderFloat(Blending._MLS_ENABLE_REFLECTIONS_BLENDING, 0);

            //    affectableObject.SetShaderTexture(Blending._MLS_Reflection_Blend_From_0, defaultCubeBlack);
            //    affectableObject.SetShaderTexture(Blending._MLS_Reflection_Blend_To_0, defaultCubeBlack);
            //    affectableObject.SetShaderTexture(Blending._MLS_Reflection_Blend_From_1, defaultCubeBlack);
            //    affectableObject.SetShaderTexture(Blending._MLS_Reflection_Blend_To_1, defaultCubeBlack);
            //    affectableObject.SetShaderTexture(Blending._MLS_SkyReflection_Blend_From, defaultCubeBlack);
            //    affectableObject.SetShaderTexture(Blending._MLS_SkyReflection_Blend_To, defaultCubeBlack);
            //}

            //affectableObject.ApplyPropertyBlock();
        }

#if UNITY_EDITOR
        [DidReloadScripts]
#endif
        public static void SetDefaultShaderValues()
        {
            Blending.InitiShaderProperties();

            if (defaultCubeBlack == null)
            {
                CreateDefaultCubemap();
            }
        }

        public static void SetBlendingOptionsGlobalStatic(BlendingOptions blendingOptions)
        {
            switch (blendingOptions)
            {
                case BlendingOptions.All:
                    Shader.EnableKeyword("MLS_LIGHTMAPS_BLENDING_ON");
                    Shader.DisableKeyword("MLS_LIGHTMAPS_BLENDING_OFF");
                    Shader.EnableKeyword("MLS_REFLECTIONS_BLENDING_ON");
                    Shader.DisableKeyword("MLS_REFLECTIONS_BLENDING_OFF");
                    Shader.EnableKeyword("_MLS_SKYBOX_BLENDING_ON");
                    Shader.DisableKeyword("_MLS_SKYBOX_BLENDING_OFF");
                    break;
                case BlendingOptions.None:
                    Shader.EnableKeyword("MLS_LIGHTMAPS_BLENDING_OFF");
                    Shader.DisableKeyword("MLS_LIGHTMAPS_BLENDING_ON");
                    Shader.EnableKeyword("MLS_REFLECTIONS_BLENDING_OFF");
                    Shader.DisableKeyword("MLS_REFLECTIONS_BLENDING_ON");
                    Shader.EnableKeyword("_MLS_SKYBOX_BLENDING_OFF");
                    Shader.DisableKeyword("_MLS_SKYBOX_BLENDING_ON");
                    break;
            }
        }

        public void SetBlendingOptionsGlobal(BlendingOptions blendingOptions)
        {
            SetBlendingOptionsGlobalStatic(blendingOptions);
        }

        private void StoreAffectableObjects(string targetScene)
        {
            List<AffectedObject> tempAffectableTerrains = new List<AffectedObject>();
            List<AffectedObject> tempStaticAffectableObjects = new List<AffectedObject>();
            List<AffectedObject> tempDynamicAffectableObjects = new List<AffectedObject>();

            Object[] renderers = FindObjectsOfType<Renderer>();

            foreach (Renderer renderer in renderers)
            {
                if (workflow == Workflow.MultiScene)
                {
                    if (renderer.gameObject.scene.name != targetScene)
                    {
                        continue;
                    }
                }

                MLSStaticRenderer staticRenderer = renderer.gameObject.GetComponent<MLSStaticRenderer>();
                MLSDynamicRenderer dynamicRenderer = renderer.gameObject.GetComponent<MLSDynamicRenderer>();

                if (staticRenderer != null)
                {
                    AffectedObject affectableObject = new AffectedObject();

                    affectableObject.mlsObject = renderer.GetComponent<MLSObject>();
                    affectableObject.isStatic = true;
                    affectableObject.renderer = renderer;
                    affectableObject.reflectionProbeUsage = renderer.reflectionProbeUsage;
                    affectableObject.materialsCount = renderer.sharedMaterials.Length;
                    affectableObject.objectId = staticRenderer.scriptId;

                    tempStaticAffectableObjects.Add(affectableObject);
                }
                else if (dynamicRenderer != null)
                {
                    if (!renderer.enabled)
                    {
                        if (renderer.GetComponent<MLSStaticRenderer>() != null)
                        {
                            GameObject.DestroyImmediate(renderer.GetComponent<MLSStaticRenderer>());
                        }

                        continue;
                    }

                    AffectedObject affectableObject = new AffectedObject();

                    affectableObject.mlsObject = renderer.GetComponent<MLSObject>();
                    affectableObject.renderer = renderer;
                    affectableObject.reflectionProbeUsage = renderer.reflectionProbeUsage;
                    affectableObject.materialsCount = renderer.sharedMaterials.Length;
                    affectableObject.objectId = dynamicRenderer.scriptId;

                    tempDynamicAffectableObjects.Add(affectableObject);
                }
            }

            Object[] terrains = FindObjectsOfType<Terrain>();

            foreach (Terrain terrain in terrains)
            {
                if (workflow == Workflow.MultiScene)
                {
                    if (terrain.gameObject.scene.name != targetScene)
                    {
                        continue;
                    }
                }

                if (terrain.lightmapIndex < 0 || terrain.lightmapScaleOffset.x == 0 || terrain.lightmapScaleOffset.y == 0 || !terrain.enabled)
                {
                    if (terrain.GetComponent<MLSStaticRenderer>() != null)
                    {
                        GameObject.DestroyImmediate(terrain.GetComponent<MLSStaticRenderer>());
                    }

                    continue;
                }

                MLSStaticRenderer staticRenderer = terrain.gameObject.GetComponent<MLSStaticRenderer>();
                MLSDynamicRenderer dynamicRenderer = terrain.gameObject.GetComponent<MLSDynamicRenderer>();

                if (staticRenderer != null)
                {
                    AffectedObject affectableObject = new AffectedObject();

                    affectableObject.mlsObject = staticRenderer.GetComponent<MLSObject>();
                    affectableObject.isStatic = true;
                    affectableObject.terrain = terrain;
                    affectableObject.objectId = staticRenderer.scriptId;

                    tempAffectableTerrains.Add(affectableObject);
                }
                else if (dynamicRenderer != null)
                {
                    AffectedObject affectableObject = new AffectedObject();

                    affectableObject.mlsObject = dynamicRenderer.GetComponent<MLSObject>();
                    affectableObject.terrain = terrain;
                    affectableObject.objectId = dynamicRenderer.scriptId;

                    tempDynamicAffectableObjects.Add(affectableObject);
                }
            }

            if (workflow == Workflow.MultiScene)
            {
                if (!staticAffectedObjects.ContainsKey(targetScene))
                {
                    staticAffectedObjects.Add(targetScene, tempStaticAffectableObjects);
                }
                else
                {
                    staticAffectedObjects[targetScene] = tempStaticAffectableObjects;
                }

                if (!dynamicAffectedObjects.ContainsKey(targetScene))
                {
                    dynamicAffectedObjects.Add(targetScene, tempDynamicAffectableObjects);
                }
                else
                {
                    dynamicAffectedObjects[targetScene] = tempDynamicAffectableObjects;
                }
            }
            else
            {
                affectedTerrains = tempAffectableTerrains;
                sceneStaticAffectedObjects = tempStaticAffectableObjects;
                sceneDynamicAffectedObjects = tempDynamicAffectableObjects;
            }
        }

        private void TetrahedralizeProbesAsync()
        {
            needsRetetrahedralization = true;
            tetrahedralizationCompleted = false;

            //LightProbes.TetrahedralizeAsync();
        }

        private void TetrahedralizationCompleted()
        {
            needsRetetrahedralization = false;
            tetrahedralizationCompleted = true;
        }

        private bool tetrahedralizationCompleted;
        private bool needsRetetrahedralization;

        private void Init()
        {
            if (useTextureArrays)
            {
                Shader.EnableKeyword("MLS_TEXTURE_ARRAYS_ON");
                Shader.DisableKeyword("MLS_TEXTURE_ARRAYS_OFF");
            }
            else
            {
                Shader.EnableKeyword("MLS_TEXTURE_ARRAYS_OFF");
                Shader.DisableKeyword("MLS_TEXTURE_ARRAYS_ON");
            }

            CreateDefaultCubemap();
#if UNITY_EDITOR
            LoadDependentAssets();
            Blending.InitComputes();
#endif

            if (systemProperties != null && !systemProperties.useSwitchingOnly)
            {
                Shader.EnableKeyword("MLS_REFLECTIONS_BLENDING_ON");
                Shader.DisableKeyword("MLS_REFLECTIONS_BLENDING_OFF");

                Shader.EnableKeyword("MLS_LIGHTMAPS_BLENDING_ON");
                Shader.DisableKeyword("MLS_LIGHTMAPS_BLENDING_OFF");

                Shader.EnableKeyword("_MLS_SKYBOX_BLENDING_ON");
                Shader.DisableKeyword("MLS_SKYBOX_BLENDING_OFF");
            }
            else
            {
                Shader.EnableKeyword("MLS_REFLECTIONS_BLENDING_OFF");
                Shader.DisableKeyword("MLS_REFLECTIONS_BLENDING_ON");

                Shader.EnableKeyword("MLS_LIGHTMAPS_BLENDING_OFF");
                Shader.DisableKeyword("MLS_LIGHTMAPS_BLENDING_ON");

                Shader.EnableKeyword("_MLS_SKYBOX_BLENDING_OFF");
                Shader.DisableKeyword("MLS_SKYBOX_BLENDING_ON");
            }

            storedDataUpdated = false;
            globalDataArraysInitialized = false;

            OnDynamicRendererAdded = new DynamicRendererAddedEvent();
            OnDynamicRendererRemoved = new DynamicRendererRemoveEvent();

            OnDynamicRendererAdded.AddListener(AddDynamicRenderer);
            OnDynamicRendererRemoved.AddListener(RemoveDynamicRenderer);
            
            switch (workflow)
            {
                case Workflow.SingleScene:
                    if (!storedDataUpdated)
                    {
                        UpdateStoredArray(SceneManager.GetActiveScene().name, true);
                    } 
                    break;
                case Workflow.MultiScene:
#if UNITY_EDITOR
                    if (!Application.isPlaying && !storedDataUpdated)
                    {
                        UpdateStoredArray(SceneManager.GetActiveScene().name, true);
                    }
#endif
                    break;
            }
        }

        private void Awake()
        {
            Init();
        }

        public bool lightprobesBlendingStarted;
        private void OnEnable()
        {
            lightprobesBlendingStarted = false;
#if UNITY_EDITOR
            if (switcherSerializedObject == null)
            {
                switcherSerializedObject = new SerializedObject(this);
            }

            if (!Application.isPlaying)
            {
                Init();
            }
#endif

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            //LightProbes.needsRetetrahedralization += TetrahedralizeProbesAsync;
            //LightProbes.tetrahedralizationCompleted += TetrahedralizationCompleted;
        }

        void Start()
        {

        }

        private void Update() 
        {
            if (becameVisibleObjects > 0)
            {
                becameVisibleObjects = 0;

                Blending.UpdateBlend();
            }

#if UNITY_EDITOR
            if (Application.isEditor)
            {
                //EditorApplication.update += SetDefaultShaderValuesLocal;
            }
#endif
        }

        private void OnDisable()
        {
            
        }

        async void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (loadScenarioOnStart)
            {
                while (storedDataUpdatingProcess)
                {
                    Debug.Log("Wait");
                    await Task.Yield();
                }

                var onStartScenario = availableScenarios[onStartLightingScenario];
                var runtimeAPIInternal = new RuntimeAPI(this, onStartScenario, onStartPreset);
            }
        }

        private void AddDynamicRenderer(GameObject gameObject, MLSDynamicRenderer dynamicRenderer)
        {            
            AffectedObject affectableObject = new AffectedObject();

            affectableObject.renderer = gameObject.GetComponent<MeshRenderer>();
            affectableObject.objectId = dynamicRenderer.scriptId;

            if (workflow == Workflow.MultiScene)
            {
                List<AffectedObject> currentAffectableObjects = new List<AffectedObject>();

                if (dynamicAffectedObjects.ContainsKey(gameObject.scene.name))
                {
                    dynamicAffectedObjects.TryGetValue(gameObject.scene.name, out currentAffectableObjects);
                    currentAffectableObjects.Add(affectableObject);
                    dynamicAffectedObjects[gameObject.scene.name] = currentAffectableObjects;
                }
            }
            else
            {
                sceneDynamicAffectedObjects.Add(affectableObject);
            }
        }

        private void RemoveDynamicRenderer(GameObject gameObject, AffectedObject affectableObject)
        {
            if (workflow == Workflow.MultiScene)
            {
                List<AffectedObject> currentAffectableObjects = new List<AffectedObject>();

                if (dynamicAffectedObjects.ContainsKey(gameObject.scene.name))
                {
                    dynamicAffectedObjects.TryGetValue(gameObject.scene.name, out currentAffectableObjects);
                    currentAffectableObjects.Remove(affectableObject);
                    dynamicAffectedObjects[gameObject.scene.name] = currentAffectableObjects;
                }
            }
            else
            {
                sceneDynamicAffectedObjects.Remove(affectableObject);
            }
        }

        public static void CreateDefaultCubemap()
        {
#if UNITY_EDITOR
            switch (EditorUserBuildSettings.activeBuildTarget)
            {
                case BuildTarget.WebGL:
                    defaultCubeBlack = new Cubemap(12, TextureFormat.RGBAHalf, false);
                    break;
                case BuildTarget.StandaloneWindows64:
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneOSX:
                case BuildTarget.Switch:
                    defaultCubeBlack = new Cubemap(12, UnityEngine.Experimental.Rendering.DefaultFormat.HDR, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);
                    break;
                case BuildTarget.Android:
                    defaultCubeBlack = new Cubemap(12, TextureFormat.ETC_RGB4, false);
                    break;
            }
#else
            switch (Application.platform)
            {
                case RuntimePlatform.WebGLPlayer:
                    defaultCubeBlack = new Cubemap(12, TextureFormat.RGBAHalf, false);
                    break;
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.Switch:
                    defaultCubeBlack = new Cubemap(12, UnityEngine.Experimental.Rendering.DefaultFormat.HDR, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);
                    break;
                case RuntimePlatform.Android:
                    defaultCubeBlack = new Cubemap(12, TextureFormat.ETC_RGB4, false);
                    break;
            } 
#endif
        }

#if UNITY_EDITOR
        private void FindSystemProperies()
        {
            if (string.IsNullOrEmpty(workPath))
            {
                string[] directories = Directory.GetDirectories(Application.dataPath, "Magic Lightmap Switcher", SearchOption.AllDirectories);

                for (int i = 0; i < directories.Length; i++)
                {
                    if (Directory.GetFiles(directories[i]).Length == 0)
                    {
                        continue;
                    }

                    if (!directories[i].Contains("Resources"))
                    {
                        workPath = directories[i];
                        break;
                    }
                }
            }

            int subIndex = workPath.IndexOf("Assets");
            string finalPath = workPath.Substring(subIndex + "Assets".Length + 1);
            systemProperties = AssetDatabase.LoadAssetAtPath<SystemProperties>("Assets/" + finalPath + "/Editor/SystemProperties.asset");
        }

        private void LoadDependentAssets()
        {
            FindSystemProperies();

            if (systemProperties == null)
            {
                workPath = "";
                FindSystemProperies();
                Debug.LogFormat("<color=cyan>MLS:</color> Palgin's position in the project hierarchy has changed.");
            }
        }
#endif
    }
}