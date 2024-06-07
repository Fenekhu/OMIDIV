using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FFmpegRenderFeature : ScriptableRendererFeature {

    public static void SendTextureToFFmpeg(Texture tex0, uint frameNum) {
        // resize and convert to srgb
        (int vw, int vh) = FFmpegWrapper2.VideoSize;
        RenderTexture tex = RenderTexture.GetTemporary(vw, vh, 0, RenderTextureFormat.ARGB32);
        bool srgbreset = GL.sRGBWrite;
        GL.sRGBWrite = true;
        Graphics.Blit(tex0, tex);
        GL.sRGBWrite = srgbreset;

        // RequestIntoNativeArray doesn't finish in order, causing ReceiveFrame to be called with out of order.
        // The frame number will be used by ReceiveFrame to buffer and reorder frames.
        var narray = new NativeArray<byte>(tex.width * tex.height * 4, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        var request = AsyncGPUReadback.RequestIntoNativeArray(ref narray, tex, 0, TextureFormat.ARGB32, (AsyncGPUReadbackRequest req) => {
            if (!req.hasError) {
                FFmpegWrapper2.ReceiveFrame(ref narray, frameNum);
            }
            narray.Dispose();
        });
        RenderTexture.ReleaseTemporary(tex);
    }

    public class SendToFFmpegPass : ScriptableRenderPass {

        private static uint frameNum = 0;
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {
            if (!FFmpegWrapper2.IsRecording) { frameNum = 0; return; }

            RTHandle ct = renderingData.cameraData.renderer.cameraColorTargetHandle;
            RTHandle dt = renderingData.cameraData.renderer.cameraDepthTargetHandle;
            SendTextureToFFmpeg(ct, frameNum++);
        }
    }

    public RenderPassEvent rpevent = RenderPassEvent.AfterRendering;
    private SendToFFmpegPass pass;

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
        if (renderingData.cameraData.cameraType == CameraType.Game && renderingData.cameraData.camera == Camera.main) {
            pass.renderPassEvent = rpevent;
            renderer.EnqueuePass(pass);
        }
    }

    public override void Create() {
        pass = new SendToFFmpegPass();
    }
}
