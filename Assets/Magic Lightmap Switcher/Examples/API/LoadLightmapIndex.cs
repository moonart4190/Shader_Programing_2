using System.Collections;
using UnityEngine;
using MagicLightmapSwitcher;

public class LoadLightmapIndex : MonoBehaviour
{
    public StoredLightingScenario lightingScenario;
    public int lightmapIndex;

    private RuntimeAPI runtimeAPI;

    void Start()
    {
        StartCoroutine(LoadLightmapAtIndex());
    }

    private void Update()
    {
        
    }

    private IEnumerator LoadLightmapAtIndex()
    {
        while(lightingScenario == null) 
        {
            yield return null;
        }

        runtimeAPI = new RuntimeAPI(this, lightingScenario, lightmapIndex);
    }
}
