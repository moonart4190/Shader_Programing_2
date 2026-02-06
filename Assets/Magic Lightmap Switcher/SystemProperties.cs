using System.Collections.Generic;
using UnityEngine;

namespace MagicLightmapSwitcher
{    
    [System.Serializable]
    public class SystemProperties : ScriptableObject
    {
        [SerializeField]
        public bool standardRPActive;
        [SerializeField]
        public bool universalRPActive;
        [SerializeField]
        public bool highDefinitionRPActive;
        [SerializeField]
        public bool standardRPPatched;
        [SerializeField]
        public bool universalRPPatched;
        [SerializeField]
        public bool highDefinitionRPPatched;
        [SerializeField]
        public bool clearOriginalLightingData;
        [SerializeField]
        public bool batchByLightmapIndex;
        [SerializeField]
        public bool editorRestarted;
        [SerializeField]
        public double prevTimeSinceStartup;
        [SerializeField]
        public bool checkForPatchedRoutineInProcess;
        [SerializeField, HideInInspector]
        public bool restoring;
        [SerializeField]
        public bool srpPatching;
        [SerializeField]
        public bool waitForRestart;
        [SerializeField, HideInInspector]
        public string srpCoreDirFrom;
        [SerializeField, HideInInspector]
        public string srpCoreDirTo;
        [SerializeField, HideInInspector]
        public string srpCommonDirFrom;
        [SerializeField, HideInInspector]
        public string srpCommonDirTo;
        
        [SerializeField, HideInInspector]
        public string shaderGraphDirFrom;
        [SerializeField, HideInInspector]
        public string ShaderGraphDirTo;
        [SerializeField, HideInInspector]
        public string VFXGraphDirFrom;
        [SerializeField, HideInInspector]
        public string VFXGraphDirTo;
        [SerializeField, HideInInspector]
        public string HDRPConfigDirFrom;
        [SerializeField, HideInInspector]
        public string HDRPConfigDirTo;
        [SerializeField]
        public string srpVersion;
        [SerializeField, HideInInspector]
        public bool srpReady;
        [SerializeField]
        public string storePath = "/MLS_DATA";
        [SerializeField]
        public bool deferredWarningConfirmed;
        [SerializeField]
        public bool useSwitchingOnly;
        [SerializeField]
        public bool ignoreCameraError;
        [SerializeField]
        public bool showShaderInitWarning = true;

        #region Scriptable Render Pipeline Files
        #region Common
        [HideInInspector]
        public string srp_PathRoot;
        [SerializeField, HideInInspector]
        public string srp_ModifiedSourcesPathRoot;
        [SerializeField, HideInInspector]
        public string srp_Core_SourcesPath;
        [SerializeField, HideInInspector]
        public string srp_CORE_ModifySourcesPath;
        [SerializeField, HideInInspector]
        public string SRP__ENTITY_LIGHTING__DEFAULT_SOURCE_FILE = "EntityLighting.hlsl";
        [SerializeField, HideInInspector]
        public string SRP__COMMON__DEFAULT_SOURCE_FILE = "Common.hlsl";
        #endregion

        #region URP
        [SerializeField, HideInInspector]
        public string srp_URP_SourcesPath;
        [SerializeField, HideInInspector]
        public string srp_URP_COMMON_ModifySourcesPath;
        [SerializeField, HideInInspector]
        public string srp_URP_Shaders_ModifySourcesPath;
        [SerializeField, HideInInspector]
        public string URP__SHADOWS__DEFAULT_SOURCE_FILE = "Shadows.hlsl";
        [SerializeField, HideInInspector]
        public string URP__LIT__DEFAULT_SOURCE_FILE = "Shaders/Lit.shader";
        [SerializeField, HideInInspector]
        public string URP__TERRAIN_LIT__DEFAULT_SOURCE_FILE = "Shaders/Terrain/TerrainLit.shader";
        [SerializeField, HideInInspector]
        public string URP__LIT__MODYFIED_SOURCE_FILE = "Shaders/Lit.shader";
        [SerializeField, HideInInspector]
        public string URP__TERRAIN_LIT__MODYFIED_SOURCE_FILE = "Shaders/TerrainLit.shader";
        [SerializeField, HideInInspector]
        public string URP__LIGHTING__DEFAULT_SOURCE_FILE = "_EMPTY_";
        #endregion
        
        #region HDRP
        [SerializeField, HideInInspector]
        public string srp_HDRP_SourcesPath;
        [SerializeField, HideInInspector]
        public string srp_HDRP_COMMON_ModifySourcesPath;
        [SerializeField, HideInInspector]
        public string srp_HDRP_Shaders_ModifySourcesPath;
        [SerializeField, HideInInspector]
        public string HDRP__LIT__DEFAULT_SOURCE_FILE = "Runtime/Material/Lit/Lit.shader";
        [SerializeField, HideInInspector]
        public string HDRP__HDRI_SKY__DEFAULT_SOURCE_FILE = "Runtime/Sky/HDRISky/HDRISky.shader";
        [SerializeField, HideInInspector]
        public string HDRP__TERRAIN_LIT__DEFAULT_SOURCE_FILE = "Runtime/Material/TerrainLit/TerrainLit.shader";
        [SerializeField, HideInInspector]
        public string HDRP__LIGHTING_LOOP_DEF__DEFAULT_SOURCE_FILE = "Runtime/Lighting/LightLoop/LightLoopDef.hlsl";
        [SerializeField, HideInInspector]
        public string HDRP__BUILTINGI__DEFAULT_SOURCE_FILE = "Runtime/Material/BuiltInGIUtilities.hlsl";
        #endregion
        #endregion
    }
}
