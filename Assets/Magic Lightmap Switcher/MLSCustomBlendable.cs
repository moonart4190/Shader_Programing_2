using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MagicLightmapSwitcher
{
    [ExecuteInEditMode]
    [System.Serializable]
    public class MLSCustomBlendable : MonoBehaviour
    {
        public enum BindOptions
        {
            Fixed,
            Blending
        }

        public enum BlendingSynchMode
        {
            WithGlobalBlend,
            WithReflections,
            WithLightmaps
        }

        [SerializeField, HideInInspector]
        public string sourceScriptId;
        [HideInInspector]
        public List<FieldInfo> blendableFloatFields = new List<FieldInfo>();
        [HideInInspector]
        public List<FieldInfo> blendableCubemapFields = new List<FieldInfo>();
        [HideInInspector]
        public List<FieldInfo> blendableColorFields = new List<FieldInfo>();
        [HideInInspector]
        public List<FieldInfo> blendableBoolFields = new List<FieldInfo>();
        [HideInInspector]
        public List<BindOptions> bindOptions = new List<BindOptions>();
        [HideInInspector]
        public List<BlendingSynchMode> blendingMode = new List<BlendingSynchMode>();
        [HideInInspector]
        public List<List<float>> blendingRanges = new List<List<float>>();

#if UNITY_EDITOR
        [SerializeField]
        int instanceID = 0;

        void Awake()
        {
            if (Application.isPlaying)
            {
                return;
            }

            if (instanceID == 0)
            {
                instanceID = GetInstanceID();
                return;
            }

            if (instanceID != GetInstanceID() && GetInstanceID() < 0)
            {
                sourceScriptId = null;
                instanceID = GetInstanceID();
                SetGUID();
            }
        }
#endif

        public void OnEnable()
        {
            SetGUID();
        }

        public void SetGUID()
        {
            if (string.IsNullOrEmpty(sourceScriptId))
            {
                sourceScriptId = Guid.NewGuid().ToString();
            }
        }

        public MLSCustomBlendable UpdateScriptLink()
        {
            return this;
        }

        public void GetSharedParameters()
        {
            UpdateScriptLink();

            bindOptions.Clear();
            blendingMode.Clear();
            blendableFloatFields.Clear();
            blendableCubemapFields.Clear();
            blendableColorFields.Clear();
            blendableBoolFields.Clear();

            FieldInfo[] sorceScriptFields = GetType().GetFields();

            foreach (FieldInfo parameter in sorceScriptFields)
            {
                if (parameter.Name.Contains("mls_"))
                {
                    if (parameter.Name.Contains("mls_b_"))
                    {
                        bindOptions.Add(BindOptions.Blending);
                        blendingMode.Add(new BlendingSynchMode());

                        if (parameter.FieldType.Name == "Cubemap")
                        {
                            if (!blendableCubemapFields.Contains(parameter))
                            {
                                blendableCubemapFields.Add(parameter);
                            }
                        }
                        else if (parameter.FieldType.Name == "Single")
                        {
                            if (!blendableFloatFields.Contains(parameter))
                            {
                                blendableFloatFields.Add(parameter);
                            }
                        }
                        else if (parameter.FieldType.Name == "Color")
                        {
                            if (!blendableColorFields.Contains(parameter))
                            {
                                blendableColorFields.Add(parameter);
                            }
                        }
                        else if (parameter.FieldType.Name == "Boolean")
                        {
                            if (!blendableBoolFields.Contains(parameter))
                            {
                                blendableBoolFields.Add(parameter);
                            }
                        }
                    }
                }
            }
        }
    }
}
