using UnityEngine;

namespace MagicLightmapSwitcher
{
    [ExecuteInEditMode]
    public class MLSDynamicRenderer : MLSObject
    {
        public bool added;
        
        private new void OnEnable()
        {
            base.OnEnable();

            if (parentScene != gameObject.scene.name)
            {
                parentScene = gameObject.scene.name;
                UpdateGUID();
            }
        }

        private new void Update()
        {
            if (!added)
            {
                if (MagicLightmapSwitcher.OnDynamicRendererAdded != null)
                {
                    added = true;
                    MagicLightmapSwitcher.OnDynamicRendererAdded.Invoke(gameObject, this);
                }
            }
            
            base.Update();
        }

        private void OnDestroy()
        {
            if (affectableObject != null)
            {
                MagicLightmapSwitcher.OnDynamicRendererRemoved.Invoke(gameObject, affectableObject);
            }
        }
    }
}