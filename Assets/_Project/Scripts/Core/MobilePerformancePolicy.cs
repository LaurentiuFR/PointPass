using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PointPass.Core
{
    /// <summary>Mobile-only runtime budget; UI stays at native resolution and project assets are not edited.</summary>
    public sealed class MobilePerformancePolicy : MonoBehaviour
    {
        private RenderPipelineAsset previous;
        private UniversalRenderPipelineAsset runtime;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            if(!Application.isMobilePlatform) return;
            var root=new GameObject("MobilePerformancePolicy");DontDestroyOnLoad(root);
            root.AddComponent<MobilePerformancePolicy>().Configure();
        }
        private void Configure()
        {
            Application.targetFrameRate=60; QualitySettings.vSyncCount=0; QualitySettings.antiAliasing=2;
            Screen.autorotateToPortrait=false;Screen.autorotateToPortraitUpsideDown=false;
            Screen.autorotateToLandscapeLeft=true;Screen.autorotateToLandscapeRight=true;
            Screen.orientation=ScreenOrientation.AutoRotation;
            var source=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(source==null)return;
            previous=QualitySettings.renderPipeline;runtime=Instantiate(source);runtime.name="PointPass mobile runtime pipeline";
            runtime.msaaSampleCount=2;
            runtime.supportsHDR=false;runtime.supportsCameraDepthTexture=false;runtime.supportsCameraOpaqueTexture=false;
            runtime.renderScale=SystemInfo.graphicsMemorySize>0&&SystemInfo.graphicsMemorySize<=2048 ? .9f : 1f;
            QualitySettings.renderPipeline=runtime;
        }
        private void OnDestroy()
        {
            if(runtime==null)return;
            if(QualitySettings.renderPipeline==runtime)QualitySettings.renderPipeline=previous;
            Destroy(runtime);
        }
    }
}
