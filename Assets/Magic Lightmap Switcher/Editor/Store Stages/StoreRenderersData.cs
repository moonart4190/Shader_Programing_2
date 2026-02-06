#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static MagicLightmapSwitcher.StoredLightmapData;

namespace MagicLightmapSwitcher
{
    public class StoreRenderersData
    {
        public IEnumerator Execute(StoredLightmapData lightmapData, MagicLightmapSwitcher mainComponent)
        {
            MLSProgressBarHelper.StartNewStage("Storing Renderers Data...");

            yield return null;

            Object[] gameObjects = Object.FindObjectsOfType(typeof(GameObject));
            List<List<Renderer>> prefabsRenderers = new List<List<Renderer>>();

            // foreach (var o in gameObjects)
            // {
            //     var go = (GameObject) o;
            //
            //     if (PrefabUtility.IsAnyPrefabInstanceRoot(go))
            //     {
            //         MLSPrefab mlsPrefab = go.GetComponent<MLSPrefab>();
            //         
            //         if (mlsPrefab == null)
            //         {
            //             mlsPrefab = go.AddComponent<MLSPrefab>();
            //         }
            //
            //         if (!mlsPrefab.dataStored)
            //         {
            //             List<MeshRenderer> test = new List<MeshRenderer>(
            //                 go.GetComponentsInChildren<MeshRenderer>());
            //             prefabsRenderers.Add(
            //                 new List<MeshRenderer>(test));
            //         }
            //     }
            // }
            
            Object[] renderers = Object.FindObjectsOfType(typeof(Renderer));

            List<RendererData> renderersDataTemp = new List<RendererData>();
            List<GameObjectsData> gameObjectsDataTemp = new List<GameObjectsData>();

            string fullStorePath = mainComponent.currentDataPath + "/AdditionalObjectDataMap.png";
            Texture2D mapTexture = new Texture2D(2048, 2048);

            foreach (var o in renderers)
            {
                var renderer = (Renderer) o;
                
                if (renderer.GetComponent<MLSStaticRenderer>() == null)
                {
                    if (!renderer.enabled)
                    {
                        continue;
                    }
                }

                if (renderer.GetComponent<Renderer>() != null)
                {
#if BAKERY_INCLUDED
                    if (renderer.gameObject.GetComponent<BakeryLightMesh>() != null)
                    {
                        continue;
                    }
#endif  
                    RendererData rendererData = new RendererData();

                    MLSStaticRenderer staticRenderer = null;
                    MLSDynamicRenderer dynamicRenderer = null;

                    if (MagicLightmapSwitcher.CheckIfStatic(renderer.gameObject))
                    {
                        if (renderer.gameObject.GetComponent<MLSStaticRenderer>() == null)
                        {
                            if (renderer.gameObject.GetComponent<MLSDynamicRenderer>() != null)
                            {
                                Object.DestroyImmediate(renderer.gameObject.GetComponent<MLSDynamicRenderer>());
                            }
#if BAKERY_INCLUDED
                            if (renderer.gameObject.GetComponent<BakeryLightMesh>() == null)
                            {
                                staticRenderer = renderer.gameObject.AddComponent<MLSStaticRenderer>();
                                staticRenderer.isStatic = true;
                                staticRenderer.UpdateGUID();
                            }
#else
                                staticRenderer = renderer.gameObject.AddComponent<MLSStaticRenderer>();
                                staticRenderer.isStatic = true;
                                staticRenderer.UpdateGUID();
#endif
                        }
                        else
                        {
                            staticRenderer = renderer.gameObject.GetComponent<MLSStaticRenderer>();
                        }
                    }
                    else
                    {
                        if (renderer.gameObject.GetComponent<MLSDynamicRenderer>() == null)
                        {
                            if (renderer.gameObject.GetComponent<MLSStaticRenderer>() != null)
                            {
                                Object.DestroyImmediate(renderer.gameObject.GetComponent<MLSStaticRenderer>());
                            }
#if BAKERY_INCLUDED
                            if (renderer.gameObject.GetComponent<BakeryLightMesh>() == null)
                            {
                                dynamicRenderer = renderer.gameObject.AddComponent<MLSDynamicRenderer>();
                                dynamicRenderer.UpdateGUID();
                            }
#else
                            dynamicRenderer = renderer.gameObject.AddComponent<MLSDynamicRenderer>();
                            dynamicRenderer.UpdateGUID();
#endif
                        }
                        else
                        {
                            dynamicRenderer = renderer.gameObject.GetComponent<MLSDynamicRenderer>();

                            if (renderersDataTemp.Find(item => item.objectId == dynamicRenderer.scriptId) != null)
                            {
                                dynamicRenderer.UpdateGUID();
                            }
                        }

                        rendererData.objectId = dynamicRenderer.scriptId;
                        rendererData.rotation = renderer.gameObject.transform.rotation;
                        rendererData.position = renderer.gameObject.transform.position;

                        if (dynamicRenderer.haveTrackableShaderProps)
                        {
                            rendererData.rendererShaderProperties = StoreMaterialData(rendererData, renderer);
                        }
                    }

                    if (staticRenderer != null)
                    {
                        if (renderersDataTemp.Find(item => item.objectId == staticRenderer.scriptId) != null)
                        {
                            staticRenderer.UpdateGUID();
                        }

                        var bakedSkinnedMesh = staticRenderer.gameObject.GetComponent<MLSBakedSkinnedMesh>();

                        if (bakedSkinnedMesh != null)
                        {
                            renderer.lightmapIndex = bakedSkinnedMesh.copyLightmapDataFrom.lightmapIndex;
                            renderer.lightmapScaleOffset = bakedSkinnedMesh.copyLightmapDataFrom.lightmapScaleOffset;

                            rendererData.objectId = staticRenderer.scriptId;
                            rendererData.lightmapIndex = renderer.lightmapIndex;
                            rendererData.lightmapScaleOffset = renderer.lightmapScaleOffset;
                            rendererData.rotation = renderer.gameObject.transform.rotation;
                            rendererData.position = renderer.gameObject.transform.position;
                        }
                        else
                        {
                            rendererData.objectId = staticRenderer.scriptId;
                            rendererData.lightmapIndex = renderer.lightmapIndex;
                            rendererData.lightmapScaleOffset = renderer.lightmapScaleOffset;
                            rendererData.rotation = renderer.gameObject.transform.rotation;
                            rendererData.position = renderer.gameObject.transform.position;
                        }

                        //mapTexture.SetPixel();

                        //Доработать до нескольких материалов

                        if (staticRenderer.haveTrackableShaderProps)
                        {
                           rendererData.rendererShaderProperties = StoreMaterialData(rendererData, renderer);
                        }

                        for (int i = 0; i < prefabsRenderers.Count; i++)
                        {
                            if (prefabsRenderers[i].Contains(renderer))
                            {
                                renderer.GetComponentInParent<MLSPrefab>().renderers.Add(rendererData);
                            }
                        }
                    }

                    renderersDataTemp.Add(rendererData);
                }

                if (UnityEditorInternal.InternalEditorUtility.isApplicationActive)
                {
                    if (MLSProgressBarHelper.UpdateProgress(renderers.Length, 0))
                    {
                        yield return null;
                    }
                }
            }

            lightmapData.sceneLightingData.rendererDatas = renderersDataTemp.ToArray();

            MLSLightmapDataStoring.stageExecuting = false;
        }

