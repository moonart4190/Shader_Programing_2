using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEditor.PackageManager.Requests;
using UnityEditor.PackageManager;
using UnityEngine;
using NUnit.Framework.Internal;
using static UnityEngine.GraphicsBuffer;
using System.Threading.Tasks;

namespace MagicLightmapSwitcher
{
    [ExecuteInEditMode]
    public class MLSShaderCodeModifier : EditorWindow
    {
        public enum RenderPipelineType
        {
            Standard,
            URP,
            HDRP
        }

        private struct CmdProcessArguments
        {
            public enum CommandType
            {
                Copy,
                Delete
            }

            public CmdProcessArguments(CmdProcessArguments data)
            {
                commandType = data.commandType;
                deleteFrom = data.deleteFrom;
                copyFrom = data.copyFrom;
                copyTo = data.copyTo;
            }

            public CommandType commandType;
            public string deleteFrom;
            public string copyFrom;
            public string copyTo;
        }

        private static SystemProperties systemProperties;
        private static MLSShaderCodeModifier shaderCodeModifierWindow;

        private static IEnumerator prepareSRPPackageRoutine;
        private static IEnumerator checkForPatchedRoutine;

        private const string MLS_INCLUDED_DEFINE = "MLS_INCLUDED";

        private static bool initialized;
        private static bool hasHDRPDefine;
        private static bool hasURPDefine;
        private const string HDRP_7_PackageDefine = "MT_HDRP_7_INCLUDED";
        private const string HDRP_8_PackageDefine = "MT_HDRP_8_INCLUDED";
        private const string HDRP_9_PackageDefine = "MT_HDRP_9_INCLUDED";
        private const string HDRP_10_PackageDefine = "MT_HDRP_10_INCLUDED";
        private const string HDRP_11_PackageDefine = "MT_HDRP_11_INCLUDED";
        private const string HDRP_12_PackageDefine = "MT_HDRP_12_INCLUDED";
        private const string HDRP_13_PackageDefine = "MT_HDRP_13_INCLUDED";
        private const string HDRP_14_PackageDefine = "MT_HDRP_14_INCLUDED";
        private const string HDRP_16_PackageDefine = "MT_HDRP_16_INCLUDED";
        private const string URP_7_PackageDefine = "MT_URP_7_INCLUDED";
        private const string URP_8_PackageDefine = "MT_URP_9_INCLUDED";
        private const string URP_9_PackageDefine = "MT_URP_9_INCLUDED";
        private const string URP_10_PackageDefine = "MT_URP_10_INCLUDED";
        private const string URP_11_PackageDefine = "MT_URP_11_INCLUDED";
        private const string URP_12_PackageDefine = "MT_URP_12_INCLUDED";
        private const string URP_13_PackageDefine = "MT_URP_13_INCLUDED";
        private const string URP_14_PackageDefine = "MT_URP_14_INCLUDED";
        private const string URP_16_PackageDefine = "MT_URP_16_INCLUDED";
        private const string URP_17_PackageDefine = "MT_URP_17_INCLUDED";
        private static bool manual;
        private static bool standard_RP_Patched;  
        private static bool isCustom = false;

        private static string patch_URP_Button_Text = "Patch URP Shaders";
        private static string patch_HDRP_Button_Text = "Patch HDRP Shaders";

        #region Standard Render Pipeline

        private static string standard_RP_SourcesPath;
        private static string standard_RP_ModifySourcesPath;

        // Standard RP Default Source Files
        private static string STANDARD__UNITY_IMAGE_BASED_LIGHTING__DEFAULT_SOURCE_FILE = "UnityImageBasedLighting.cginc";
        private static string STANDARD__UNITY_PBS_LIGHTING__DEFAULT_SOURCE_FILE = "UnityPBSLighting.cginc";
        private static string STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE = "UnityShadowLibrary.cginc";
        private static string STANDARD__GLOBAL_ILLUMINATION__DEFAULT_SOURCE_FILE = "UnityGlobalIllumination.cginc";
        private static string STANDARD__CORE__DEFAULT_SOURCE_FILE = "UnityStandardCore.cginc";

        // Standard RP Additional Includes
        private static string STANDARD__CORE__COMMON__INCLUDE_FILE = "MLS_Standard_Common.cginc";

        // Standard RP Search Fragments
        private static string STANDARD__UNITY_IMAGE_BASED_LIGHTING__UNITY_GLOSSY_ENVIRONMENT__SEARCH_STRING =
            "half3 Unity_GlossyEnvironment (UNITY_ARGS_TEXCUBE(tex), half4 hdr, Unity_GlossyEnvironmentData glossIn)";
        private static string STANDARD__UNITY_PBS_LIGHTING__LIGHTING_STANDARD_GI__SEARCH_STRING =
            "gi = UnityGlobalIllumination(data, s.Occlusion, s.Normal, g);";
        private static string STANDARD__UNITY_SHADOW_LIBRARY__UNITY_SAMPLE_BAKED_OCCLUSION__SEARCH_STRING =
            "fixed UnitySampleBakedOcclusion (float2 lightmapUV, float3 worldPos)";
        private static string STANDARD__UNITY_SHADOW_LIBRARY__UNITY_GET_RAW_BAKED_OCCLUSIONS__SEARCH_STRING =
            "fixed4 UnityGetRawBakedOcclusions(float2 lightmapUV, float3 worldPos)";
        private static string STANDARD__GLOBAL_ILLUMINATION__INCLUDE_SECTION__SEARCH_STRING =
            "#include \"UnityImageBasedLighting.cginc\"";
        private static string STANDARD__GLOBAL_ILLUMINATION__UNITY_GI_BASE__SEARCH_STRING =
            "half4 bakedColorTex = UNITY_SAMPLE_TEX2D(unity_Lightmap, data.lightmapUV.xy);" +
            "fixed4 bakedDirTex = UNITY_SAMPLE_TEX2D_SAMPLER (unity_LightmapInd, unity_Lightmap, data.lightmapUV.xy);";
        private static string STANDARD__GLOBAL_ILLUMINATION__UNITY_GI_INDIRECT_SPECULAR__SEARCH_STRING =
            "half3 env0 = Unity_GlossyEnvironment (UNITY_PASS_TEXCUBE(unity_SpecCube0), data.probeHDR[0], glossIn);" +
            "half3 env1 = Unity_GlossyEnvironment (UNITY_PASS_TEXCUBE_SAMPLER(unity_SpecCube1,unity_SpecCube0), data.probeHDR[1], glossIn);";

        // Standard RP Patch Source Files
        private static string STANDARD__UNITY_IMAGE_BASED_LIGHTING__UNITY_GLOSSY_ENVIRONMENT__MODYFI_SOURCE_FILE = "Unity_Image_Based_Lighting_Glossy_Environment.txt";
        private static string STANDARD__UNITY_PBS_LIGHTING__LIGHTING_STANDARD__MODYFI_SOURCE_FILE = "Unity_PBS_Lighting_Standard_Lighting_Additions.txt";
        private static string STANDARD__GLOBAL_ILLUMINATION__INCLUDE_SECTION__MODYFI_SOURCE_FILE = "Global_Illumination_Include_Section_Additions.txt";
        private static string STANDARD__GLOBAL_ILLUMINATION__UNITY_GI_INDIRECT_SPECULAR__MODYFI_SOURCE_FILE = "Global_Illumination_UnityGI_IndirectSpecular_Additions.txt";
        private static string STANDARD__GLOBAL_ILLUMINATION__UNITY_GI_BASE__MODYFI_SOURCE_FILE = "Global_Illumination_Unity_GI_Base_Additions.txt";
        private static string STANDARD__UNITY_SHADOW_LIBRARY__UNITY_SAMPLE_BAKED_OCCLUSION__MODYFI_SOURCE_FILE = "Unity_Shadow_Library_Unity_Sample_Baked_Occlusion_Additions.txt";
        private static string STANDARD__UNITY_SHADOW_LIBRARY__UNITY_GET_RAW_BAKED_OCCLUSIONS__MODYFI_SOURCE_FILE = "Unity_Shadow_Library_Unity_Get_Raw_Baked_Occlusions_Additions.txt";

        // Standard RP Patch Lines
        private List<string> standard__Unity_Image_Based_Lighting__Glossy_Environment_Lines = new List<string>();
        private List<string> standard__Unity_PBS_Lighting__Lighting_Standard_Lines = new List<string>();
        private List<string> standard__Global_Illumination__Include_Section_Lines = new List<string>();
        private List<string> standard__Global_Illumination__Unity_GI_Indirect_Specular_Lines = new List<string>();
        private List<string> standard__Global_Illumination__Unity_GI_Base_Lines = new List<string>();
        private List<string> standard__Unity_Shadow_Library__Unity_Sample_Baked_Occlusion_Lines = new List<string>();
        private List<string> standard__Unity_Shadow_Library__Unity_Get_Raw_Baked_Occlusions_Lines = new List<string>();
        private List<string> includeSectionAdditions = new List<string>();
        private List<string> fragmentGIBlendReflectionProbes = new List<string>();

        // Standard MLS Patched Signatures
        private static string STANDARD__UNITY_IMAGE_BASED_LIGHTING__UNITY_GLOSSY_ENVIRONMENT__SIGNATURE = "//<MLS_IMAGE_BASED_LIGHTING_ENVIRONMENT_CUSTOM>";
        private static string STANDARD__UNITY_PBS_LIGHTING__LIGHTING_STANDARD__SIGNATURE = "//<MLS_UNITY_PBS_LIGHTING_LIGHTING_STANDARD_ADDITIONS>";
        private static string STANDARD__GLOBAL_ILLUMINATION__INCLUDE_SECTION__SIGNATURE = "//<MLS_GLOBAL_ILLUMINATION_INCLUDE_SECTION>";
        private static string STANDARD__GLOBAL_ILLUMINATION__UNITY_GI_INDIRECT_SPECULAR__SIGNATURE = "//<MLS_GLOBAL_ILLUMINATION_UNITY_GI_INDIRECT_SPECULAR_ADDITIONS>";
        private static string STANDARD__GLOBAL_ILLUMINATION__UNITY_GI_BASE__SIGNATURE = "//<MLS_GLOBAL_ILLUMINATION_UNITY_GI_BASE_ADDITIONS>";
        private static string STANDARD__UNITY_SHADOW_LIBRARY__UNITY_SAMPLE_BAKED_OCCLUSION__SIGNATURE = "//<MLS_UNITY_SHADOW_LIBRARY_UNITY_SAMPLE_BAKED_OCCLUSION_ADDITIONS>";
        private static string STANDARD__UNITY_SHADOW_LIBRARY__UNITY_GET_RAW_BAKED_OCCLUSIONS__SIGNATURE = "//<MLS_UNITY_SHADOW_LIBRARY_UNITY_GET_RAW_BAKED_OCCLUSIONS_ADDITIONS>";
        #endregion        

        [MenuItem("Tools/Magic Tools/Magic Lightmap Switcher/Prepare Shaders...", priority = 1)]
        static void OpenWindow()
        {
            shaderCodeModifierWindow = (MLSShaderCodeModifier) GetWindow(typeof(MLSShaderCodeModifier), true, "Magic Lightmap Switcher - Patch Shaders");
            shaderCodeModifierWindow.maxSize = new Vector2(250 * EditorGUIUtility.pixelsPerPoint, 100 * EditorGUIUtility.pixelsPerPoint);
            shaderCodeModifierWindow.minSize = shaderCodeModifierWindow.maxSize;
            
            #if UNITY_EDITOR_OSX && UNITY_2020_1_OR_NEWER
            shaderCodeModifierWindow.position = new Rect(
                EditorGUIUtility.GetMainWindowPosition().center.x - shaderCodeModifierWindow.minSize.x * 0.5f,
                EditorGUIUtility.GetMainWindowPosition().center.y - shaderCodeModifierWindow.minSize.y * 0.5f,
                shaderCodeModifierWindow.minSize.x,
                shaderCodeModifierWindow.minSize.y);
            #else
            shaderCodeModifierWindow.position = new Rect(
                Screen.width + shaderCodeModifierWindow.minSize.x * 0.5f,
                Screen.height - shaderCodeModifierWindow.minSize.y * 0.5f,
                shaderCodeModifierWindow.minSize.x,
                shaderCodeModifierWindow.minSize.y);
            #endif

            shaderCodeModifierWindow.Show();
        }        

        public static void PrepareSRPPackageSRPIteratorUpdate()
        {
            if (prepareSRPPackageRoutine != null && prepareSRPPackageRoutine.MoveNext())
            {
                return;
            }

            EditorApplication.update -= PrepareSRPPackageSRPIteratorUpdate;
        }

        public static void CheckForPatchedIteratorUpdate()
        {
            if (checkForPatchedRoutine != null && checkForPatchedRoutine.MoveNext())
            {
                return;
            }

            EditorApplication.update -= CheckForPatchedIteratorUpdate;
        }

