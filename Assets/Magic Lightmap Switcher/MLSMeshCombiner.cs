#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MagicLightmapSwitcher
{
    public static class MLSMeshCombiner
    {
        private static bool createMultiMaterialMesh = false, combineInactiveChildren = false, deactivateCombinedChildren = true,
        deactivateCombinedChildrenMeshRenderers = false, generateUVMap = false, destroyCombinedChildren = false;
        private static List<List<MagicLightmapSwitcher.AffectedObject>> _sortedByDistanceObjects = new List<List<MagicLightmapSwitcher.AffectedObject>>();
        private static List<List<MagicLightmapSwitcher.AffectedObject>> _singleMaterialObjectsForBatching = new List<List<MagicLightmapSwitcher.AffectedObject>>();
        private static List<List<MagicLightmapSwitcher.AffectedObject>> _multiMaterialObjectsForBatching = new List<List<MagicLightmapSwitcher.AffectedObject>>();        

        public static bool CreateMultiMaterialMesh
        {
            get
            {
                return createMultiMaterialMesh;
            }

            set
            {
                createMultiMaterialMesh = value;
            }
        }

        public static bool CombineInactiveChildren
        {
            get
            {
                return combineInactiveChildren;
            }

            set

            {
                combineInactiveChildren = value;
            }
        }

        public static bool DeactivateCombinedChildren
        {
            get
            {
                return deactivateCombinedChildren;
            }

            set
            {
                deactivateCombinedChildren = value;
                CheckDeactivateCombinedChildren();
            }
        }

        public static bool DeactivateCombinedChildrenMeshRenderers
        {
            get
            {
                return deactivateCombinedChildrenMeshRenderers;
            }

            set
            {
                deactivateCombinedChildrenMeshRenderers = value;
                CheckDeactivateCombinedChildren();
            }
        }

        public static bool GenerateUVMap
        {
            get
            {
                return generateUVMap;
            }

            set
            {
                generateUVMap = value;
            }
        }

        public static bool DestroyCombinedChildren
        {
            get
            {
                return destroyCombinedChildren;
            }

            set
            {
                destroyCombinedChildren = value;
                CheckDestroyCombinedChildren();
            }
        }

        private static void CheckDeactivateCombinedChildren()
        {
            if (deactivateCombinedChildren || deactivateCombinedChildrenMeshRenderers)
            {
                destroyCombinedChildren = false;
            }
        }

        private static void CheckDestroyCombinedChildren()
        {
            if (destroyCombinedChildren)
            {
                deactivateCombinedChildren = false;
                deactivateCombinedChildrenMeshRenderers = false;
            }
        }   

        public static void CreateStaticMeshesBatch(MagicLightmapSwitcher swithcerInstance, List<MagicLightmapSwitcher.AffectedObject> staticObjectsList)
        {
            CreateCombineList(swithcerInstance, staticObjectsList);

            for (int i = 0; i < _singleMaterialObjectsForBatching.Count; i++)
            {
                GameObject combinedMeshRoot = new GameObject() { name = "CombinedMesh_" + i };
                Material[] materials = staticObjectsList[i].renderer.sharedMaterials;
                List<Material> uniqueMaterialsList = new List<Material>();        

                for (int j = 0; j < materials.Length; j++)
                {
                    if (!uniqueMaterialsList.Contains(materials[j])) // If Material doesn't exists in the list then add it.
                    {
                        uniqueMaterialsList.Add(materials[j]);
                    }
                }

                CombineMeshes(combinedMeshRoot, _singleMaterialObjectsForBatching[i], uniqueMaterialsList, i);
            }    
        }

        private static LODGroup GetLODGroup(Transform targetObject)
        {
            LODGroup outLODGroup = null;
            var currentParent = targetObject.parent;

            List<Transform> parents = new List<Transform>()
            {
                currentParent
            };            

            while (currentParent != null) 
            {
                currentParent = currentParent.parent;

                if (currentParent != null)
                {
                    parents.Add(currentParent);
                }
            }
            
            for (int i = 0; i < parents.Count;i++) 
            { 
                var lodGroup = parents[i].GetComponent<LODGroup>();
                
                if (lodGroup != null) 
                {
                    outLODGroup = lodGroup;
                    break;
                }
            }

            return outLODGroup;
        }

        private static void CreateCombineList(MagicLightmapSwitcher swithcerInstance, List<MagicLightmapSwitcher.AffectedObject> staticObjectsList)
        {
            List<MagicLightmapSwitcher.AffectedObject> multimaterialObjects = new List<MagicLightmapSwitcher.AffectedObject>();
            List<MagicLightmapSwitcher.AffectedObject> objectsWithLOD = new List<MagicLightmapSwitcher.AffectedObject>();
            List<MagicLightmapSwitcher.AffectedObject> objectsWithoutLOD = new List<MagicLightmapSwitcher.AffectedObject>();

            _sortedByDistanceObjects.Clear();
            _multiMaterialObjectsForBatching.Clear();
            _singleMaterialObjectsForBatching.Clear();

            _sortedByDistanceObjects.Add(new List<MagicLightmapSwitcher.AffectedObject>());

            _multiMaterialObjectsForBatching.Add(new List<MagicLightmapSwitcher.AffectedObject>());
            _singleMaterialObjectsForBatching.Add(new List<MagicLightmapSwitcher.AffectedObject>());
            _singleMaterialObjectsForBatching[0].Add(staticObjectsList[0]);

            List<List<LOD[]>> combinedLODs = new List<List<LOD[]>>();

            //for (int i = 0; i < staticObjectsList.Count; i++)
            //{
            //    if (GetLODGroup(staticObjectsList[i].meshRenderer.transform) != null)
            //    {
            //        objectsWithLOD.Add(staticObjectsList[i]);
            //    }
            //    else
            //    {
            //        objectsWithoutLOD.Add(staticObjectsList[i]);
            //    }
            //}

            //for (int i = 0;i < objectsWithLOD.Count; i++)
            //{
            //    if (objectsWithLOD[i].mlsObject.excludeFromCombining)
            //    {
            //        continue;
            //    }

            //    if (objectsWithLOD[i].meshRenderer.sharedMaterials.Length > 1)
            //    {
            //        continue;
            //    }

            //    var lodGroup = GetLODGroup(objectsWithLOD[i].meshRenderer.transform);
            //    var renderersInGroup = lodGroup.GetLODs();
            //    var combinedLOD = new LOD[renderersInGroup.Length];

            //    for (int j = 0; j < renderersInGroup.Length; j++)
            //    {
            //        for (int k = 0; k < renderersInGroup[j].renderers.Length; k++)
            //        {
            //            if (renderersInGroup[j].renderers[k].sharedMaterial.name.StartsWith(
            //                objectsWithLOD[i].meshRenderer.sharedMaterial.name))
            //            {
            //                combinedLOD[j].renderers
            //            }
            //        }                    
            //    }

            //    combinedLODs.Add(renderersInGroup[j]);
            //}            

            //GameObject combinedMeshRoot = new GameObject() { name = "CombinedMesh_" + i };
            //var lod = combinedMeshRoot.AddComponent<LODGroup>();

            //lod.SetLODs();

            var addedToCombine = 0;

            for (int i = 1; i < staticObjectsList.Count; i++)
            {
                if (staticObjectsList[i].mlsObject.excludeFromCombining)
                {
                    multimaterialObjects.Add(staticObjectsList[i]);
                    continue;
                }

                if (staticObjectsList[i].renderer.sharedMaterials.Length > 1)
                {
                    continue;
                }

                addedToCombine = 0;

                for (int j = 0; j < _singleMaterialObjectsForBatching.Count; j++)
                {
                    for (int k = 0; k < _singleMaterialObjectsForBatching[j].Count; k++)
                    {
                        if (_singleMaterialObjectsForBatching[j][k].renderer.sharedMaterial.name.StartsWith(
                            staticObjectsList[i].renderer.sharedMaterial.name) &&
                            _singleMaterialObjectsForBatching[j][k].renderer.lightmapIndex ==
                            staticObjectsList[i].renderer.lightmapIndex)
                        {
                            if (_singleMaterialObjectsForBatching[j][k].renderer.bounds.size.Equals(
                                staticObjectsList[i].renderer.bounds.size))
                            {
                                var currentSize = 0.0f;
                                var prevSize = 0.0f;

                                for (int s = 0; s < swithcerInstance.sizeGroups.Count; s++)
                                {
                                    currentSize = swithcerInstance.sizeGroups[s];

                                    if (s == 0)
                                    {
                                        prevSize = 0.0f;
                                    }
                                    else
                                    {
                                        prevSize = swithcerInstance.sizeGroups[s - 1];
                                    }

                                    if (Vector3.Magnitude(staticObjectsList[i].renderer.bounds.size) < currentSize &&
                                        Vector3.Magnitude(staticObjectsList[i].renderer.bounds.size) > prevSize)
                                    {
                                        if (Vector3.Distance(
                                            _singleMaterialObjectsForBatching[j][k].renderer.transform.position,
                                            staticObjectsList[i].renderer.transform.position) <= swithcerInstance.distanceGroups[s])
                                        {
                                            var lodGroup_1 = GetLODGroup(staticObjectsList[i].renderer.transform);
                                            var lodGroup_2 = GetLODGroup(_singleMaterialObjectsForBatching[j][k].renderer.transform);

                                            if (lodGroup_1 != null && lodGroup_2 != null)
                                            {
                                                var test1 = lodGroup_1.GetLODs();
                                                var test2 = lodGroup_2.GetLODs();                                                

                                                _singleMaterialObjectsForBatching[j].Add(staticObjectsList[i]);

                                                addedToCombine++;
                                                break;
                                            }
                                            else
                                            {
                                                _singleMaterialObjectsForBatching[j].Add(staticObjectsList[i]);

                                                addedToCombine++;
                                                break;
                                            }
                                        }
                                    }
                                }

                                if (addedToCombine > 0)
                                {
                                    break;
                                }
                            }                         
                        }
                        else
                        {                            
                            break;
                        }
                    }

                    if (addedToCombine > 0)
                    {
                        break;
                    }
                }

                if (addedToCombine == 0)
                {   
                    _singleMaterialObjectsForBatching.Add(new List<MagicLightmapSwitcher.AffectedObject>());
                    _singleMaterialObjectsForBatching.Last().Add(staticObjectsList[i]);
                }
            }

            //for (int i = 0; i < _singleMaterialObjectsForBatching.Count; i++)
            //{
            //    for (int j = 0; j < _singleMaterialObjectsForBatching[i].Count; j++)
            //    {
            //        if (_singleMaterialObjectsForBatching[i][j].meshRenderer.bounds.size.Equals(
            //            _singleMaterialObjectsForBatching[i][j + 1].meshRenderer.bounds.size))
            //        {
            //            var currentSize = 0.0f;
            //            var prevSize = 0.0f;

            //            for (int s = 0; s < swithcerInstance.sizeGroups.Count; s++)
            //            {
            //                currentSize = swithcerInstance.sizeGroups[s];

            //                if (s == 0)
            //                {
            //                    prevSize = 0.0f;
            //                }
            //                else
            //                {
            //                    prevSize = swithcerInstance.sizeGroups[s - 1];
            //                }

            //                if (Vector3.Magnitude(staticObjectsList[i].meshRenderer.bounds.size) < currentSize &&
            //                    Vector3.Magnitude(staticObjectsList[i].meshRenderer.bounds.size) > prevSize)
            //                {
            //                    if (Vector3.Distance(
            //                        _singleMaterialObjectsForBatching[j][k].meshRenderer.transform.position,
            //                        staticObjectsList[i].meshRenderer.transform.position) <= currentSize * 3)
            //                    {

            //                    }
            //                }
            //            }

            //            if (addedToCombine > 0)
            //            {
            //                break;
            //            }
            //        }
            //    }
            //}
        }

        private static void CombineMeshes(GameObject parentObject, List<MagicLightmapSwitcher.AffectedObject> objectsGroupForBatching, List<Material> materialsList, int groupIndex)
        {
            parentObject.transform.rotation = Quaternion.identity;
            parentObject.transform.position = Vector3.zero;
            parentObject.transform.localScale = Vector3.one;

            Combine(parentObject, objectsGroupForBatching, materialsList, groupIndex);
        }

        private static void Combine(GameObject parentObject, List<MagicLightmapSwitcher.AffectedObject> objectsGroupForBatching, List<Material> materialsList, int groupIndex)
        {            
            List<CombineInstance> finalMeshCombineInstancesList = new List<CombineInstance>();

            CombineInstance[] combineInstance = new CombineInstance[objectsGroupForBatching.Count];

            for (int i = 0; i < objectsGroupForBatching.Count; i++)
            { 
                combineInstance[i].subMeshIndex = 0;
                combineInstance[i].mesh = objectsGroupForBatching[i].renderer.transform.GetComponent<MeshFilter>().sharedMesh;
                combineInstance[i].transform = objectsGroupForBatching[i].renderer.transform.localToWorldMatrix;

                //finalMeshCombineInstancesList.Add(combineInstance);
            }

            Mesh mesh = new Mesh();

            mesh.name = "CombinedMesh_" + groupIndex;
            mesh.CombineMeshes(combineInstance, true, true, false);

            Debug.Log(mesh.name + " Sub meshes count - " + mesh.subMeshCount);

            MeshFilter myMeshFilter = parentObject.AddComponent<MeshFilter>();
            MeshRenderer myMeshRenderer = parentObject.AddComponent<MeshRenderer>();

            myMeshFilter.sharedMesh = mesh;
            myMeshRenderer.sharedMaterial = objectsGroupForBatching[0].renderer.sharedMaterial;

            //            for (int i = 0; i < uniqueMaterialsList.Count; i++) // Create each Mesh (submesh) from Meshes with the same Material.
            //            {
            //                List<CombineInstance> submeshCombineInstancesList = new List<CombineInstance>();

            //                for (int j = 0; j < meshFilters.Length - 1; j++) // Get only childeren Meshes (skip our Mesh).
            //                {
            //                    if (meshRenderers[j + 1] != null)
            //                    {
            //                        Material[] submeshMaterials = meshRenderers[j+1].sharedMaterials; // Get all Materials from child Mesh.

            //                        for (int k = 0; k < submeshMaterials.Length; k++)
            //                        {
            //                            // If Materials are equal, combine Mesh from this child:
            //                            if (uniqueMaterialsList[i] == submeshMaterials[k])
            //                            {
            //                                CombineInstance combineInstance = new CombineInstance();
            //                                combineInstance.subMeshIndex = k; // Mesh may consist of smaller parts - submeshes.
            //                                                                  // Every part have different index. If there are 3 submeshes
            //                                                                  // in Mesh then MeshRender needs 3 Materials to render them.
            //                                combineInstance.mesh = meshFilters[j + 1].sharedMesh;
            //                                combineInstance.transform = meshFilters[j + 1].transform.localToWorldMatrix;
            //                                submeshCombineInstancesList.Add(combineInstance);
            //                                verticesLength += combineInstance.mesh.vertices.Length;
            //                            }
            //                        }
            //                    }
            //                }

            //                // Create new Mesh (submesh) from Meshes with the same Material:
            //                Mesh submesh = new Mesh();

            //#if UNITY_2017_3_OR_NEWER
            //                if (verticesLength > Mesh16BitBufferVertexLimit)
            //                {
            //                    submesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // Only works on Unity 2017.3 or higher.
            //                }

            //                submesh.CombineMeshes(submeshCombineInstancesList.ToArray(), true);
            //#else
            //			// Below Unity 2017.3 if vertices count is above the limit then an error appears in the console when we use the below method.
            //			// Anyway we don't stop the algorithm here beacuse we want to count the entire number of vertices in the children meshes:
            //			if(verticesLength <= Mesh16BitBufferVertexLimit)
            //			{
            //				submesh.CombineMeshes(submeshCombineInstancesList.ToArray(), true);
            //			}
            //#endif

            //                CombineInstance finalCombineInstance = new CombineInstance();
            //                finalCombineInstance.subMeshIndex = 0;
            //                finalCombineInstance.mesh = submesh;
            //                finalCombineInstance.transform = Matrix4x4.identity;
            //                finalMeshCombineInstancesList.Add(finalCombineInstance);
            //            }
            //            #endregion Combine submeshes (children Meshes) with the same Material.

            //            #region Set Materials array & combine submeshes into one multimaterial Mesh:
            //            meshRenderers[0].sharedMaterials = uniqueMaterialsList.ToArray();

            //            Mesh combinedMesh = new Mesh();
            //            combinedMesh.name = name;

            //#if UNITY_2017_3_OR_NEWER
            //            if (verticesLength > Mesh16BitBufferVertexLimit)
            //            {
            //                combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // Only works on Unity 2017.3 or higher.
            //            }

            //            combinedMesh.CombineMeshes(finalMeshCombineInstancesList.ToArray(), false);
            //            GenerateUV(combinedMesh);
            //            meshFilters[0].sharedMesh = combinedMesh;
            //            DeactivateCombinedGameObjects(meshFilters);

            //            if (showCreatedMeshInfo)
            //            {
            //                if (verticesLength <= Mesh16BitBufferVertexLimit)
            //                {
            //                    Debug.Log("<color=#00cc00><b>Mesh \"" + name + "\" was created from " + (meshFilters.Length - 1) + " children meshes and has "
            //                        + finalMeshCombineInstancesList.Count + " submeshes, and " + verticesLength + " vertices.</b></color>");
            //                }
            //                else
            //                {
            //                    Debug.Log("<color=#ff3300><b>Mesh \"" + name + "\" was created from " + (meshFilters.Length - 1) + " children meshes and has "
            //                        + finalMeshCombineInstancesList.Count + " submeshes, and " + verticesLength
            //                        + " vertices. Some old devices, like Android with Mali-400 GPU, do not support over 65535 vertices.</b></color>");
            //                }
            //            }		
        }

        private static void DeactivateCombinedGameObjects(MeshFilter[] meshFilters)
        {
            for (int i = 0; i < meshFilters.Length - 1; i++) // Skip first MeshFilter belongs to this GameObject in this loop.
            {
                if (!destroyCombinedChildren)
                {
                    if (deactivateCombinedChildren)
                    {
                        meshFilters[i + 1].gameObject.SetActive(false);
                    }
                    if (deactivateCombinedChildrenMeshRenderers)
                    {
                        MeshRenderer meshRenderer = meshFilters[i+1].gameObject.GetComponent<MeshRenderer>();
                        if (meshRenderer != null)
                        {
                            meshRenderer.enabled = false;
                        }
                    }
                }
                else
                {
                    //DestroyImmediate(meshFilters[i + 1].gameObject);
                }
            }
        }

        private static void GenerateUV(Mesh combinedMesh)
        {
#if UNITY_EDITOR
            if (generateUVMap)
            {
                UnityEditor.UnwrapParam unwrapParam = new UnityEditor.UnwrapParam();
                UnityEditor.UnwrapParam.SetDefaults(out unwrapParam);
                UnityEditor.Unwrapping.GenerateSecondaryUVSet(combinedMesh, unwrapParam);
            }
#endif
        }

        public static void GenerateCombinerSettings(MagicLightmapSwitcher activeInstance)
        {
            Renderer[] renderers = Object.FindObjectsOfType<Renderer>();
            
            List<List<Renderer>> groupedBySize = new List<List<Renderer>>()
            {
                new List<Renderer>()
            };            

            groupedBySize.Last().Add(renderers[0]);

            List<float> sizesList = new List<float>()
            {
                Vector3.Magnitude(renderers[0].bounds.size)
            };

            List<GameObject> appropriateObjects = new List<GameObject>();

            var addedToSizeGroup = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                addedToSizeGroup = 0;

                for (int j = 0; j < groupedBySize.Count; j++)
                {
                    for (int k = 0; k < groupedBySize[j].Count; k++)
                    {
                        if (groupedBySize[j][k].bounds.size.Equals(
                            renderers[i].bounds.size))
                        {
                            groupedBySize[j].Add(renderers[i]);

                            addedToSizeGroup++;
                            break;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (addedToSizeGroup > 0)
                    {
                        break;
                    }
                }

                if (addedToSizeGroup == 0)
                {
                    appropriateObjects.Add(renderers[i].gameObject);
                    groupedBySize.Add(new List<Renderer>());
                    groupedBySize.Last().Add(renderers[i]);

                    sizesList.Add(Vector3.Magnitude(renderers[i].bounds.size));
                }
            }

            sizesList.Sort();
            activeInstance.sizeGroups.Clear();
            activeInstance.appropriateObjects.Clear();
            var size = 1;

            for (int i = 0; i < sizesList.Count; i++)
            {
                if (sizesList[i] > size) 
                {
                    activeInstance.sizeGroups.Add(Mathf.Round(sizesList[i]));
                    activeInstance.distanceGroups.Add(sizesList[i] * 3);
                    activeInstance.appropriateObjects.Add(appropriateObjects[i]);
                    size *= 3;                    
                }
            }
        }
    }
}
#endif