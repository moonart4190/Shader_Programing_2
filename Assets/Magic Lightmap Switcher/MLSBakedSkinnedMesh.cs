using UnityEngine;

namespace MagicLightmapSwitcher
{
    public class MLSBakedSkinnedMesh : MonoBehaviour
    {
        public MeshRenderer copyLightmapDataFrom;

        void Awake()
        {
            if (copyLightmapDataFrom != null)
            {
                GetComponent<Renderer>().lightmapIndex = copyLightmapDataFrom.lightmapIndex;
                GetComponent<Renderer>().lightmapScaleOffset = copyLightmapDataFrom.lightmapScaleOffset;
            }
        }
    }
}