        private static void GetSystemProperties()
        {
            if (systemProperties == null)
            {
                string[] directories = Directory.GetDirectories(Application.dataPath, "Magic Lightmap Switcher", SearchOption.AllDirectories);
                string projectRelativePath = directories[0].Split(new string[] { "Magic Lightmap Switcher" }, StringSplitOptions.None)[0];

                systemProperties = AssetDatabase.LoadAssetAtPath(FileUtil.GetProjectRelativePath(projectRelativePath + "/Magic Lightmap Switcher/Editor/SystemProperties.asset"), typeof(SystemProperties)) as SystemProperties;
            }

            if (systemProperties == null)
            {
                string[] directories = Directory.GetDirectories(Application.dataPath, "Magic Lightmap Switcher", SearchOption.AllDirectories);
                string projectRelativePath = directories[0].Split(new[] { "Magic Lightmap Switcher" }, StringSplitOptions.None)[0];
                
                systemProperties = ScriptableObject.CreateInstance<SystemProperties>();

                AssetDatabase.CreateAsset(systemProperties, FileUtil.GetProjectRelativePath(projectRelativePath + "/Magic Lightmap Switcher/Editor/SystemProperties.asset"));
                AssetDatabase.SaveAssets();
            }
        }

        private static void GetInstalledSRPVersion()
        {
            if (!systemProperties.srpPatching)
            {
                if (string.IsNullOrEmpty(systemProperties.srpVersion))
                {
                    string fullPathToSRP = Path.GetFullPath("Packages/com.unity.render-pipelines.core");

                    if (File.Exists(fullPathToSRP + "/package.json"))
                    {
                        string[] packageInfo = File.ReadAllLines(fullPathToSRP + "/package.json");

                        for (int i = 0; i < packageInfo.Length; i++)
                        {
                            if (packageInfo[i].Contains("version"))
                            {
                                systemProperties.srpVersion =
                                    packageInfo[i].Split(new char[] {':'})[1].Split(new char[] {'\"'})[1];
                            }
                        }
                    }
                }
            }
            else
            {
                if (!Directory.Exists(Application.dataPath + "/../MLS_SRP_TEMP"))
                {
                    systemProperties.srpPatching = false;
                    return;
                }

                string[] packageInfo = File.ReadAllLines(Application.dataPath + "/../MLS_SRP_TEMP/com.unity.render-pipelines.core/package.json");

                for (int i = 0; i < packageInfo.Length; i++)
                {
                    if (packageInfo[i].Contains("version"))
                    {
                        systemProperties.srpVersion = packageInfo[i].Split(new char[] { ':' })[1].Split(new char[] { '\"' })[1];
                    }
                }
            }
        }

        [DidReloadScripts]
        static void CheckSRPState()
        {
            GetSystemProperties();
            GetInstalledSRPVersion();

            systemProperties.highDefinitionRPActive = DoesTypeExist("HDRenderPipelineAsset");
            systemProperties.universalRPActive = DoesTypeExist("UniversalRenderPipelineAsset");

#if UNITY_6000_0_OR_NEWER
            string scriptingDefine = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);
#else
            string scriptingDefine = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
#endif

            string[] scriptingDefines = scriptingDefine.Split(';');

            if (!scriptingDefines.Contains(MLS_INCLUDED_DEFINE))
            {
                AddDefine(MLS_INCLUDED_DEFINE, false);
            }

            if (scriptingDefines.Contains(HDRP_7_PackageDefine) ||
                scriptingDefines.Contains(HDRP_8_PackageDefine) ||
                scriptingDefines.Contains(HDRP_9_PackageDefine) ||
                scriptingDefines.Contains(HDRP_10_PackageDefine) ||
                scriptingDefines.Contains(HDRP_11_PackageDefine) ||
                scriptingDefines.Contains(HDRP_12_PackageDefine) ||
                scriptingDefines.Contains(HDRP_13_PackageDefine) ||
                scriptingDefines.Contains(HDRP_14_PackageDefine) ||
                scriptingDefines.Contains(HDRP_16_PackageDefine))
            {
                hasHDRPDefine = true;
            }

            if (scriptingDefines.Contains(URP_7_PackageDefine) ||
                scriptingDefines.Contains(URP_8_PackageDefine) ||
                scriptingDefines.Contains(URP_9_PackageDefine) ||
                scriptingDefines.Contains(URP_10_PackageDefine) ||
                scriptingDefines.Contains(URP_11_PackageDefine) ||
                scriptingDefines.Contains(URP_12_PackageDefine) ||
                scriptingDefines.Contains(URP_13_PackageDefine) ||
                scriptingDefines.Contains(URP_14_PackageDefine) ||
                scriptingDefines.Contains(URP_16_PackageDefine) ||
                scriptingDefines.Contains(URP_17_PackageDefine))
            {
                hasURPDefine = true;
            }

            // HDRP Package Check
            if (systemProperties.highDefinitionRPActive && !hasHDRPDefine)
            {
                if (systemProperties.srpVersion.StartsWith("7"))
                {
                    AddDefine(HDRP_7_PackageDefine, false);
                }
                else if (systemProperties.srpVersion.StartsWith("8"))
                {
                    AddDefine(HDRP_8_PackageDefine, false);
                }
                else if (systemProperties.srpVersion.StartsWith("9"))
                {
                    AddDefine(HDRP_9_PackageDefine, false);
                }
                else if (systemProperties.srpVersion.StartsWith("10"))
                {
                    AddDefine(HDRP_10_PackageDefine, false);
                }
                else if (systemProperties.srpVersion.StartsWith("11"))
                {
                    AddDefine(HDRP_11_PackageDefine, false);
                }
                else if (systemProperties.srpVersion.StartsWith("12"))
                {
                    AddDefine(HDRP_12_PackageDefine, false);
                }
                else if (systemProperties.srpVersion.StartsWith("13"))
                {
                    AddDefine(HDRP_13_PackageDefine, false);
                }
                else if (systemProperties.srpVersion.StartsWith("14"))
                {
                    AddDefine(HDRP_14_PackageDefine, false);
                }
                else if (systemProperties.srpVersion.StartsWith("16"))
                {
                    AddDefine(HDRP_16_PackageDefine, false);
                }
            }
            else if (!systemProperties.highDefinitionRPActive && hasHDRPDefine)
            {
                if (scriptingDefines.Contains(HDRP_7_PackageDefine))
                {
                    RemoveDefine(HDRP_7_PackageDefine, false);
                }
                else if (scriptingDefines.Contains(HDRP_8_PackageDefine))
                {
                    RemoveDefine(HDRP_8_PackageDefine, false);
                }
                else if (scriptingDefines.Contains(HDRP_9_PackageDefine))
                {
                    RemoveDefine(HDRP_9_PackageDefine, false);
                }
                else if (scriptingDefines.Contains(HDRP_10_PackageDefine))
                {
                    RemoveDefine(HDRP_10_PackageDefine, false);
                }
                else if (scriptingDefines.Contains(HDRP_11_PackageDefine))
                {
                    RemoveDefine(HDRP_11_PackageDefine, false);
                }
                else if (scriptingDefines.Contains(HDRP_12_PackageDefine))
                {
                    RemoveDefine(HDRP_12_PackageDefine, false);
                }
                else if (scriptingDefines.Contains(HDRP_13_PackageDefine))
                {
                    RemoveDefine(HDRP_13_PackageDefine, false);
                }
                else if (scriptingDefines.Contains(HDRP_14_PackageDefine))
                {
                    RemoveDefine(HDRP_14_PackageDefine, false);
                }
                else if (scriptingDefines.Contains(HDRP_16_PackageDefine))
                {
                    RemoveDefine(HDRP_16_PackageDefine, false);
                }
            }

            // URP Package Check
            if (systemProperties.universalRPActive && !hasURPDefine)
            {
                if (systemProperties.srpVersion.StartsWith("7"))
                {
                    AddDefine(URP_7_PackageDefine, false);
                }
                else if (systemProperties.srpVersion.StartsWith("8"))
                {
                    AddDefine(URP_8_PackageDefine, false);
                }
                else if (systemProperties.srpVersion.StartsWith("9"))
                {
                    AddDefine(URP_9_PackageDefine, false);
                }
                else if (systemProperties.srpVersion.StartsWith("10"))
                {
                    AddDefine(URP_10_PackageDefine, false);
                }
                else if (systemProperties.srpVersion.StartsWith("11"))
                {
                    AddDefine(URP_11_PackageDefine, false);
                }
                else if (systemProperties.srpVersion.StartsWith("12"))
                {
                    AddDefine(URP_12_PackageDefine, false);
                }
                else if (systemProperties.srpVersion.StartsWith("13"))
                {
                    AddDefine(URP_13_PackageDefine, false);
                }
                else if (systemProperties.srpVersion.StartsWith("14"))
                {
                    AddDefine(URP_14_PackageDefine, false);
                }
                else if (systemProperties.srpVersion.StartsWith("16"))
                {
                    AddDefine(URP_16_PackageDefine, false);
                }
                else if (systemProperties.srpVersion.StartsWith("17"))
                {
                    AddDefine(URP_17_PackageDefine, false);
                }
            }
            else if (!systemProperties.universalRPActive && hasURPDefine)
            {
                if (scriptingDefines.Contains(URP_7_PackageDefine))
                {
                    RemoveDefine(URP_7_PackageDefine, false);
                }
                else if (scriptingDefines.Contains(URP_8_PackageDefine))
                {
                    RemoveDefine(URP_8_PackageDefine, false);
                }
                else if (scriptingDefines.Contains(URP_9_PackageDefine))
                {
                    RemoveDefine(URP_9_PackageDefine, false);
                }
                else if (scriptingDefines.Contains(URP_10_PackageDefine))
                {
                    RemoveDefine(URP_10_PackageDefine, false);
                }
                else if (scriptingDefines.Contains(URP_11_PackageDefine))
                {
                    RemoveDefine(URP_11_PackageDefine, false);
                }
                else if (scriptingDefines.Contains(URP_12_PackageDefine))
                {
                    RemoveDefine(URP_12_PackageDefine, false);
                }
                else if (scriptingDefines.Contains(URP_13_PackageDefine))
                {
                    RemoveDefine(URP_13_PackageDefine, false);
                }
                else if (scriptingDefines.Contains(URP_14_PackageDefine))
                {
                    RemoveDefine(URP_14_PackageDefine, false);
                }
                else if (scriptingDefines.Contains(URP_16_PackageDefine))
                {
                    RemoveDefine(URP_16_PackageDefine, false);
                }
                else if (scriptingDefines.Contains(URP_17_PackageDefine))
                {
                    RemoveDefine(URP_17_PackageDefine, false);
                }
            } 

            if (manual)
            {
                manual = false;
                EditorUtility.DisplayDialog("MLS Dependency Checker", "Scripting Define Symbols configured.", "OK");
            }
        }

        public static void Initialize() 
        {
            Application.runInBackground = true;

            string managedDir = "";

            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                managedDir = EditorApplication.applicationPath.Split(new string[] { "Unity.exe" }, StringSplitOptions.None)[0];
            }
            else
            {
                System.Reflection.Assembly entryAssembly = new System.Diagnostics.StackTrace().GetFrames().Last().GetMethod().Module.Assembly;
                managedDir = System.IO.Path.GetDirectoryName(entryAssembly.Location);
            }

            standard_RP_SourcesPath = managedDir + "Data/CGIncludes/";

            if (!Directory.Exists(standard_RP_SourcesPath))
            {
                standard_RP_SourcesPath = managedDir + "/../../CGIncludes/";
            }

            if (!Directory.Exists(standard_RP_SourcesPath))
            {
                UnityEngine.Debug.LogError("Can't find directory: " + standard_RP_SourcesPath);
                return;
            }

            string[] directories = Directory.GetDirectories(Application.dataPath, "Magic Lightmap Switcher", SearchOption.AllDirectories);
            string projectRelativePath = Application.dataPath + directories[0].Split(new string[] { "Magic Lightmap Switcher" }, StringSplitOptions.None)[1];
            
            standard_RP_ModifySourcesPath = directories[0] + "/Editor/Dependent Resources/Shader Sources/Standard/";

            GetSystemProperties();
            CheckSRPState();

            if (systemProperties.universalRPActive || systemProperties.highDefinitionRPActive)
            {
                GetInstalledSRPVersion();

                if (!systemProperties.srpVersion.Contains("7") &&
                    !systemProperties.srpVersion.Contains("8") &&
                    !systemProperties.srpVersion.Contains("9") &&
                    !systemProperties.srpVersion.Contains("10") &&
                    !systemProperties.srpVersion.Contains("11") &&
                    !systemProperties.srpVersion.Contains("12") &&
                    !systemProperties.srpVersion.Contains("13") &&
                    !systemProperties.srpVersion.Contains("14") &&
                    !systemProperties.srpVersion.Contains("16") &&
                    !systemProperties.srpVersion.Contains("17"))
                {
                    EditorUtility.DisplayDialog("Magic Lightmap Switcher",
                        "The SRP version is not supported by the plugin. " +
                        "Contact the developer using any of the available " +
                        "communication channels to quickly fix this problem.",
                        "OK");
                }
                
                if (systemProperties.universalRPActive)
                {
                    systemProperties.srp_ModifiedSourcesPathRoot =
                        directories[0] + "/Editor/Dependent Resources/Shader Sources/SRP/URP " + systemProperties.srpVersion.Split('.')[0];

                    systemProperties.srp_CORE_ModifySourcesPath = 
                        directories[0] + "/Editor/Dependent Resources/Shader Sources/SRP/URP " + systemProperties.srpVersion.Split('.')[0] + "/Core/";
                    systemProperties.srp_URP_COMMON_ModifySourcesPath = 
                        directories[0] + "/Editor/Dependent Resources/Shader Sources/SRP/URP " + systemProperties.srpVersion.Split('.')[0] + "/Common/";
                }

                if (systemProperties.highDefinitionRPActive)
                {
                    systemProperties.srp_ModifiedSourcesPathRoot =
                        directories[0] + "/Editor/Dependent Resources/Shader Sources/SRP/HDRP " + systemProperties.srpVersion.Split('.')[0];

                    systemProperties.srp_CORE_ModifySourcesPath = 
                        directories[0] + "/Editor/Dependent Resources/Shader Sources/SRP/HDRP " + systemProperties.srpVersion.Split('.')[0] + "/Core/";
                    systemProperties.srp_HDRP_COMMON_ModifySourcesPath = 
                        directories[0] + "/Editor/Dependent Resources/Shader Sources/SRP/HDRP " + systemProperties.srpVersion.Split('.')[0] + "/Common/";
                }
            }
            else
            {
                standard_RP_Patched = CheckForStandardRPPatched(false);
                systemProperties.standardRPPatched = standard_RP_Patched;
            }

