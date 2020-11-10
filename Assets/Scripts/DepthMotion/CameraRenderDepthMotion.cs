
#define USE_TEMPORARY_RT

using UnityEngine; 
using UnityEngine.Rendering; 
using UnityEngine.Assertions; 

using System.IO;

namespace DepthMotion
{
    [RequireComponent(typeof(Camera))]
    public class CameraRenderDepthMotion : MonoBehaviour
    {
        // todo: this one should be a power of two, create an attribute for it.
        public Vector2Int dim = new Vector2Int(1920, 1080);
        [RangeAttribute(2, 8)]
        public int downscalingFactor = 4;

        // Important:
        // The motion vectors need the previous frame in order to be computed.
        // As such, they should be renderer two consecutive frames..
        [RangeAttribute(1, 500)]
        public int samplingStep = 1;
        
        #region MonoBehaviour Events

        void Awake()
        {
            AssertSystem();

            // If we save each frame, there won't be an issue.
            m_shouldUseQuickFix = samplingStep > 1;
            
            PrepareDims();
            PrepareTextures();
            PrepareCameras();
            
        }

        // OnPostRender only works with the legacy render pipeline
        void OnPostRender()
        {
            RenderDepthMotionStep();
        }

        // Kind of an equivalent for SRP.
        void EndCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (cam == m_cam)
            {
                RenderDepthMotionStep();
            }
        }
        
        void OnDestroy()
        {
            FreeAll();
        }

        #endregion
        
        #region Private processing

        void RenderDepthMotionStep()
        {
            // We save at least a couple of those.
            // So that they are both computed and the second gives the actual result.
            // The first motion vector texture is discarded (if it is faulty, with the quick fix).
            if (m_currentFrameIndex % samplingStep == 0
                || (m_currentFrameIndex + 1) % samplingStep == 0)
            {
                RenderDepthMotion();
            }
            ++m_currentFrameIndex;
        }
        
        void RenderDepthMotion()
        {
            SaveFrame();
        }

        void FreeAll()
        {
#if (USE_TEMPORARY_RT)
            RenderTexture.ReleaseTemporary(m_view);
            RenderTexture.ReleaseTemporary(m_motion);
            RenderTexture.ReleaseTemporary(m_depth);
            RenderTexture.ReleaseTemporary(m_frameBuffer);
#else
            m_view.Release();
            m_motion.Release();
            m_depth.Release();
            m_frameBuffer.Release();
#endif

            RenderPipelineManager.endCameraRendering -= EndCameraRendering;
        }
        
        void PrepareDims()
        {
            m_downscaledDim = dim / downscalingFactor;
        }
        void PrepareCameras()
        {
            m_cam = GetComponent<Camera>();
            m_cam.allowDynamicResolution = false;

            // Signal that the camera should render in this Render Texture.
            m_cam.targetTexture = m_frameBuffer;
            // Signal that the camera should compute and store Depth and Motion Vectors both.
            m_cam.depthTextureMode = DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
            
            // Add callback on end of rendering of frame.
            // Used by Custom Rendering Pipelines,
            // as a stand-in for OnPostRender.
            // RenderPipelineManager.endCameraRendering += EndCameraRendering;
            
            // For now will use not Shader Replacement.
            m_blitViewCommand = new CommandBuffer   { name = "BlitCommandView" };
            m_blitDepthCommand  = new CommandBuffer { name = "BlitCommandDepth" };
            m_blitMotionCommand = new CommandBuffer { name = "BlitCommandMotionVectors" };

            
            // should set each render target as active for the corresponding buffer
            // then signal that the latter should render in its active target.
            m_blitViewCommand.SetRenderTarget(m_view);
            m_blitDepthCommand.SetRenderTarget(m_depth);
            m_blitMotionCommand.SetRenderTarget(m_motion);
            
            // they actually included the english spelling for 'grey'. wow.
            // Add a Clear action on current render target.
            m_blitViewCommand.ClearRenderTarget(true, true, Color.clear);
            m_blitDepthCommand.ClearRenderTarget(true, true, Color.clear);
            m_blitMotionCommand.ClearRenderTarget(true, true, Color.clear);

            // Add a Blit action.
            m_blitViewCommand.Blit(
                BuiltinRenderTextureType.CameraTarget, 
                m_view
                );
            m_blitDepthCommand.Blit(
                BuiltinRenderTextureType.Depth, 
                m_depth
                );
            m_blitMotionCommand.Blit(
                BuiltinRenderTextureType.MotionVectors, 
                m_motion
                );
            
            m_cam.AddCommandBuffer(
                CameraEvent.AfterEverything,
                m_blitViewCommand
                );
            m_cam.AddCommandBuffer(
                CameraEvent.AfterDepthTexture,
                m_blitDepthCommand
                );
            m_cam.AddCommandBuffer(
                CameraEvent.BeforeImageEffectsOpaque,
                m_blitMotionCommand
                );
            
            // Shader Replacement part.
            // Will need to have an additional Camera, I think??
            /*
            m_downscaledCameraObject = new GameObject();
            m_downscaledCameraObject.SetActive(false);
            m_downscaledCamera = m_downscaledCameraObject.AddComponent<Camera>();
            */
            
        }

