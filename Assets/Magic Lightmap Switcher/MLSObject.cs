using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MagicLightmapSwitcher
{
    public class MLSObject : MonoBehaviour
    {
        #if BAKERY_INCLUDED
        public enum BakeryVolumesBlendingSynch
        {
            Reflections,
            Lightmaps
        }

        public BakeryVolumesBlendingSynch bakeryVolumesBlendingSynch;
        #endif
        
        [SerializeField] 
        public string scriptId;
        [SerializeField] 
        public string parentScene;
        [SerializeField] 
        public Mesh defaultMesh;
        [SerializeField] 
        public Transform defaultTransform;
        [SerializeField]
        public bool excludeFromCombining;

        public MagicLightmapSwitcher.AffectedObject affectableObject;
        public MaterialPropertyBlock propertyBlock;
        public MagicLightmapSwitcher switcherInstance;
        public MagicLightmapSwitcher.AffectedObject objectData;
        public Renderer objectRenderer;
        public MaterialPropertyBlock materialPropertyBlock;
        public Terrain terrain;
        public List<ReflectionProbeBlendInfo> closestReflectionProbes = new List<ReflectionProbeBlendInfo>();
        public string[] probeNames = new string[2];
        public int[] probeIndexes = new int[2];
        public bool isStatic;
        public bool isSkipped = false;
        public bool haveTrackableShaderProps = false;
        private Vector3 _lastPosition;
        private bool updated; 

        public void OnEnable()
        {
#if UNITY_EDITOR
            var objectsOnScene = FindObjectsOfType<MLSObject>();

            for (int i = 0; i < objectsOnScene.Length; i++)
            {
                if (objectsOnScene[i] != this && 
                    objectsOnScene[i].scriptId == scriptId)
                {
                    UpdateGUID();
                }
            }
#endif

            switcherInstance = RuntimeAPI.GetSwitcherInstanceStatic(gameObject.scene.name);
            objectRenderer = gameObject.GetComponent<Renderer>();
            terrain = gameObject.GetComponent<Terrain>();
            propertyBlock = new MaterialPropertyBlock();
            updated = false;
        }

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        public void Update()
        {
            if (switcherInstance == null)
            {
                switcherInstance = RuntimeAPI.GetSwitcherInstanceStatic(gameObject.scene.name);
            }
            
            if (!switcherInstance.storedDataUpdated)
            {
                return;
            }
        }
        
        public void UpdateGUID()
        {
            scriptId = Guid.NewGuid().ToString();
        }

        private void OnBecameVisible()
        {
            if (switcherInstance != null)
            {
                switcherInstance.becameVisibleObjects++;
            }

            isSkipped = false;
        }

        private void OnBecameInvisible()
        {
            isSkipped = true;
        }
    }
}