            EditorUtility.SetDirty(systemProperties);
            initialized = true;
        }

        private static void PrintPatchReport(RenderPipelineType type, List<string> patchErrors = null)
        {
            if (patchErrors != null)
            {
                StringBuilder sb = new StringBuilder();
                string renderPipeline = "";

                for (int i = 0; i < patchErrors.Count; i++)
                {
                    if (i < patchErrors.Count - 1)
                    {
                        sb.Append(patchErrors[i] + ", ");
                    }
                    else
                    {
                        sb.Append(patchErrors[i]);
                    }
                }

                switch (type)
                {
                    case RenderPipelineType.Standard:
                        renderPipeline = "Render Pipeline: Standard";
                        break;
                    case RenderPipelineType.URP:
                        renderPipeline = "Render Pipeline: URP " + systemProperties.srpVersion;
                        break;
                    case RenderPipelineType.HDRP:
                        renderPipeline = "Render Pipeline: HDRP " + systemProperties.srpVersion;
                        break;
                }

                Debug.LogErrorFormat("" +
                    "<color=cyan>MLS:</color> Patch error of [" + sb.ToString() + "] " + "Unity version: " + Application.unityVersion.ToString() + ". " + renderPipeline +
                    "\r\n" +
                    "Show the text of this error to the developer.");
            }
            else
            {
                switch (type)
                {
                    case RenderPipelineType.Standard:
                        Debug.LogFormat("<color=cyan>MLS:</color> Standard Shaders patched successfully.");
                        break;
                    case RenderPipelineType.URP:
                        Debug.LogFormat("<color=cyan>MLS:</color> URP " + systemProperties.srpVersion + " patched successfully.");
                        break;
                    case RenderPipelineType.HDRP:
                        Debug.LogFormat("<color=cyan>MLS:</color> HDRP " + systemProperties.srpVersion + " patched successfully.");
                        break;
                }
            }
        }

        void OnInspectorUpdate()
        {
            Repaint();
        }

        private void OnGUI()
        {
            if (!initialized)
            {
                Initialize();
            }
            else
            {
                CheckSRPState();
            }

            GetSystemProperties();

            MLSEditorUtils.InitStyles();

            GUILayout.BeginVertical(GUI.skin.box);

            GUILayout.Label("These changes will be applied to the base Unity shaders.", MLSEditorUtils.captionStyle);

            GUILayout.Label(
                "Changes do not affect the basic functionality of the shaders, " +
                "they only contain some extras to support real-time lightmap blending.", MLSEditorUtils.labelCenteredStyle);

            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUI.skin.box);

            if ((!systemProperties.universalRPActive && !systemProperties.highDefinitionRPActive) || systemProperties.standardRPActive)
            {
                GUILayout.Label("Standard Shaders", MLSEditorUtils.caption_1_Style);

                if (File.Exists(standard_RP_SourcesPath + STANDARD__CORE__COMMON__INCLUDE_FILE))
                {
                    standard_RP_Patched = true;
                }
                else
                {
                    standard_RP_Patched = false;
                }

                if (standard_RP_Patched)
                {
                    systemProperties.standardRPPatched = true;

                    if (GUILayout.Button("Restore Standard RP Shaders", MLSEditorUtils.bigButtonStyle))
                    {
                        RestoreStandardRPShaders();
                        systemProperties.prevTimeSinceStartup = 0;
                        standard_RP_Patched = CheckForStandardRPPatched(false);
                    }
                }
                else
                {
                    systemProperties.standardRPPatched = false;

                    if (GUILayout.Button("Patch Standard RP Shaders", MLSEditorUtils.bigButtonStyle))
                    {
                        PatchStandardRPShaders();
                        systemProperties.editorRestarted = false;
                        systemProperties.prevTimeSinceStartup = 0;
                        standard_RP_Patched = CheckForStandardRPPatched(true);
                    }
                }

                systemProperties.standardRPActive = true;
            }
            else
            {
                if (GUILayout.Button("Use Standard RP", MLSEditorUtils.bigButtonStyle))
                {
                    systemProperties.standardRPActive = true;
                }

                GUILayout.Label("SRP Packages", MLSEditorUtils.caption_1_Style);
            }

            if (systemProperties.universalRPActive)
            {
                CheckForSRPPatched(RenderPipelineType.URP, false, false);

                systemProperties.universalRPActive = true;

                EditorGUILayout.HelpBox(
                "Your project contains a URP " + systemProperties.srpVersion + " package.", MessageType.Info);

                if (isCustom)
                {
                    GUILayout.Label("Is Custom SRP");
                }

                if (systemProperties.universalRPPatched)
                {
                    if (GUILayout.Button("Restore URP Shaders", MLSEditorUtils.bigButtonStyle))
                    {
                        systemProperties.universalRPPatched = false;
                        RestoreSRPShaders(RenderPipelineType.URP, true);
                    }
                }
                else
                {
                    if (!File.Exists(systemProperties.srp_ModifiedSourcesPathRoot + "/PATCH_LIST.txt"))
                    {
                        EditorGUILayout.HelpBox(
                            "You have not imported the URP support package. " +
                            "It is located in the Magic Lightmap Switcher/Support Packages folder", MessageType.Error);
                    }
                    else
                    {
                        if (GUILayout.Button(patch_URP_Button_Text, MLSEditorUtils.bigButtonStyle))
                        {
                            systemProperties.standardRPActive = false;
                            systemProperties.editorRestarted = true;
                            PrepareSRPPackage(RenderPipelineType.URP);
                        }                        
                    }
                }
            }

            if (systemProperties.highDefinitionRPActive)
            {
                EditorGUILayout.HelpBox(
                "Your project contains a HDRP " + systemProperties.srpVersion + " package.", MessageType.Info);

                if (systemProperties.highDefinitionRPPatched)
                {
                    if (GUILayout.Button("Restore HDRP Shaders", MLSEditorUtils.bigButtonStyle))
                    {
                        systemProperties.highDefinitionRPPatched = false;
                        RestoreSRPShaders(RenderPipelineType.HDRP, true);
                    }
                }
                else
                {
                    if (!File.Exists(systemProperties.srp_ModifiedSourcesPathRoot + "/PATCH_LIST.txt"))
                    {
                        EditorGUILayout.HelpBox(
                            "You have not imported the HDRP support package. " +
                            "It is located in the Magic Lightmap Switcher/Support Packages folder", MessageType.Error);
                    }
                    else
                    {
                        if (GUILayout.Button(patch_HDRP_Button_Text, MLSEditorUtils.bigButtonStyle))
                        {
                            systemProperties.standardRPActive = false;
                            systemProperties.editorRestarted = true;
                            PrepareSRPPackage(RenderPipelineType.HDRP);
                        }
                    }
                }
            }

            EditorUtility.SetDirty(systemProperties);

            GUILayout.EndVertical();

            if ((Event.current.type == EventType.Repaint))
            {
                Rect templateEnd = GUILayoutUtility.GetLastRect();

                if (shaderCodeModifierWindow != null)
                {
                    shaderCodeModifierWindow.minSize = new Vector2(shaderCodeModifierWindow.minSize.x, templateEnd.position.y + templateEnd.height);
                }
            }
        }

        private static bool CheckForStandardRPPatched(bool printReport)
        {
            int patchedFragments = 0;
            int filePatchedFragments = 0;
            List<string> patchErrors = new List<string>();

            try
            {
                using (StreamReader reader = new StreamReader(standard_RP_SourcesPath + STANDARD__GLOBAL_ILLUMINATION__DEFAULT_SOURCE_FILE))
                {
                    filePatchedFragments = 0;

                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();

                        if (!string.IsNullOrEmpty(line) &&
                            (STANDARD__GLOBAL_ILLUMINATION__INCLUDE_SECTION__SIGNATURE == line.Trim() ||
                            STANDARD__GLOBAL_ILLUMINATION__UNITY_GI_INDIRECT_SPECULAR__SIGNATURE == line.Trim() ||
                            STANDARD__GLOBAL_ILLUMINATION__UNITY_GI_BASE__SIGNATURE == line.Trim()))
                        {
                            patchedFragments++;
                            filePatchedFragments++;
                        }
                    }

                    if (filePatchedFragments != 3)
                    {
                        patchErrors.Add("GlobalIllumination.cginc");
                    }
                }

                using (StreamReader reader = new StreamReader(standard_RP_SourcesPath + STANDARD__UNITY_IMAGE_BASED_LIGHTING__DEFAULT_SOURCE_FILE))
                {
                    filePatchedFragments = 0;

                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();

                        if (!string.IsNullOrEmpty(line) &&
                            (STANDARD__UNITY_IMAGE_BASED_LIGHTING__UNITY_GLOSSY_ENVIRONMENT__SIGNATURE == line.Trim()))
                        {
                            patchedFragments++;
                        }
                    }

                    if (filePatchedFragments != 1)
                    {
                        patchErrors.Add("ImageBasedLighting.cginc");
                    }
                }

                using (StreamReader reader = new StreamReader(standard_RP_SourcesPath + STANDARD__UNITY_PBS_LIGHTING__DEFAULT_SOURCE_FILE))
                {
                    filePatchedFragments = 0;

                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();

                        if (!string.IsNullOrEmpty(line) &&
                            (STANDARD__UNITY_PBS_LIGHTING__LIGHTING_STANDARD__SIGNATURE == line.Trim()))
                        {
                            patchedFragments++;
                        }
                    }

                    if (filePatchedFragments != 1)
                    {
                        patchErrors.Add("PBSLighting.cginc");
                    }
                }

                using (StreamReader reader = new StreamReader(standard_RP_SourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE))
                {
                    filePatchedFragments = 0;

                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();

                        if (!string.IsNullOrEmpty(line) &&
                            (STANDARD__UNITY_SHADOW_LIBRARY__UNITY_GET_RAW_BAKED_OCCLUSIONS__SIGNATURE == line.Trim()))
                        {
                            patchedFragments++;
                        }
                    }

                    if (filePatchedFragments != 1)
                    {
                        patchErrors.Add("UnityShadowLibrary.cginc (Deferred Section)");
                    }
                }

                using (StreamReader reader = new StreamReader(standard_RP_SourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE))
                {
                    filePatchedFragments = 0;

                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();

                        if (!string.IsNullOrEmpty(line) &&
                            (STANDARD__UNITY_SHADOW_LIBRARY__UNITY_SAMPLE_BAKED_OCCLUSION__SIGNATURE == line.Trim()))
                        {
                            patchedFragments++;
                        }
                    }

                    if (filePatchedFragments != 1)
                    {
                        patchErrors.Add("UnityShadowLibrary.cginc (Forward Section)");
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.Log("The file could not be read: " + e.Message);
                return false;
            }

            if (patchedFragments == 10)
            {
                if (printReport)
                {
                    PrintPatchReport(RenderPipelineType.Standard);
                }

                return true;
            }
            else
            {
                if (printReport)
                {
                    PrintPatchReport(RenderPipelineType.Standard, patchErrors);
                }

                return false;
            }
        }

        static public EmbedRequest Request;

        static public void Embed(string inTarget)
        {
            // Embed a package in the project
            Debug.Log("Embed('" + inTarget + "') called");
            Request = Client.Embed(inTarget);
            EditorApplication.update += Progress;

        }

        static void Progress()
        {
            if (Request.IsCompleted)
            {
                if (Request.Status == StatusCode.Success)
                {
                    Debug.Log("Embedded: " + Request.Result.packageId);

                    var destDir = Request.Result.packageId.Split("@")[1].Split("file")[1].Substring(1);

                    systemProperties.srp_Core_SourcesPath = destDir;

                    if (Directory.Exists(destDir))
                    {
                        Debug.Log(destDir);
                    }
                }
                else if (Request.Status >= StatusCode.Failure)
                {
                    Debug.Log(Request.Error.message);
                }

                EditorApplication.update -= Progress;
            }
        }

        private static async Task<string> GetPackagePath(string packageName)
        {
            // Получить список всех пакетов
            var listRequest = Client.List(true, true);
            while (!listRequest.IsCompleted)
            {
                await Task.Yield();
            }

            if (listRequest.Status == StatusCode.Success)
            {
                foreach (var package in listRequest.Result)
                {
                    if (package.name == packageName)
                    {
                        if (!string.IsNullOrEmpty(package.assetPath))
                            return package.resolvedPath;

                        return package.resolvedPath;
                    }
                }
            }
            else
            {
                Debug.LogError($"Ошибка получения списка пакетов: {listRequest.Error.message}");
            }

            return null;
        }

        private static async void PrepareSRPPackage(RenderPipelineType srp)
        {
            var sourceDir = "";
            var destDir = "";
            var projectRoot = Directory.GetParent(Application.dataPath);

            switch (srp)
            {
                case RenderPipelineType.URP:
#if UNITY_6000_0_OR_NEWER
                    await Task.Yield();

                    if (Directory.Exists(projectRoot + "/Library/PackageCache/com.unity.render-pipelines.core"))
                    {
                        isCustom = false;
                    }
                    else if (Directory.Exists(projectRoot + "/Library/PackageCache/com.unity.render-pipelines.core"))
                    {
                        isCustom = true;
                    }

                    if (isCustom)
                    {
                        sourceDir = projectRoot + "/Library/PackageCache/com.unity.render-pipelines.core";
                        destDir = projectRoot + "/Packages/com.unity.render-pipelines.core";
                    }
                    else
                    {
                        sourceDir = await GetPackagePath("com.unity.render-pipelines.core");
                        destDir = projectRoot + "/Packages/com.unity.render-pipelines.core@" + systemProperties.srpVersion;

                        if (Directory.Exists(destDir))
                        {
                            Directory.Delete(destDir, true);
                        }

                        Directory.Move(sourceDir, destDir);
                    }

                    systemProperties.srp_Core_SourcesPath = destDir + "/ShaderLibrary/";

                    if (isCustom)
                    {
                        sourceDir = projectRoot + "/Library/PackageCache/com.unity.render-pipelines.universal";
                        destDir = projectRoot + "/Packages/com.unity.render-pipelines.universal";
                    }
                    else
                    {
                        sourceDir = await GetPackagePath("com.unity.render-pipelines.universal");
                        destDir = projectRoot + "/Packages/com.unity.render-pipelines.universal@" + systemProperties.srpVersion;

                        if (Directory.Exists(destDir))
                        {
                            Directory.Delete(destDir, true);
                        }

                        Directory.Move(sourceDir, destDir);
                    }

                    systemProperties.srp_PathRoot = projectRoot + "/Packages/";
                    systemProperties.srp_URP_SourcesPath = destDir + "/";
#else
                    await Task.Yield();

                    if (Directory.Exists(Application.dataPath + "/../Library/PackageCache/com.unity.render-pipelines.core@" + systemProperties.srpVersion))
                    {
                        isCustom = false;
                    }
                    else if (Directory.Exists(Application.dataPath + "/../Library/PackageCache/com.unity.render-pipelines.core"))
                    {
                        isCustom = true;
                    }

                    if (isCustom)
                    {
                        sourceDir = Application.dataPath + "/../Library/PackageCache/com.unity.render-pipelines.core";
                        destDir = Application.dataPath + "/../Packages/com.unity.render-pipelines.core";
                    }
                    else
                    {
                        sourceDir = await GetPackagePath("com.unity.render-pipelines.core");
                        destDir = Application.dataPath + "/../Packages/com.unity.render-pipelines.core@" + systemProperties.srpVersion;

                        if (Directory.Exists(destDir))
                        {
                            Directory.Delete(destDir, true);
                        }

                        Directory.Move(sourceDir, destDir);
                    }

                    systemProperties.srp_Core_SourcesPath = destDir + "/ShaderLibrary/";

                    if (isCustom)
                    {
                        sourceDir = Application.dataPath + "/../Library/PackageCache/com.unity.render-pipelines.universal";
                        destDir = Application.dataPath + "/../Packages/com.unity.render-pipelines.universal";
                    }
                    else
                    {
                        sourceDir = await GetPackagePath("com.unity.render-pipelines.universal");
                        destDir = Application.dataPath + "/../Packages/com.unity.render-pipelines.universal@" + systemProperties.srpVersion;

                        if (Directory.Exists(destDir))
                        {
                            Directory.Delete(destDir, true);
                        }

                        Directory.Move(sourceDir, destDir);
                    }

                    systemProperties.srp_PathRoot = Application.dataPath + "/../Packages/";
                    systemProperties.srp_URP_SourcesPath = destDir + "/";
#endif

                    PatchSRPShaders(RenderPipelineType.URP);
                    break;
                case RenderPipelineType.HDRP:
#if UNITY_6000_0_OR_NEWER
                    await Task.Yield();

                    if (Directory.Exists(projectRoot + "/Library/PackageCache/com.unity.render-pipelines.core"))
                    {
                        isCustom = false;
                    }
                    else if (Directory.Exists(projectRoot + "/Library/PackageCache/com.unity.render-pipelines.core"))
                    {
                        isCustom = true;
                    }

                    if (isCustom)
                    {
                        sourceDir = projectRoot + "/Library/PackageCache/com.unity.render-pipelines.core";
                        destDir = projectRoot + "/Packages/com.unity.render-pipelines.core";
                    }
                    else
                    {
                        sourceDir = await GetPackagePath("com.unity.render-pipelines.core");
                        destDir = projectRoot + "/Packages/com.unity.render-pipelines.core@" + systemProperties.srpVersion;

                        if (Directory.Exists(destDir))
                        {
                            Directory.Delete(destDir, true);
                        }

                        Directory.Move(sourceDir, destDir);
                    }

                    systemProperties.srp_Core_SourcesPath = destDir + "/ShaderLibrary/";

                    if (isCustom)
                    {
                        sourceDir = projectRoot + "/Library/PackageCache/com.unity.render-pipelines.high-definition";
                        destDir = projectRoot + "/Packages/com.unity.render-pipelines.high-definition";
                    }
                    else
                    {
                        sourceDir = await GetPackagePath("com.unity.render-pipelines.high-definition");
                        destDir = projectRoot + "/Packages/com.unity.render-pipelines.high-definition@" + systemProperties.srpVersion;

                        if (Directory.Exists(destDir))
                        {
                            Directory.Delete(destDir, true);
                        }

                        Directory.Move(sourceDir, destDir);
                    }

                    systemProperties.srp_PathRoot = projectRoot + "/Packages/";
                    systemProperties.srp_URP_SourcesPath = destDir + "/";
#else
                    await Task.Yield();

                    if (Directory.Exists(Application.dataPath + "/../Library/PackageCache/com.unity.render-pipelines.core@" + systemProperties.srpVersion))
                    {
                        isCustom = false;
                    }
                    else if (Directory.Exists(Application.dataPath + "/../Library/PackageCache/com.unity.render-pipelines.core"))
                    {
                        isCustom = true;
                    }

                    if (isCustom)
                    {
                        sourceDir = Application.dataPath + "/../Library/PackageCache/com.unity.render-pipelines.core";
                        destDir = Application.dataPath + "/../Packages/com.unity.render-pipelines.core";
                    }
                    else
                    {
                        sourceDir = await GetPackagePath("com.unity.render-pipelines.core");
                        destDir = Application.dataPath + "/../Packages/com.unity.render-pipelines.core@" + systemProperties.srpVersion;

                        if (Directory.Exists(destDir))
                        {
                            Directory.Delete(destDir, true);
                        }

                        Directory.Move(sourceDir, destDir);
                    }

                    systemProperties.srp_Core_SourcesPath = destDir + "/ShaderLibrary/";

                    if (isCustom)
                    {
                        sourceDir = Application.dataPath + "/../Library/PackageCache/com.unity.render-pipelines.high-definition";
                        destDir = Application.dataPath + "/../Packages/com.unity.render-pipelines.high-definition";
                    }
                    else
                    {
                        sourceDir = await GetPackagePath("com.unity.render-pipelines.high-definition");
                        destDir = Application.dataPath + "/../Packages/com.unity.render-pipelines.high-definition@" + systemProperties.srpVersion;

                        if (Directory.Exists(destDir))
                        {
                            Directory.Delete(destDir, true);
                        }

                        Directory.Move(sourceDir, destDir);
                    }

                    systemProperties.srp_PathRoot = Application.dataPath + "/../Packages/";
                    systemProperties.srp_URP_SourcesPath = destDir + "/";
#endif

                    PatchSRPShaders(RenderPipelineType.HDRP);
                    break;
            }
        }

        private static void CheckForSRPPatched(RenderPipelineType srp, bool printReport, bool needRestart = false)
        {
            if (!File.Exists(systemProperties.srp_ModifiedSourcesPathRoot + "/PATCH_LIST.txt"))
            {
                return;
            }

            List<string> patchErrors = new List<string>();
            
            using (StreamReader reader = new StreamReader(systemProperties.srp_ModifiedSourcesPathRoot + "/PATCH_LIST.txt"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var filePath = "";

                    if (!string.IsNullOrEmpty(line) && !line.StartsWith("#"))
                    {
                        filePath = line.Replace("version", systemProperties.srpVersion);
                        filePath = systemProperties.srp_PathRoot + "/" + filePath;

                        if (File.Exists(filePath))
                        {
                            if (!CheckForMLSTags(filePath, "<MLS"))
                            {
                                patchErrors.Add(filePath);
                            }
                        }
                        else
                        {
                            patchErrors.Add(filePath);
                        }
                    }
                }
            }

            if (patchErrors.Count == 0)
            {
                if (printReport)
                {
                    PrintPatchReport(srp);
                }

                switch (srp)
                {
                    case RenderPipelineType.URP:
                        systemProperties.universalRPPatched = true;
                        break;
                    case RenderPipelineType.HDRP:
                        systemProperties.highDefinitionRPPatched = true;
                        break;
                }

                if (needRestart)
                {
                    if (EditorUtility.DisplayDialog(
                            "Magic Lightmap Switcher",
                            "You must restart the editor for the changes to take effect.", "OK"))
                    {
                        systemProperties.useSwitchingOnly = false;
                        systemProperties.editorRestarted = true;
                        EditorApplication.OpenProject(Directory.GetCurrentDirectory());
                    }
                    else
                    {
                        systemProperties.useSwitchingOnly = false;
                        systemProperties.editorRestarted = false;
                        Debug.LogFormat("<color=cyan>MLS:</color> You won't be able to use lightmap blending and switching until the editor is restarted.");
                    }
                }
            }
            else
            {
                if (printReport)
                {
                    PrintPatchReport(srp, patchErrors);
                }

                switch (srp)
                {
                    case RenderPipelineType.URP:
                        systemProperties.universalRPPatched = false;
                        break;
                    case RenderPipelineType.HDRP:
                        systemProperties.highDefinitionRPPatched = false;
                        break;
                }
            }

            systemProperties.restoring = false;
        }

        private static void RunAsAdmin(List<CmdProcessArguments> argumentsList)
        {
            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.Append(@"/c ");

            for (int i = 0; i < argumentsList.Count; i++)
            {
                switch (argumentsList[i].commandType)
                {
                    case CmdProcessArguments.CommandType.Copy:
                        stringBuilder.Append("copy /y \"" + argumentsList[i].copyFrom.Replace("/", @"\") + "\" \"" + argumentsList[i].copyTo.Replace("/", @"\") + "\" && ");
                        break;
                    case CmdProcessArguments.CommandType.Delete:
                        stringBuilder.Append("del /q \"" + argumentsList[i].deleteFrom.Replace("/", @"\") + "\" && ");
                        break;
                }
            }

            stringBuilder.Append("pause");

            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();

            startInfo.FileName = "cmd.exe";
            startInfo.Verb = "runas";
            startInfo.UseShellExecute = true;
            startInfo.Arguments = stringBuilder.ToString();

            try
            {
                System.Diagnostics.Process process =  new System.Diagnostics.Process();

                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit();
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToString());
            }
        }

        private void PatchStandardRPShaders()
        {
            CmdProcessArguments arguments = new CmdProcessArguments();
            List<CmdProcessArguments> processArguments = new List<CmdProcessArguments>();

            standard__Unity_Image_Based_Lighting__Glossy_Environment_Lines.Clear();
            standard__Unity_PBS_Lighting__Lighting_Standard_Lines.Clear();
            standard__Global_Illumination__Unity_GI_Indirect_Specular_Lines.Clear();
            standard__Global_Illumination__Include_Section_Lines.Clear();
            standard__Global_Illumination__Unity_GI_Base_Lines.Clear();
            standard__Unity_Shadow_Library__Unity_Sample_Baked_Occlusion_Lines.Clear();
            standard__Unity_Shadow_Library__Unity_Get_Raw_Baked_Occlusions_Lines.Clear();
            includeSectionAdditions.Clear();
            fragmentGIBlendReflectionProbes.Clear();
            
            bool USEFTRACE = false;

            #region Read Modified Sources

            try
            {
                using (StreamReader reader = new StreamReader(standard_RP_ModifySourcesPath + STANDARD__GLOBAL_ILLUMINATION__INCLUDE_SECTION__MODYFI_SOURCE_FILE))
                {
                    while (!reader.EndOfStream)
                    {
                        standard__Global_Illumination__Include_Section_Lines.Add(reader.ReadLine());
                    }
                }

                using (StreamReader reader = new StreamReader(standard_RP_ModifySourcesPath + STANDARD__UNITY_PBS_LIGHTING__LIGHTING_STANDARD__MODYFI_SOURCE_FILE))
                {
                    while (!reader.EndOfStream)
                    {
                        standard__Unity_PBS_Lighting__Lighting_Standard_Lines.Add(reader.ReadLine());
                    }
                }

                using (StreamReader reader = new StreamReader(standard_RP_ModifySourcesPath + STANDARD__GLOBAL_ILLUMINATION__UNITY_GI_INDIRECT_SPECULAR__MODYFI_SOURCE_FILE))
                {
                    while (!reader.EndOfStream)
                    {
                        standard__Global_Illumination__Unity_GI_Indirect_Specular_Lines.Add(reader.ReadLine());
                    }
                }

                using (StreamReader reader = new StreamReader(standard_RP_ModifySourcesPath + STANDARD__UNITY_IMAGE_BASED_LIGHTING__UNITY_GLOSSY_ENVIRONMENT__MODYFI_SOURCE_FILE))
                {
                    while (!reader.EndOfStream)
                    {
                        standard__Unity_Image_Based_Lighting__Glossy_Environment_Lines.Add(reader.ReadLine());
                    }
                }

                using (StreamReader reader = new StreamReader(standard_RP_ModifySourcesPath + STANDARD__GLOBAL_ILLUMINATION__UNITY_GI_BASE__MODYFI_SOURCE_FILE))
                {
                    while (!reader.EndOfStream)
                    {
                        standard__Global_Illumination__Unity_GI_Base_Lines.Add(reader.ReadLine());
                    }
                }

                using (StreamReader reader = new StreamReader(standard_RP_ModifySourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__UNITY_GET_RAW_BAKED_OCCLUSIONS__MODYFI_SOURCE_FILE))
                {
                    while (!reader.EndOfStream)
                    {
                        standard__Unity_Shadow_Library__Unity_Get_Raw_Baked_Occlusions_Lines.Add(reader.ReadLine());
                    }
                }

                using (StreamReader reader = new StreamReader(standard_RP_ModifySourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__UNITY_SAMPLE_BAKED_OCCLUSION__MODYFI_SOURCE_FILE))
                {
                    while (!reader.EndOfStream)
                    {
                        standard__Unity_Shadow_Library__Unity_Sample_Baked_Occlusion_Lines.Add(reader.ReadLine());
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogErrorFormat("<color=cyan>MLS:</color> The file could not be read: " + e.Message);
            }

            #endregion

            #region Buckup Original Files

            if (!File.Exists(standard_RP_SourcesPath + STANDARD__CORE__COMMON__INCLUDE_FILE))
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    arguments.commandType = CmdProcessArguments.CommandType.Copy;
                    arguments.copyFrom = standard_RP_ModifySourcesPath + STANDARD__CORE__COMMON__INCLUDE_FILE;
                    arguments.copyTo = standard_RP_SourcesPath + STANDARD__CORE__COMMON__INCLUDE_FILE;

                    processArguments.Add(new CmdProcessArguments(arguments));
                }
                else
                {
                    File.Copy(standard_RP_ModifySourcesPath + STANDARD__CORE__COMMON__INCLUDE_FILE, standard_RP_SourcesPath + STANDARD__CORE__COMMON__INCLUDE_FILE);
                }
            }

            if (!File.Exists(standard_RP_SourcesPath + STANDARD__GLOBAL_ILLUMINATION__DEFAULT_SOURCE_FILE + "_original"))
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    arguments.commandType = CmdProcessArguments.CommandType.Copy;
                    arguments.copyFrom = standard_RP_SourcesPath + STANDARD__GLOBAL_ILLUMINATION__DEFAULT_SOURCE_FILE;
                    arguments.copyTo = standard_RP_SourcesPath + STANDARD__GLOBAL_ILLUMINATION__DEFAULT_SOURCE_FILE + "_original";

                    processArguments.Add(new CmdProcessArguments(arguments));
                }
                else
                {
                    File.Copy(standard_RP_SourcesPath + STANDARD__GLOBAL_ILLUMINATION__DEFAULT_SOURCE_FILE, standard_RP_SourcesPath + STANDARD__GLOBAL_ILLUMINATION__DEFAULT_SOURCE_FILE + "_original");
                }
            }

            if (!File.Exists(standard_RP_SourcesPath + STANDARD__UNITY_PBS_LIGHTING__DEFAULT_SOURCE_FILE + "_original"))
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    arguments.commandType = CmdProcessArguments.CommandType.Copy;
                    arguments.copyFrom = standard_RP_SourcesPath + STANDARD__UNITY_PBS_LIGHTING__DEFAULT_SOURCE_FILE;
                    arguments.copyTo = standard_RP_SourcesPath + STANDARD__UNITY_PBS_LIGHTING__DEFAULT_SOURCE_FILE + "_original";

                    processArguments.Add(new CmdProcessArguments(arguments));
                }
                else
                {
                    File.Copy(standard_RP_SourcesPath + STANDARD__UNITY_PBS_LIGHTING__DEFAULT_SOURCE_FILE, standard_RP_SourcesPath + STANDARD__UNITY_PBS_LIGHTING__DEFAULT_SOURCE_FILE + "_original");
                }
            }

            if (!File.Exists(standard_RP_SourcesPath + STANDARD__UNITY_IMAGE_BASED_LIGHTING__DEFAULT_SOURCE_FILE + "_original"))
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    arguments.commandType = CmdProcessArguments.CommandType.Copy;
                    arguments.copyFrom = standard_RP_SourcesPath + STANDARD__UNITY_IMAGE_BASED_LIGHTING__DEFAULT_SOURCE_FILE;
                    arguments.copyTo = standard_RP_SourcesPath + STANDARD__UNITY_IMAGE_BASED_LIGHTING__DEFAULT_SOURCE_FILE + "_original";

                    processArguments.Add(new CmdProcessArguments(arguments));
                }
                else
                {
                    File.Copy(standard_RP_SourcesPath + STANDARD__UNITY_IMAGE_BASED_LIGHTING__DEFAULT_SOURCE_FILE, standard_RP_SourcesPath + STANDARD__UNITY_IMAGE_BASED_LIGHTING__DEFAULT_SOURCE_FILE + "_original");
                }
            }

            if (!File.Exists(standard_RP_SourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE + "_original"))
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    arguments.commandType = CmdProcessArguments.CommandType.Copy;
                    arguments.copyFrom = standard_RP_SourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE;
                    arguments.copyTo = standard_RP_SourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE + "_original";

                    processArguments.Add(new CmdProcessArguments(arguments));
                }
                else
                {
                    File.Copy(standard_RP_SourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE, standard_RP_SourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE + "_original");
                }
            }

            RunAsAdmin(processArguments);

            #endregion

            #region Patch Shader Files

            List<string> fileLines = new List<string>();
            processArguments.Clear();

            try
            {
                #region Global Illumination

                using (StreamReader reader = new StreamReader(standard_RP_SourcesPath + STANDARD__GLOBAL_ILLUMINATION__DEFAULT_SOURCE_FILE))
                {
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();
                        
                        if (!USEFTRACE && !string.IsNullOrEmpty(line) && line.Trim() == "//<FTRACEV1.0>")
                        {
                            USEFTRACE = true;
                            
                            for (int i = 0; i < standard__Global_Illumination__Include_Section_Lines.Count; i++)
                            {
                                fileLines.Add(standard__Global_Illumination__Include_Section_Lines[i]);
                            }

                            fileLines.Add("");
                            fileLines.Add(line);
                            continue;
                        }

                        if (!USEFTRACE && !string.IsNullOrEmpty(line) && STANDARD__GLOBAL_ILLUMINATION__INCLUDE_SECTION__SEARCH_STRING.Contains(line.Trim()))
                        {
                            for (int i = 0; i < standard__Global_Illumination__Include_Section_Lines.Count; i++)
                            {
                                fileLines.Add(standard__Global_Illumination__Include_Section_Lines[i]);
                            }

                            fileLines.Add("");
                            fileLines.Add(line);
                            continue;
                        }

                        if (!string.IsNullOrEmpty(line) && STANDARD__GLOBAL_ILLUMINATION__UNITY_GI_INDIRECT_SPECULAR__SEARCH_STRING.Contains(line.Trim()))
                        {
                            if (line.Trim().Contains("env0"))
                            {
                                fileLines.Add("");

                                for (int i = 0; i < 28; i++)
                                {
                                    fileLines.Add(standard__Global_Illumination__Unity_GI_Indirect_Specular_Lines[i]);
                                }

                                fileLines.Add("");
                            }

                            if (line.Trim().Contains("env1"))
                            {
                                for (int i = 29; i < 56; i++)
                                {
                                    fileLines.Add(standard__Global_Illumination__Unity_GI_Indirect_Specular_Lines[i]);
                                }

                                fileLines.Add("");
                            }
                            continue;
                        }

                        if (!string.IsNullOrEmpty(line) && STANDARD__GLOBAL_ILLUMINATION__UNITY_GI_BASE__SEARCH_STRING.Contains(line.Trim()))
                        {
                            if (line.Trim().Contains("half4 bakedColorTex"))
                            {
                                fileLines.Add("");

                                for (int i = 0; i < 6; i++)
                                {
                                    fileLines.Add(standard__Global_Illumination__Unity_GI_Base_Lines[i]);
                                }

                                fileLines.Add("");
                            }

                            if (line.Trim().Contains("fixed4 bakedDirTex"))
                            {
                                for (int i = 7; i < 15; i++)
                                {
                                    fileLines.Add(standard__Global_Illumination__Unity_GI_Base_Lines[i]);
                                }

                                fileLines.Add("");
                            }
                            continue;
                        }

                        fileLines.Add(line);
                    }
                }

                if (!File.Exists(standard_RP_ModifySourcesPath + STANDARD__GLOBAL_ILLUMINATION__DEFAULT_SOURCE_FILE))
                {
                    File.Create(standard_RP_ModifySourcesPath + STANDARD__GLOBAL_ILLUMINATION__DEFAULT_SOURCE_FILE).Close();
                }

                using (StreamWriter writer = new StreamWriter(standard_RP_ModifySourcesPath + STANDARD__GLOBAL_ILLUMINATION__DEFAULT_SOURCE_FILE))
                {
                    for (int i = 0; i < fileLines.Count; i++)
                    {
                        writer.WriteLine(fileLines[i]);
                    }
                }

                USEFTRACE = false;
                fileLines.Clear();

                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    arguments.commandType = CmdProcessArguments.CommandType.Copy;
                    arguments.copyFrom = standard_RP_ModifySourcesPath + STANDARD__GLOBAL_ILLUMINATION__DEFAULT_SOURCE_FILE;
                    arguments.copyTo = standard_RP_SourcesPath + STANDARD__GLOBAL_ILLUMINATION__DEFAULT_SOURCE_FILE;

                    processArguments.Add(new CmdProcessArguments(arguments));
                }
                else
                {
                    File.Copy(
                        standard_RP_ModifySourcesPath + STANDARD__GLOBAL_ILLUMINATION__DEFAULT_SOURCE_FILE,
                        standard_RP_SourcesPath + STANDARD__GLOBAL_ILLUMINATION__DEFAULT_SOURCE_FILE);
                }

                #endregion

                #region PBS Lightnig

                using (StreamReader reader = new StreamReader(standard_RP_SourcesPath + STANDARD__UNITY_PBS_LIGHTING__DEFAULT_SOURCE_FILE))
                {
                    bool skipLine = false;

                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();

                        if (skipLine)
                        {
                            skipLine = false;
                            continue;
                        }

                        if (!string.IsNullOrEmpty(line) && STANDARD__UNITY_PBS_LIGHTING__LIGHTING_STANDARD_GI__SEARCH_STRING.Contains(line.Trim()))
                        {
                            skipLine = true;

                            fileLines.Add(line);
                            fileLines.Add("#endif");
                            fileLines.Add("");

                            for (int i = 0; i < standard__Unity_PBS_Lighting__Lighting_Standard_Lines.Count; i++)
                            {
                                fileLines.Add(standard__Unity_PBS_Lighting__Lighting_Standard_Lines[i]);
                            }

                            fileLines.Add("");
                            continue;
                        }

                        fileLines.Add(line);
                    }
                }

                if (!File.Exists(standard_RP_ModifySourcesPath + STANDARD__UNITY_PBS_LIGHTING__DEFAULT_SOURCE_FILE))
                {
                    File.Create(standard_RP_ModifySourcesPath + STANDARD__UNITY_PBS_LIGHTING__DEFAULT_SOURCE_FILE).Close();
                }

                using (StreamWriter writer = new StreamWriter(standard_RP_ModifySourcesPath + STANDARD__UNITY_PBS_LIGHTING__DEFAULT_SOURCE_FILE))
                {
                    for (int i = 0; i < fileLines.Count; i++)
                    {
                        writer.WriteLine(fileLines[i]);
                    }
                }

                fileLines.Clear();

                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    arguments.commandType = CmdProcessArguments.CommandType.Copy;
                    arguments.copyFrom = standard_RP_ModifySourcesPath + STANDARD__UNITY_PBS_LIGHTING__DEFAULT_SOURCE_FILE;
                    arguments.copyTo = standard_RP_SourcesPath + STANDARD__UNITY_PBS_LIGHTING__DEFAULT_SOURCE_FILE;

                    processArguments.Add(new CmdProcessArguments(arguments));
                }
                else
                {
                    File.Copy(
                        standard_RP_ModifySourcesPath + STANDARD__UNITY_PBS_LIGHTING__DEFAULT_SOURCE_FILE,
                        standard_RP_SourcesPath + STANDARD__UNITY_PBS_LIGHTING__DEFAULT_SOURCE_FILE);
                }

                #endregion

                #region Image Based Lighting

                using (StreamReader reader = new StreamReader(standard_RP_SourcesPath + STANDARD__UNITY_IMAGE_BASED_LIGHTING__DEFAULT_SOURCE_FILE))
                {
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();

                        if (!string.IsNullOrEmpty(line) && STANDARD__UNITY_IMAGE_BASED_LIGHTING__UNITY_GLOSSY_ENVIRONMENT__SEARCH_STRING == line.Trim())
                        {
                            fileLines.Add("");

                            for (int i = 0; i < standard__Unity_Image_Based_Lighting__Glossy_Environment_Lines.Count; i++)
                            {
                                fileLines.Add(standard__Unity_Image_Based_Lighting__Glossy_Environment_Lines[i]);
                            }

                            fileLines.Add("");
                            fileLines.Add(line);
                            continue;
                        }

                        fileLines.Add(line);
                    }
                }

                if (!File.Exists(standard_RP_ModifySourcesPath + STANDARD__UNITY_IMAGE_BASED_LIGHTING__DEFAULT_SOURCE_FILE))
                {
                    File.Create(standard_RP_ModifySourcesPath + STANDARD__UNITY_IMAGE_BASED_LIGHTING__DEFAULT_SOURCE_FILE).Close();
                }

                using (StreamWriter writer = new StreamWriter(standard_RP_ModifySourcesPath + STANDARD__UNITY_IMAGE_BASED_LIGHTING__DEFAULT_SOURCE_FILE))
                {
                    for (int i = 0; i < fileLines.Count; i++)
                    {
                        writer.WriteLine(fileLines[i]);
                    }
                }

                fileLines.Clear();

                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    arguments.commandType = CmdProcessArguments.CommandType.Copy;
                    arguments.copyFrom = standard_RP_ModifySourcesPath + STANDARD__UNITY_IMAGE_BASED_LIGHTING__DEFAULT_SOURCE_FILE;
                    arguments.copyTo = standard_RP_SourcesPath + STANDARD__UNITY_IMAGE_BASED_LIGHTING__DEFAULT_SOURCE_FILE;

                    processArguments.Add(new CmdProcessArguments(arguments));
                }
                else
                {
                    File.Copy(
                        standard_RP_ModifySourcesPath + STANDARD__UNITY_IMAGE_BASED_LIGHTING__DEFAULT_SOURCE_FILE,
                        standard_RP_SourcesPath + STANDARD__UNITY_IMAGE_BASED_LIGHTING__DEFAULT_SOURCE_FILE);
                }

                #endregion

                #region Shadow Library

                using (StreamReader reader = new StreamReader(standard_RP_SourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE))
                {
                    bool skipLine = false;
                    bool fragmentFound = false;
                    bool replaceStartLine = false;
                    bool replaceEndLine = false;
                    List<string> tempLines = new List<string>();

                    while (!reader.EndOfStream)
                    {
                        if (skipLine)
                        {
                            skipLine = false;
                            continue;
                        }

                        string line = reader.ReadLine();

                        if (fragmentFound)
                        {
                            if (!replaceStartLine)
                            {
                                if (!string.IsNullOrEmpty(line) && line.Trim() == "#if defined (SHADOWS_SHADOWMASK)")
                                {
                                    replaceStartLine = true;

                                    for (int i = 0; i < standard__Unity_Shadow_Library__Unity_Sample_Baked_Occlusion_Lines.Count; i++)
                                    {
                                        fileLines.Add(standard__Unity_Shadow_Library__Unity_Sample_Baked_Occlusion_Lines[i]);
                                    }
                                }
                                else
                                {
                                    fileLines.Add(line);
                                    continue;
                                }
                            }

                            if (!replaceEndLine)
                            {
                                if (!string.IsNullOrEmpty(line) && line.Trim() == "return saturate(dot(rawOcclusionMask, unity_OcclusionMaskSelector));")
                                {
                                    fragmentFound = false;
                                    replaceEndLine = true;
                                    continue;
                                }
                                else
                                {
                                    continue;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(line) && STANDARD__UNITY_SHADOW_LIBRARY__UNITY_SAMPLE_BAKED_OCCLUSION__SEARCH_STRING == line.Trim())
                        {
                            fileLines.Add(line);
                            fragmentFound = true;
                            continue;
                        }

                        fileLines.Add(line);
                    }
                }

                if (!File.Exists(standard_RP_ModifySourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE))
                {
                    File.Create(standard_RP_ModifySourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE).Close();
                }

                using (StreamWriter writer = new StreamWriter(standard_RP_ModifySourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE))
                {
                    for (int i = 0; i < fileLines.Count; i++)
                    {
                        writer.WriteLine(fileLines[i]);
                    }
                }

                fileLines.Clear();

                using (StreamReader reader = new StreamReader(standard_RP_ModifySourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE))
                {
                    bool skipLine = false;
                    bool fragmentFound = false;
                    bool replaceStartLine = false;
                    bool replaceEndLine = false;
                    bool isNGSSModified = false;

                    string target = "";

                    while (!reader.EndOfStream)
                    { 
                        if (skipLine)
                        {
                            skipLine = false;
                            continue;
                        }

                        string line = reader.ReadLine();
                        
                        if (!string.IsNullOrEmpty(line) && line.Trim() == "#ifdef USEFTRACE")
                        {
                            USEFTRACE = true;
                        }

                        if (!isNGSSModified)
                        {
                            if (!string.IsNullOrEmpty(line) && line.Trim() == "//NGSS SUPPORT")
                            {
                                isNGSSModified = true;
                                target = "return UNITY_SAMPLE_TEX2D_SAMPLER(unity_ShadowMask, unity_Lightmap, lightmapUV.xy);//Unity 2017 and below";
                            }
                            else
                            {
                                target = "return UNITY_SAMPLE_TEX2D(unity_ShadowMask, lightmapUV.xy);";
                            }
                        }

                        if (fragmentFound)
                        {
                            if (!replaceStartLine)
                            {
                                if (!string.IsNullOrEmpty(line) && line.Trim() == target)
                                {
                                    replaceStartLine = true;

                                    for (int i = 0; i < standard__Unity_Shadow_Library__Unity_Get_Raw_Baked_Occlusions_Lines.Count; i++)
                                    {
                                        fileLines.Add(standard__Unity_Shadow_Library__Unity_Get_Raw_Baked_Occlusions_Lines[i]);
                                    }

                                    if (USEFTRACE)
                                    {
                                        fileLines.Add("#endif");
                                    }
                                }
                                else
                                {
                                    fileLines.Add(line);
                                    continue;
                                }
                            }

                            if (!replaceEndLine)
                            {
                                if (!string.IsNullOrEmpty(line) && line.Trim() == "#else")
                                {
                                    fileLines.Add(line);
                                    fragmentFound = false;
                                    replaceEndLine = true;
                                    continue;
                                }
                                else
                                {
                                    continue;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(line) && STANDARD__UNITY_SHADOW_LIBRARY__UNITY_GET_RAW_BAKED_OCCLUSIONS__SEARCH_STRING == line.Trim())
                        {
                            fileLines.Add(line);
                            fragmentFound = true;
                            continue;
                        }

                        fileLines.Add(line);
                    }
                }

                using (StreamWriter writer = new StreamWriter(standard_RP_ModifySourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE))
                {
                    for (int i = 0; i < fileLines.Count; i++)
                    {
                        writer.WriteLine(fileLines[i]);
                    }
                }

                fileLines.Clear();

                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    arguments.commandType = CmdProcessArguments.CommandType.Copy;
                    arguments.copyFrom = standard_RP_ModifySourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE;
                    arguments.copyTo = standard_RP_SourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE;

                    processArguments.Add(new CmdProcessArguments(arguments));
                }
                else
                {
                    File.Copy(
                        standard_RP_ModifySourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE,
                        standard_RP_SourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE);
                }

                #endregion

                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    RunAsAdmin(processArguments);
                }

                if (File.Exists(standard_RP_ModifySourcesPath + STANDARD__GLOBAL_ILLUMINATION__DEFAULT_SOURCE_FILE))
                {
                    File.Delete(standard_RP_ModifySourcesPath + STANDARD__GLOBAL_ILLUMINATION__DEFAULT_SOURCE_FILE);
                }

                if (File.Exists(standard_RP_ModifySourcesPath + STANDARD__UNITY_PBS_LIGHTING__DEFAULT_SOURCE_FILE))
                {
                    File.Delete(standard_RP_ModifySourcesPath + STANDARD__UNITY_PBS_LIGHTING__DEFAULT_SOURCE_FILE);
                }

                if (File.Exists(standard_RP_ModifySourcesPath + STANDARD__UNITY_IMAGE_BASED_LIGHTING__DEFAULT_SOURCE_FILE))
                {
                    File.Delete(standard_RP_ModifySourcesPath + STANDARD__UNITY_IMAGE_BASED_LIGHTING__DEFAULT_SOURCE_FILE);
                }

                if (File.Exists(standard_RP_ModifySourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE))
                {
                    File.Delete(standard_RP_ModifySourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE);
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.Log("The file could not be read: " + e.Message);
            }
            #endregion

            if (EditorUtility.DisplayDialog(
                "Magic Lightmap Switcher",
                "You must restart the editor for the changes to take effect. Restart now?", "Yes", "No, I will do it later."))
            {
                systemProperties.editorRestarted = true;
                EditorApplication.OpenProject(Directory.GetCurrentDirectory());
            }
            else
            {
                systemProperties.editorRestarted = false;
                Debug.LogFormat("<color=cyan>MLS:</color> You won't be able to use lightmap blending and switching until the editor is restarted.");
            }
        }

        private static bool CheckForMLSTags(string path, string tag)
        {
            bool result = false;

            using (StreamReader reader = new StreamReader(path))
            {
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();

                    if (!string.IsNullOrEmpty(line) && line.Trim().Contains(tag))
                    {
                        //Debug.Log("File: " + path + " patched successfully.");
                        result = true;
                        break;
                    }
                }
            }

            return result;
        }

        private void RestoreStandardRPShaders()
        {
            CmdProcessArguments arguments;
            List<CmdProcessArguments> processArguments = new List<CmdProcessArguments>();

            if (File.Exists(standard_RP_SourcesPath + STANDARD__GLOBAL_ILLUMINATION__DEFAULT_SOURCE_FILE + "_original"))
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    arguments = new CmdProcessArguments();

                    arguments.commandType = CmdProcessArguments.CommandType.Delete;
                    arguments.deleteFrom = standard_RP_SourcesPath + STANDARD__GLOBAL_ILLUMINATION__DEFAULT_SOURCE_FILE;
                    processArguments.Add(new CmdProcessArguments(arguments));

                    arguments = new CmdProcessArguments();

                    arguments.commandType = CmdProcessArguments.CommandType.Copy;
                    arguments.copyFrom = standard_RP_SourcesPath + STANDARD__GLOBAL_ILLUMINATION__DEFAULT_SOURCE_FILE + "_original";
                    arguments.copyTo = standard_RP_SourcesPath + STANDARD__GLOBAL_ILLUMINATION__DEFAULT_SOURCE_FILE;
                    processArguments.Add(new CmdProcessArguments(arguments));

                    arguments = new CmdProcessArguments();

                    arguments.commandType = CmdProcessArguments.CommandType.Delete;
                    arguments.deleteFrom = standard_RP_SourcesPath + STANDARD__GLOBAL_ILLUMINATION__DEFAULT_SOURCE_FILE + "_original";
                    processArguments.Add(new CmdProcessArguments(arguments));
                }
                else
                {
                    File.Delete(standard_RP_SourcesPath + STANDARD__GLOBAL_ILLUMINATION__DEFAULT_SOURCE_FILE);
                    File.Copy(standard_RP_SourcesPath + STANDARD__GLOBAL_ILLUMINATION__DEFAULT_SOURCE_FILE + "_original", standard_RP_SourcesPath + STANDARD__GLOBAL_ILLUMINATION__DEFAULT_SOURCE_FILE);
                    File.Delete(standard_RP_SourcesPath + STANDARD__GLOBAL_ILLUMINATION__DEFAULT_SOURCE_FILE + "_original");
                    File.Delete(standard_RP_SourcesPath + STANDARD__GLOBAL_ILLUMINATION__DEFAULT_SOURCE_FILE + "_original.meta");
                }
            }

            if (File.Exists(standard_RP_SourcesPath + STANDARD__UNITY_PBS_LIGHTING__DEFAULT_SOURCE_FILE + "_original"))
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    arguments = new CmdProcessArguments();

                    arguments.commandType = CmdProcessArguments.CommandType.Delete;
                    arguments.deleteFrom = standard_RP_SourcesPath + STANDARD__UNITY_PBS_LIGHTING__DEFAULT_SOURCE_FILE;
                    processArguments.Add(new CmdProcessArguments(arguments));

                    arguments = new CmdProcessArguments();

                    arguments.commandType = CmdProcessArguments.CommandType.Copy;
                    arguments.copyFrom = standard_RP_SourcesPath + STANDARD__UNITY_PBS_LIGHTING__DEFAULT_SOURCE_FILE + "_original";
                    arguments.copyTo = standard_RP_SourcesPath + STANDARD__UNITY_PBS_LIGHTING__DEFAULT_SOURCE_FILE;
                    processArguments.Add(new CmdProcessArguments(arguments));

                    arguments = new CmdProcessArguments();

                    arguments.commandType = CmdProcessArguments.CommandType.Delete;
                    arguments.deleteFrom = standard_RP_SourcesPath + STANDARD__UNITY_PBS_LIGHTING__DEFAULT_SOURCE_FILE + "_original";
                    processArguments.Add(new CmdProcessArguments(arguments));
                }
                else
                {
                    File.Delete(standard_RP_SourcesPath + STANDARD__UNITY_PBS_LIGHTING__DEFAULT_SOURCE_FILE);
                    File.Copy(standard_RP_SourcesPath + STANDARD__UNITY_PBS_LIGHTING__DEFAULT_SOURCE_FILE + "_original", standard_RP_SourcesPath + STANDARD__UNITY_PBS_LIGHTING__DEFAULT_SOURCE_FILE);
                    File.Delete(standard_RP_SourcesPath + STANDARD__UNITY_PBS_LIGHTING__DEFAULT_SOURCE_FILE + "_original");
                    File.Delete(standard_RP_SourcesPath + STANDARD__UNITY_PBS_LIGHTING__DEFAULT_SOURCE_FILE + "_original.meta");
                }
            }

            if (File.Exists(standard_RP_SourcesPath + STANDARD__UNITY_IMAGE_BASED_LIGHTING__DEFAULT_SOURCE_FILE + "_original"))
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    arguments = new CmdProcessArguments();

                    arguments.commandType = CmdProcessArguments.CommandType.Delete;
                    arguments.deleteFrom = standard_RP_SourcesPath + STANDARD__UNITY_IMAGE_BASED_LIGHTING__DEFAULT_SOURCE_FILE;
                    processArguments.Add(new CmdProcessArguments(arguments));

                    arguments = new CmdProcessArguments();

                    arguments.commandType = CmdProcessArguments.CommandType.Copy;
                    arguments.copyFrom = standard_RP_SourcesPath + STANDARD__UNITY_IMAGE_BASED_LIGHTING__DEFAULT_SOURCE_FILE + "_original";
                    arguments.copyTo = standard_RP_SourcesPath + STANDARD__UNITY_IMAGE_BASED_LIGHTING__DEFAULT_SOURCE_FILE;
                    processArguments.Add(new CmdProcessArguments(arguments));

                    arguments = new CmdProcessArguments();

                    arguments.commandType = CmdProcessArguments.CommandType.Delete;
                    arguments.deleteFrom = standard_RP_SourcesPath + STANDARD__UNITY_IMAGE_BASED_LIGHTING__DEFAULT_SOURCE_FILE + "_original";
                    processArguments.Add(new CmdProcessArguments(arguments));
                }
                else
                {
                    File.Delete(standard_RP_SourcesPath + STANDARD__UNITY_IMAGE_BASED_LIGHTING__DEFAULT_SOURCE_FILE);
                    File.Copy(standard_RP_SourcesPath + STANDARD__UNITY_IMAGE_BASED_LIGHTING__DEFAULT_SOURCE_FILE + "_original", standard_RP_SourcesPath + STANDARD__UNITY_IMAGE_BASED_LIGHTING__DEFAULT_SOURCE_FILE);
                    File.Delete(standard_RP_SourcesPath + STANDARD__UNITY_IMAGE_BASED_LIGHTING__DEFAULT_SOURCE_FILE + "_original");
                    File.Delete(standard_RP_SourcesPath + STANDARD__UNITY_IMAGE_BASED_LIGHTING__DEFAULT_SOURCE_FILE + "_original.meta");
                }
            }

            if (File.Exists(standard_RP_SourcesPath + STANDARD__CORE__DEFAULT_SOURCE_FILE + "_original"))
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    arguments = new CmdProcessArguments();

                    arguments.commandType = CmdProcessArguments.CommandType.Delete;
                    arguments.deleteFrom = standard_RP_SourcesPath + STANDARD__CORE__DEFAULT_SOURCE_FILE;
                    processArguments.Add(new CmdProcessArguments(arguments));

                    arguments = new CmdProcessArguments();

                    arguments.commandType = CmdProcessArguments.CommandType.Copy;
                    arguments.copyFrom = standard_RP_SourcesPath + STANDARD__CORE__DEFAULT_SOURCE_FILE + "_original";
                    arguments.copyTo = standard_RP_SourcesPath + STANDARD__CORE__DEFAULT_SOURCE_FILE;
                    processArguments.Add(new CmdProcessArguments(arguments));

                    arguments = new CmdProcessArguments();

                    arguments.commandType = CmdProcessArguments.CommandType.Delete;
                    arguments.deleteFrom = standard_RP_SourcesPath + STANDARD__CORE__DEFAULT_SOURCE_FILE + "_original";
                    processArguments.Add(new CmdProcessArguments(arguments));
                }
                else
                {
                    File.Delete(standard_RP_SourcesPath + STANDARD__CORE__DEFAULT_SOURCE_FILE);
                    File.Copy(standard_RP_SourcesPath + STANDARD__CORE__DEFAULT_SOURCE_FILE + "_original", standard_RP_SourcesPath + STANDARD__CORE__DEFAULT_SOURCE_FILE);
                    File.Delete(standard_RP_SourcesPath + STANDARD__CORE__DEFAULT_SOURCE_FILE + "_original");
                    File.Delete(standard_RP_SourcesPath + STANDARD__CORE__DEFAULT_SOURCE_FILE + "_original.meta");
                }
            }

            if (File.Exists(standard_RP_SourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE + "_original"))
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    arguments = new CmdProcessArguments();

                    arguments.commandType = CmdProcessArguments.CommandType.Delete;
                    arguments.deleteFrom = standard_RP_SourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE;
                    processArguments.Add(new CmdProcessArguments(arguments));

                    arguments = new CmdProcessArguments();

                    arguments.commandType = CmdProcessArguments.CommandType.Copy;
                    arguments.copyFrom = standard_RP_SourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE + "_original";
                    arguments.copyTo = standard_RP_SourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE;
                    processArguments.Add(new CmdProcessArguments(arguments));

                    arguments = new CmdProcessArguments();

                    arguments.commandType = CmdProcessArguments.CommandType.Delete;
                    arguments.deleteFrom = standard_RP_SourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE + "_original";
                    processArguments.Add(new CmdProcessArguments(arguments));
                }
                else
                {
                    File.Delete(standard_RP_SourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE);
                    File.Copy(standard_RP_SourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE + "_original", standard_RP_SourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE);
                    File.Delete(standard_RP_SourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE + "_original");
                    File.Delete(standard_RP_SourcesPath + STANDARD__UNITY_SHADOW_LIBRARY__DEFAULT_SOURCE_FILE + "_original.meta");
                }
            }

            if (File.Exists(standard_RP_SourcesPath + STANDARD__CORE__COMMON__INCLUDE_FILE))
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    arguments = new CmdProcessArguments();

                    arguments.commandType = CmdProcessArguments.CommandType.Delete;
                    arguments.deleteFrom = standard_RP_SourcesPath + STANDARD__CORE__COMMON__INCLUDE_FILE;
                    processArguments.Add(new CmdProcessArguments(arguments));
                }
                else
                {
                    File.Delete(standard_RP_SourcesPath + STANDARD__CORE__COMMON__INCLUDE_FILE);
                }
            }

            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                RunAsAdmin(processArguments);
            }
        }

        private static void PatchSRPShaders(RenderPipelineType srp)
        {
            string[] pathSplit;
            string targetFile;
            string lastFileName = "NONE";
            
            bool srpCommonReplaced = false;
            bool srpEntityLightingReplaced = false;

            bool urpShadowsReplaced = false;
            bool urpLitShaderReplaced = false;
            bool urpTerrainShaderReplaced = false;
            bool urpLightingReplaced = false;

            bool hdrpLitShaderReplaced = false;
            bool hdrpTerrainShaderReplaced = false;
            bool hdrpLightingLoopReplaced = false;
            bool hdrpHDRISkyReplaced = false;
            bool hdrpBuiltInGIReplaced = false;

            GetInstalledSRPVersion();

            srpCommonReplaced = CheckForMLSTags(systemProperties.srp_Core_SourcesPath + systemProperties.SRP__COMMON__DEFAULT_SOURCE_FILE, "<MLS");
            srpEntityLightingReplaced = CheckForMLSTags(systemProperties.srp_Core_SourcesPath + systemProperties.SRP__ENTITY_LIGHTING__DEFAULT_SOURCE_FILE, "<MLS");

            using (StreamReader reader = new StreamReader(systemProperties.srp_ModifiedSourcesPathRoot + "/PATCH_LIST.txt"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var filePath = "";

                    if (!string.IsNullOrEmpty(line) && !line.StartsWith("#"))
                    {
                        filePath = line.Replace("version", systemProperties.srpVersion);
                        filePath = systemProperties.srp_PathRoot + "/" + filePath;

                        // Create backup file
                        File.Copy(filePath, filePath + "_backup");

                        if (line.Contains("core"))
                        {
                            // Create patched file
                            File.Copy(
                                systemProperties.srp_CORE_ModifySourcesPath + line.Split(new string[] { "version" }, StringSplitOptions.None)[1] + "_temp",
                                filePath,
                                true);
                        }

                        // Create patched file
                        switch (srp)
                        {
                            case RenderPipelineType.URP:
                                if (line.Contains("universal"))
                                {
                                    File.Copy(
                                        systemProperties.srp_URP_COMMON_ModifySourcesPath + line.Split(new string[] { "version" }, StringSplitOptions.None)[1] + "_temp",
                                        filePath,
                                        true);
                                }
                                
                            break;
                            case RenderPipelineType.HDRP:
                                if (line.Contains("high-definition"))
                                {
                                    File.Copy(
                                        systemProperties.srp_HDRP_COMMON_ModifySourcesPath + line.Split(new string[] { "version" }, StringSplitOptions.None)[1] + "_temp",
                                        filePath,
                                        true);
                                }
                            break;
                        }
                    }
                }
            }

            CheckForSRPPatched(srp, true, true);

            return;

            switch (srp)
            {
                case RenderPipelineType.URP:

                    if (systemProperties.srpVersion.Contains("12") ||
                        systemProperties.srpVersion.Contains("13") ||
                        systemProperties.srpVersion.Contains("14"))
                    {
                        systemProperties.URP__LIGHTING__DEFAULT_SOURCE_FILE = "GlobalIllumination.hlsl";
                    }
                    else
                    {
                        systemProperties.URP__LIGHTING__DEFAULT_SOURCE_FILE = "Lighting.hlsl";
                    }
                    urpLitShaderReplaced = CheckForMLSTags(systemProperties.srp_URP_SourcesPath + systemProperties.URP__LIT__DEFAULT_SOURCE_FILE, "<MLS");
                    urpTerrainShaderReplaced = CheckForMLSTags(systemProperties.srp_URP_SourcesPath + systemProperties.URP__TERRAIN_LIT__DEFAULT_SOURCE_FILE, "<MLS");
                    urpLightingReplaced = CheckForMLSTags(systemProperties.srp_URP_SourcesPath + "/ShaderLibrary/" + systemProperties.URP__LIGHTING__DEFAULT_SOURCE_FILE, "<MLS");
                    urpShadowsReplaced = CheckForMLSTags(systemProperties.srp_URP_SourcesPath + "/ShaderLibrary/" + systemProperties.URP__SHADOWS__DEFAULT_SOURCE_FILE, "<MLS");
                    break;
                case RenderPipelineType.HDRP:
                    hdrpLitShaderReplaced = CheckForMLSTags(systemProperties.srp_HDRP_SourcesPath + systemProperties.HDRP__LIT__DEFAULT_SOURCE_FILE, "<MLS");
                    hdrpTerrainShaderReplaced = CheckForMLSTags(systemProperties.srp_HDRP_SourcesPath + systemProperties.HDRP__TERRAIN_LIT__DEFAULT_SOURCE_FILE, "<MLS");
                    hdrpLightingLoopReplaced = CheckForMLSTags(systemProperties.srp_HDRP_SourcesPath + systemProperties.HDRP__LIGHTING_LOOP_DEF__DEFAULT_SOURCE_FILE, "<MLS");
                    hdrpBuiltInGIReplaced = CheckForMLSTags(systemProperties.srp_HDRP_SourcesPath + systemProperties.HDRP__BUILTINGI__DEFAULT_SOURCE_FILE, "<MLS");
                    hdrpHDRISkyReplaced = CheckForMLSTags(systemProperties.srp_HDRP_SourcesPath + systemProperties.HDRP__HDRI_SKY__DEFAULT_SOURCE_FILE, "<MLS");
                    break;
            }            

            #region Replace Shader Files
            try
            {
                if (!srpCommonReplaced)
                {
                    lastFileName = systemProperties.SRP__COMMON__DEFAULT_SOURCE_FILE;
                    
                    File.Copy(
                        systemProperties.srp_Core_SourcesPath + systemProperties.SRP__COMMON__DEFAULT_SOURCE_FILE,
                        systemProperties.srp_Core_SourcesPath + systemProperties.SRP__COMMON__DEFAULT_SOURCE_FILE + "_backup");
                    File.Copy(
                        systemProperties.srp_CORE_ModifySourcesPath + systemProperties.SRP__COMMON__DEFAULT_SOURCE_FILE,
                        systemProperties.srp_Core_SourcesPath + systemProperties.SRP__COMMON__DEFAULT_SOURCE_FILE,
                        true);
                }

                if (!srpEntityLightingReplaced)
                {
                    lastFileName = systemProperties.SRP__ENTITY_LIGHTING__DEFAULT_SOURCE_FILE;
                    
                    File.Copy(
                        systemProperties.srp_Core_SourcesPath + systemProperties.SRP__ENTITY_LIGHTING__DEFAULT_SOURCE_FILE,
                        systemProperties.srp_Core_SourcesPath + systemProperties.SRP__ENTITY_LIGHTING__DEFAULT_SOURCE_FILE + "_backup");
                    File.Copy(
                        systemProperties.srp_CORE_ModifySourcesPath + systemProperties.SRP__ENTITY_LIGHTING__DEFAULT_SOURCE_FILE,
                        systemProperties.srp_Core_SourcesPath + systemProperties.SRP__ENTITY_LIGHTING__DEFAULT_SOURCE_FILE,
                        true);
                }

                switch (srp)
                {
                    case RenderPipelineType.URP:
                        if (!urpLitShaderReplaced)
                        {
                            lastFileName = systemProperties.URP__LIT__DEFAULT_SOURCE_FILE;
                            
                            File.Copy(
                                systemProperties.srp_URP_SourcesPath + systemProperties.URP__LIT__DEFAULT_SOURCE_FILE,
                                systemProperties.srp_URP_SourcesPath + systemProperties.URP__LIT__DEFAULT_SOURCE_FILE + "_backup");
                            File.Copy(
                                systemProperties.srp_URP_Shaders_ModifySourcesPath + systemProperties.URP__LIT__MODYFIED_SOURCE_FILE + "_temp",
                                systemProperties.srp_URP_SourcesPath + systemProperties.URP__LIT__DEFAULT_SOURCE_FILE,
                                true);
                        }

                        if (!urpTerrainShaderReplaced)
                        {
                            lastFileName = systemProperties.URP__TERRAIN_LIT__DEFAULT_SOURCE_FILE;
                            
                            File.Copy(
                                systemProperties.srp_URP_SourcesPath + systemProperties.URP__TERRAIN_LIT__DEFAULT_SOURCE_FILE,
                                systemProperties.srp_URP_SourcesPath + systemProperties.URP__TERRAIN_LIT__DEFAULT_SOURCE_FILE + "_backup");
                            File.Copy(
                                systemProperties.srp_URP_Shaders_ModifySourcesPath + systemProperties.URP__TERRAIN_LIT__MODYFIED_SOURCE_FILE + "_temp",
                                systemProperties.srp_URP_SourcesPath + systemProperties.URP__TERRAIN_LIT__DEFAULT_SOURCE_FILE,
                                true);
                        }
                        
                        if (!urpLightingReplaced)
                        {
                            lastFileName = systemProperties.URP__LIGHTING__DEFAULT_SOURCE_FILE;
                            
                            File.Copy(
                                systemProperties.srp_URP_SourcesPath + "/ShaderLibrary/" + systemProperties.URP__LIGHTING__DEFAULT_SOURCE_FILE,
                                systemProperties.srp_URP_SourcesPath + "/ShaderLibrary/" + systemProperties.URP__LIGHTING__DEFAULT_SOURCE_FILE + "_backup");
                            File.Copy(
                                systemProperties.srp_URP_COMMON_ModifySourcesPath + systemProperties.URP__LIGHTING__DEFAULT_SOURCE_FILE,
                                systemProperties.srp_URP_SourcesPath + "/ShaderLibrary/" + systemProperties.URP__LIGHTING__DEFAULT_SOURCE_FILE,
                                true);
                        }
                        
                        if (!urpShadowsReplaced)
                        {
                            lastFileName = systemProperties.URP__SHADOWS__DEFAULT_SOURCE_FILE;
                            
                            File.Copy(
                                systemProperties.srp_URP_SourcesPath + "/ShaderLibrary/" + systemProperties.URP__SHADOWS__DEFAULT_SOURCE_FILE,
                                systemProperties.srp_URP_SourcesPath + "/ShaderLibrary/" + systemProperties.URP__SHADOWS__DEFAULT_SOURCE_FILE + "_backup");
                            File.Copy(
                                systemProperties.srp_URP_COMMON_ModifySourcesPath + systemProperties.URP__SHADOWS__DEFAULT_SOURCE_FILE,
                                systemProperties.srp_URP_SourcesPath + "/ShaderLibrary/" + systemProperties.URP__SHADOWS__DEFAULT_SOURCE_FILE,
                                true);
                        }
                        break;
                    case RenderPipelineType.HDRP:
                        if (!hdrpLitShaderReplaced)
                        {
                            pathSplit = systemProperties.HDRP__LIT__DEFAULT_SOURCE_FILE.Split('/');
                            targetFile = pathSplit[pathSplit.Length - 1];
                            
                            lastFileName = targetFile;
                            
                            File.Copy(
                                systemProperties.srp_HDRP_SourcesPath + systemProperties.HDRP__LIT__DEFAULT_SOURCE_FILE,
                                systemProperties.srp_HDRP_SourcesPath + systemProperties.HDRP__LIT__DEFAULT_SOURCE_FILE + "_backup");
                            File.Copy(
                                systemProperties.srp_HDRP_Shaders_ModifySourcesPath + targetFile + "_temp",
                                systemProperties.srp_HDRP_SourcesPath + systemProperties.HDRP__LIT__DEFAULT_SOURCE_FILE,
                                true);
                        }

                        if (!hdrpTerrainShaderReplaced)
                        {
                            pathSplit = systemProperties.HDRP__TERRAIN_LIT__DEFAULT_SOURCE_FILE.Split('/');
                            targetFile = pathSplit[pathSplit.Length - 1];
                            
                            lastFileName = targetFile;
                            
                            File.Copy(
                                systemProperties.srp_HDRP_SourcesPath + systemProperties.HDRP__TERRAIN_LIT__DEFAULT_SOURCE_FILE,
                                systemProperties.srp_HDRP_SourcesPath + systemProperties.HDRP__TERRAIN_LIT__DEFAULT_SOURCE_FILE + "_backup");
                            File.Copy(
                                systemProperties.srp_HDRP_Shaders_ModifySourcesPath + targetFile + "_temp",
                                systemProperties.srp_HDRP_SourcesPath + systemProperties.HDRP__TERRAIN_LIT__DEFAULT_SOURCE_FILE,
                                true);
                        }
                        
                        if (!hdrpHDRISkyReplaced)
                        {
                            pathSplit = systemProperties.HDRP__HDRI_SKY__DEFAULT_SOURCE_FILE.Split('/');
                            targetFile = pathSplit[pathSplit.Length - 1];
                            
                            lastFileName = targetFile;
                            
                            File.Copy(
                                systemProperties.srp_HDRP_SourcesPath + systemProperties.HDRP__HDRI_SKY__DEFAULT_SOURCE_FILE,
                                systemProperties.srp_HDRP_SourcesPath + systemProperties.HDRP__HDRI_SKY__DEFAULT_SOURCE_FILE + "_backup");
                            File.Copy(
                                systemProperties.srp_HDRP_Shaders_ModifySourcesPath + targetFile + "_temp",
                                systemProperties.srp_HDRP_SourcesPath + systemProperties.HDRP__HDRI_SKY__DEFAULT_SOURCE_FILE,
                                true);
                        }

                        if (!hdrpLightingLoopReplaced)
                        {
                            pathSplit = systemProperties.HDRP__LIGHTING_LOOP_DEF__DEFAULT_SOURCE_FILE.Split('/');
                            targetFile = pathSplit[pathSplit.Length - 1];
                            
                            lastFileName = targetFile;
                            
                            File.Copy(
                                systemProperties.srp_HDRP_SourcesPath + systemProperties.HDRP__LIGHTING_LOOP_DEF__DEFAULT_SOURCE_FILE,
                                systemProperties.srp_HDRP_SourcesPath + systemProperties.HDRP__LIGHTING_LOOP_DEF__DEFAULT_SOURCE_FILE + "_backup");
                            File.Copy(
                                systemProperties.srp_HDRP_COMMON_ModifySourcesPath + targetFile,
                                systemProperties.srp_HDRP_SourcesPath + systemProperties.HDRP__LIGHTING_LOOP_DEF__DEFAULT_SOURCE_FILE,
                                true);
                        }

                        if (!hdrpBuiltInGIReplaced)
                        {
                            pathSplit = systemProperties.HDRP__BUILTINGI__DEFAULT_SOURCE_FILE.Split('/');
                            targetFile = pathSplit[pathSplit.Length - 1];
                            
                            lastFileName = targetFile;
                            
                            File.Copy(
                                systemProperties.srp_HDRP_SourcesPath + systemProperties.HDRP__BUILTINGI__DEFAULT_SOURCE_FILE,
                                systemProperties.srp_HDRP_SourcesPath + systemProperties.HDRP__BUILTINGI__DEFAULT_SOURCE_FILE + "_backup");
                            File.Copy(
                                systemProperties.srp_HDRP_COMMON_ModifySourcesPath + targetFile,
                                systemProperties.srp_HDRP_SourcesPath + systemProperties.HDRP__BUILTINGI__DEFAULT_SOURCE_FILE,
                                true);
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.Log("The file could not be read: " + lastFileName);
            }
#endregion
        }

        private void RestoreSRPShaders(RenderPipelineType srp, bool restoring)
        {
            string sourceDir = "";
            string destDir = "";

            switch (srp)
            {
                case RenderPipelineType.URP:
                    try
                    {
                        sourceDir = Application.dataPath + "/../Packages/com.unity.render-pipelines.core@" + systemProperties.srpVersion;
                        destDir = Application.dataPath + "/../Library/PackageCache/com.unity.render-pipelines.core@" + systemProperties.srpVersion;


                        Directory.Move(sourceDir, destDir);

                        sourceDir = Application.dataPath + "/../Packages/com.unity.render-pipelines.universal@" + systemProperties.srpVersion;
                        destDir = Application.dataPath + "/../Library/PackageCache/com.unity.render-pipelines.universal@" + systemProperties.srpVersion;

                        Directory.Move(sourceDir, destDir);
                    }
                    catch (Exception ex)
                    {
                        EditorUtility.DisplayDialog(
                            "Magic Lightmap Switcher",
                            "The operation cannot be performed because the file or folder " +
                            "related to SRP is open in another application. This is most " +
                            "likely your IDE, close it and try again. \r\n\r\n" +
                            "Exeption message: " + ex.Message, "OK");

                        return;
                    }

                    if (EditorUtility.DisplayDialog(
                        "Magic Lightmap Switcher",
                        "You must restart the editor for the changes to take effect.", "OK"))
                    {
                        systemProperties.editorRestarted = true;
                        EditorApplication.OpenProject(Directory.GetCurrentDirectory());
                    }
                    else
                    {
                        systemProperties.editorRestarted = false;
                        Debug.LogFormat("<color=cyan>MLS:</color> You won't be able to use lightmap blending and switching until the editor is restarted.");
                    }                    
                    break;
                case RenderPipelineType.HDRP:
                    sourceDir = Application.dataPath + "/../Packages/com.unity.render-pipelines.core@" + systemProperties.srpVersion;
                    destDir = Application.dataPath + "/../Library/PackageCache/com.unity.render-pipelines.core@" + systemProperties.srpVersion;

                    Directory.Move(sourceDir, destDir);

                    sourceDir = Application.dataPath + "/../Packages/com.unity.render-pipelines.high-definition@" + systemProperties.srpVersion;
                    destDir = Application.dataPath + "/../Library/PackageCache/com.unity.render-pipelines.high-definition@" + systemProperties.srpVersion;

                    Directory.Move(sourceDir, destDir);

                    if (EditorUtility.DisplayDialog(
                        "Magic Lightmap Switcher",
                        "You must restart the editor for the changes to take effect.", "OK"))
                    {
                        systemProperties.editorRestarted = true;
                        EditorApplication.OpenProject(Directory.GetCurrentDirectory());
                    }
                    else
                    {
                        systemProperties.editorRestarted = false;
                        Debug.LogFormat("<color=cyan>MLS:</color> You won't be able to use lightmap blending and switching until the editor is restarted.");
                    }
                    break;
            }            
        }        

        private static bool DoesTypeExist(string className)
        {
            var foundType = (from assembly in AppDomain.CurrentDomain.GetAssemblies()
                             from type in GetTypesSafe(assembly)
                             where type.Name == className
                             select type).FirstOrDefault();

            return foundType != null;
        }

        private static IEnumerable<Type> GetTypesSafe(System.Reflection.Assembly assembly)
        {
            Type[] types;

            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types;
            }

            return types.Where(x => x != null);
        }

        static private void RemoveDefine(string define, bool processLockFile = true)
        {
            if (processLockFile)
            {
                string[] directories = Directory.GetDirectories(Application.dataPath, "Magic Lightmap Switcher", SearchOption.AllDirectories);

                if (File.Exists(directories[0] + "/MLS_lock"))
                {
                    File.Delete(directories[0] + "/MLS_lock");
                }
            }

            string scriptingDefine = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
            string[] scriptingDefines = scriptingDefine.Split(';');
            List<string> listDefines = scriptingDefines.ToList();
            listDefines.Remove(define);

            string newDefines = string.Join(";", listDefines.ToArray());
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, newDefines);
        }

        static private void AddDefine(string define, bool processLockFile = true)
        {
            if (processLockFile)
            {
                string[] directories = Directory.GetDirectories(Application.dataPath, "Magic Lightmap Switcher", SearchOption.AllDirectories);

                if (!File.Exists(directories[0] + "/MLS_lock"))
                {
                    File.Create(directories[0] + "/MLS_lock");
                }
            }

            string scriptingDefine = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
            string[] scriptingDefines = scriptingDefine.Split(';');
            List<string> listDefines = scriptingDefines.ToList();
            listDefines.Add(define);

            string newDefines = string.Join(";", listDefines.ToArray());
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, newDefines);
        }
    }
}