        void PrepareTextures()
        {
#if (USE_TEMPORARY_RT)
            m_view = RenderTexture.GetTemporary(
                dim.x, 
                dim.y, 
                0,
                RenderTextureFormat.ARGB32
                );

            // Depth can be stored in a R16 format.
            // This results in a grey-scale image, as expected.
            // For now, I only use the Colour Buffer, 'cause I have trouble
            // storing it only in the Depth Buffer (which would make more sense.)
            m_depth = RenderTexture.GetTemporary(
                m_downscaledDim.x, 
                m_downscaledDim.y, 
                0,
                RenderTextureFormat.R16
            );
            
            // Motion vectors should be stored in a RGHalf format.
            // see:
            // https://docs.unity3d.com/ScriptReference/DepthTextureMode.MotionVectors.html
            m_motion = RenderTexture.GetTemporary(
                m_downscaledDim.x, 
                m_downscaledDim.y, 
                0,
                RenderTextureFormat.RGHalf
            );


            m_frameBuffer = RenderTexture.GetTemporary(
                dim.x, 
                dim.y, 
                0,
                RenderTextureFormat.ARGB32
                );
#else
            m_view = new RenderTexture(
                dim.x, 
                dim.y, 
                0,
                RenderTextureFormat.ARGB32
                );

            m_depth = new RenderTexture(
                m_downscaledDim.x, 
                m_downscaledDim.y, 
                0,
                RenderTextureFormat.R16
                );

            m_motion = new RenderTexture(
                m_downscaledDim.x, 
                m_downscaledDim.y, 
                0,
                RenderTextureFormat.RGHalf
            );
            
            m_frameBuffer = new RenderTexture(
                dim.x, 
                dim.y, 
                0,
                RenderTextureFormat.ARGB32
                );
#endif

            m_view.Create();
            m_motion.Create();
            m_depth.Create();
            m_frameBuffer.Create();
            
            m_saver = new Util.SnapshotSaver();
            m_saver.CreateFileTree();
            m_saver.CreateTextures(dim, m_downscaledDim);
        }

        void AssertSystem()
        {
            // Checks for support of dim for Render Textures
            Assert.IsTrue(dim.x < SystemInfo.maxTextureSize && dim.y < SystemInfo.maxTextureSize);
            
            // Checks for support of Motion Vectors
            Assert.IsTrue(SystemInfo.supportsMotionVectors);
            Assert.IsTrue(SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGHalf));
            Assert.IsTrue(SystemInfo.SupportsTextureFormat(TextureFormat.RGHalf));
            
            // Checks for support of Render Texture used for Depth.
            Assert.IsTrue(SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R16));
        }
        #endregion

        #region Saving routine
        
        void SaveFrame()
        {
            // Important:
            // The engine seems to not render the Motion Vectors at all
            // if we do nothing with them (that is, not save them.)
            // And we need the previous frame to be able to compute the current.
            // As such, will save a couple each time,
            int rectifiedFrameIndex = m_currentFrameIndex;
            if (m_shouldUseQuickFix && (rectifiedFrameIndex + 1) % samplingStep == 0)
            {
                // Quick fix to force sampling of previous frame
                // Then we can overwrite it on next frame.
                rectifiedFrameIndex += 1;
            }
            else if (m_currentFrameIndex == 1)
            {
                // Also, the very first frame is always bad (cause it does have a previous frame to be computed from),
                // so we always use the fix on it.
                // It will be overwritten by the next frame to be sampled.
                rectifiedFrameIndex += samplingStep;
            }
            m_saver.SaveSnapshot(rectifiedFrameIndex, m_view, m_depth, m_motion);
        }

        #endregion

        #region Private data
        Vector2Int m_downscaledDim;
        GameObject m_downscaledCameraObject;
        Camera m_cam;
        Camera m_downscaledCamera;

        /**
         * Downscaled motion texture.
         */
        RenderTexture m_motion;
        /**
         * Downscaled motion texture.
         */
        RenderTexture m_depth;
        /**
         * Original scene texture.
         */
        RenderTexture m_view;
        /**
         * Camera Frame buffer.
         */
        RenderTexture m_frameBuffer;

        CommandBuffer m_blitViewCommand;
        CommandBuffer m_blitDepthCommand;
        CommandBuffer m_blitMotionCommand;

        Util.SnapshotSaver m_saver;
        

        // Note: when m_currentFrameIndex starts at 0 the saving routines kicks in
        // before any rendering has actually been done,
        // resulting in empty textures. Starting at 1 prevents this.
        // Now, for the Motion Vectors we need one previously rendered image,
        // Because they are computed as the pixel-wise difference between two frames.
        // In conclusion, we should start at 2.
        // However, that does not work, since that results in the Motion Vectors not being
        // computed, for some reason? So we'll discard the first frame in a couple, and take only the second.
        int m_currentFrameIndex = 1;
        bool m_shouldUseQuickFix;
        
        
        #endregion
    }
}