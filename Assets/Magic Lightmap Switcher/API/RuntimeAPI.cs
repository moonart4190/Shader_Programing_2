using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MagicLightmapSwitcher
{
    public class RuntimeAPI
    {
        public RuntimeAPI(MonoBehaviour caller = null, StoredLightingScenario scenario = null, int loadLightmapIndex = -1)
        {
            if (caller != null)
            {
                caller.StartCoroutine(GetSwitcherInstance(scenario, loadLightmapIndex));
            }
        }

        private IEnumerator GetSwitcherInstance(StoredLightingScenario scenario, int lightmapIndex)
        {
            while (currentSwitcherSource == null)
            {
                currentSwitcherSource = GetSwitcherInstanceStatic("", true);
                yield return null;
            }            

            while (Time.frameCount < 3)
            {
                yield return null;
            }

            currentSwitcherSource.runtimeAPI = this;

            if (scenario != null)
            {
                SwitchLightmap(lightmapIndex, scenario);
            }
        }
        
        #region Blending
        public float currentBlendingTime;
        private StoredLightingScenario currentScenario;
        private MagicLightmapSwitcher currentSwitcherSource;
        
        public enum BlendingDirection
        {
            FirstToLast,
            LastToFirst
        }

        private bool CheckScenario(StoredLightingScenario scenario)
        {
            if (scenario == null)
            {
                Debug.LogFormat("<color=cyan>MLS:</color> Wrong lighting scenario asset.");
                return false;
            }
            else if (scenario.blendableLightmaps.Count < 2)
            {
                Debug.LogFormat("<color=cyan>MLS:</color> Insufficient data to blending. You must add at least two sets of lighting data to the scenario.");
                return false;
            }

            return true;
        }

        public MagicLightmapSwitcher GetSwitcherSource(string targetScene)
        {
            if (currentSwitcherSource == null)
            {
                currentSwitcherSource = GetSwitcherInstanceStatic(targetScene);
            }

            return currentSwitcherSource;
        }

        public static MagicLightmapSwitcher GetSwitcherInstanceStatic(string targetScene = null, bool ignoreWarning = false)
        {
            MagicLightmapSwitcher resultSource = null;
            bool sceneError = false;
            
            if (string.IsNullOrEmpty(targetScene))
            {
                targetScene = SceneManager.GetActiveScene().name;
            }
            
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene currentTestingScene = SceneManager.GetSceneAt(i);

                if (currentTestingScene.name == targetScene)
                {
                    if (!currentTestingScene.isLoaded)
                    {
                        sceneError = true;
                    }
                    else
                    {
                        GameObject[] gameObjects = currentTestingScene.GetRootGameObjects();

                        for (int j = 0; j < gameObjects.Length; j++)
                        {
                            if (gameObjects[j].name == "Magic Tools")
                            {
                                for (int o = 0; o < gameObjects[j].transform.childCount; o++)
                                {
                                    MagicLightmapSwitcher switcherObject = gameObjects[j].transform.GetChild(o)
                                        .GetComponent<MagicLightmapSwitcher>();

                                    if (switcherObject != null)
                                    {
                                        if (switcherObject.gameObject.scene.name == currentTestingScene.name)
                                        {
                                            resultSource = switcherObject;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (!ignoreWarning)
            {
                if (resultSource == null && !sceneError)
                {
                    return null;
                    // Debug.LogFormat(
                    //     "<color=cyan>MLS:</color>The system was unable to determine the instance of the core component. " +
                    //     "Make sure you provide the correct Lightmap Scenario.");
                }
            }

            return resultSource;
        }

        /// <summary>
        /// Runs a blending cycle for the selected lighting scenario.
        /// </summary>
        /// <param name="cycleLength">Blend cycle time.</param>
        /// <param name="scenario">Lighting Scenario Asset.</param>
        /// <param name="blendingDirection">Direction of blending.</param>
        public void BlendLightmapsCyclic(float cycleLength, StoredLightingScenario scenario, BlendingDirection blendingDirection = BlendingDirection.FirstToLast)
        {
            if (!CheckScenario(scenario))
            {
                return;
            }

            GetSwitcherSource(scenario.targetScene);

            if (currentSwitcherSource == null || !currentSwitcherSource.storedDataUpdated)
            {
                return;
            }

            scenario.SetActive(currentSwitcherSource);

            currentBlendingTime += Time.deltaTime;

            if (currentBlendingTime > cycleLength)
            {
                currentBlendingTime = 0;
            }

            float blendingPercent = currentBlendingTime / cycleLength;

            switch (blendingDirection)
            {
                case BlendingDirection.FirstToLast:
                    scenario.globalBlendFactor = Mathf.Lerp(0, 1, blendingPercent); 
                    break;
                case BlendingDirection.LastToFirst: 
                    scenario.globalBlendFactor = Mathf.Lerp(1, 0, blendingPercent);
                    break;
            }
            
            Blending.Blend(currentSwitcherSource, scenario.globalBlendFactor, scenario);
        }

        /// <summary>
        /// Runs a blending cycle for the selected lighting scenario.
        /// </summary>
        /// <param name="length">Blend cycle time.</param>
        /// <param name="scenario">Lighting Scenario Asset.</param>
        /// <param name="blendingDirection">Direction of blending.</param>
        
        public void BlendLightmaps(float length, StoredLightingScenario scenario, BlendingDirection blendingDirection = BlendingDirection.FirstToLast)
        {
            if (!CheckScenario(scenario))
            {
                return;
            }

            GetSwitcherSource(scenario.targetScene);

            if (currentSwitcherSource == null || !currentSwitcherSource.storedDataUpdated)
            {
                return;
            }

            scenario.SetActive(currentSwitcherSource);

            currentBlendingTime += Time.deltaTime;

            if (currentBlendingTime < length)
            {
                float blendingPercent = currentBlendingTime / length;

                switch (blendingDirection)
                {
                    case BlendingDirection.FirstToLast:
                        scenario.globalBlendFactor = Mathf.Lerp(0, 1, blendingPercent); 
                    break;
                    case BlendingDirection.LastToFirst: 
                        scenario.globalBlendFactor = Mathf.Lerp(1, 0, blendingPercent);
                    break;
                }
                
                Blending.Blend(currentSwitcherSource, scenario.globalBlendFactor, scenario);
            }
        }
        
        /// <summary>
        /// Sets the blending factor to the specified value in the range [0-1].
        /// </summary>
        /// <param name="blendValue">Value in range [0-1].</param>
        /// <param name="scenario">The lighting scenario for which blending will be applied.</param>
        public void BlendLightmapsCustom(float blendValue, StoredLightingScenario scenario)
        {
            if (!CheckScenario(scenario))
            {
                return;
            }

            GetSwitcherSource(scenario.targetScene);

            if (currentSwitcherSource == null || !currentSwitcherSource.storedDataUpdated)
            {
                return;
            }

            scenario.SetActive(currentSwitcherSource);

            scenario.globalBlendFactor = blendValue;

            Blending.Blend(currentSwitcherSource, scenario.globalBlendFactor, scenario);
        }

        public void ResetBlendingTime(float length)
        {
            if (currentBlendingTime >= length)
            {
                currentBlendingTime = 0;
            }
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="speed">Blending length.</param>
        /// <param name="scenario">Lighting Scenario Asset.</param>
        public void BlendLightmapsPingPong(float speed, StoredLightingScenario scenario)
        {
            if (scenario == null)
            {
                Debug.LogFormat("<color=cyan>MLS:</color> Wrong lighting scenario asset.");
                return;
            }
            else if (scenario.blendableLightmaps.Count < 2)
            {
                Debug.LogFormat("<color=cyan>MLS:</color> Insufficient data to blending. You must add at least two sets of lighting data to the scenario.");
                return;
            }

            GetSwitcherSource(scenario.targetScene);

            if (currentSwitcherSource == null || !currentSwitcherSource.storedDataUpdated)
            {
                return;
            }

            scenario.SetActive(currentSwitcherSource);

            scenario.globalBlendFactor = Mathf.PingPong(Time.time * speed, 1);
            Blending.Blend(currentSwitcherSource, scenario.globalBlendFactor, scenario);
        }

#if ENVIRO_LW || ENVIRO_HD
        public float Remap(float value, float from1, float to1, float from2, float to2)
        {
            return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
        }

        public void EnviroControlled(StoredLightingScenario scenario)
        {
            if (scenario == null)
            {
                Debug.LogFormat("<color=cyan>MLS:</color> Wrong lighting scenario asset.");
                return;
            }
            else if (scenario.blendableLightmaps.Count < 2)
            {
                Debug.LogFormat("<color=cyan>MLS:</color> Insufficient data to blending. You must add at least two sets of lighting data to the scenario.");
                return;
            }

            GetSwitcherSource(scenario.targetScene);

            if (currentSwitcherSource == null || !currentSwitcherSource.storedDataUpdated)
            {
                return;
            }

            scenario.SetActive(currentSwitcherSource);

            scenario.globalBlendFactor = Remap(Enviro.EnviroManager.instance.Time.GetTimeOfDay(), 0.0f, 24.0f, 0.0f, 1.0f);
            Blending.Blend(currentSwitcherSource, scenario.globalBlendFactor, scenario);
        }
#endif
        #endregion

        #region Switching
        public void SwitchLightmap(int lightmapIndex, StoredLightingScenario scenario, bool loadAtIndex = true)
        {
            if (scenario == null)
            {
                Debug.LogFormat("<color=cyan>MLS:</color> Wrong lighting scenario asset.");
                return;
            }
            else if (lightmapIndex > scenario.blendableLightmaps.Count)
            {
                Debug.LogFormat("<color=cyan>MLS:</color> The lightmap index you specified is greater than the number of lightmaps in the scenario.");
                return;
            }

            GetSwitcherSource(scenario.targetScene);

            if (currentSwitcherSource == null || !currentSwitcherSource.storedDataUpdated)
            {
                return;
            }

            currentSwitcherSource.lightingDataSwitching = true;
            scenario.SetActive(currentSwitcherSource);

            if (currentSwitcherSource.systemProperties.useSwitchingOnly)
            {
                if ((currentSwitcherSource.systemProperties.highDefinitionRPActive && !currentSwitcherSource.systemProperties.highDefinitionRPPatched) ||
                    (currentSwitcherSource.systemProperties.universalRPActive && !currentSwitcherSource.systemProperties.universalRPPatched))
                {
                    currentSwitcherSource.currentLightmapScenario.globalBlendFactor = scenario.blendableLightmaps[lightmapIndex].startValue;
                    Switching.LoadLightingData(currentSwitcherSource, lightmapIndex, Switching.LoadMode.Asynchronously);
                }
                else
                {
                    Blending.Blend(currentSwitcherSource, scenario.blendableLightmaps[lightmapIndex].startValue, scenario);
                }
            }
            else
            {
                Blending.Blend(currentSwitcherSource, scenario.blendableLightmaps[lightmapIndex].startValue, scenario);
            }

            currentSwitcherSource.OnLoadedLightmapChanged[scenario.eventsListId].Invoke(scenario, lightmapIndex);
        }        
        #endregion
    }
}