        public bool CheckIfContributeGI(GameObject gameObject)
        {
            bool isStatic = false;
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(gameObject);

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

        public List<RendererData.RendererShaderProperty> StoreMaterialData(RendererData rendererData, Renderer renderer)
        {
            MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();

            renderer.GetPropertyBlock(materialPropertyBlock);

            for (int p = 0; p < renderer.sharedMaterial.shader.GetPropertyCount(); p++)
            {
                var properties = new RendererData.RendererShaderProperty();

                properties.Create(renderer.sharedMaterial, renderer.sharedMaterial.shader, p);
                rendererData.rendererShaderProperties.Add(properties);
            }

            for (int p = 0; p < rendererData.rendererShaderProperties.Count; p++)
            {
                switch (rendererData.rendererShaderProperties[p].type)
                {
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                        rendererData.rendererShaderProperties[p].floatValue =
                            materialPropertyBlock.GetFloat(rendererData.rendererShaderProperties[p].name);
                        break;
                    case ShaderPropertyType.Color:
                        rendererData.rendererShaderProperties[p].colorValue =
                            materialPropertyBlock.GetColor(rendererData.rendererShaderProperties[p].name);
                        break;
                    case ShaderPropertyType.Texture:
                        rendererData.rendererShaderProperties[p].textureValue =
                            materialPropertyBlock.GetTexture(rendererData.rendererShaderProperties[p].name);
                        break;
                }
            }

            return rendererData.rendererShaderProperties;
        }
    }
}
#endif