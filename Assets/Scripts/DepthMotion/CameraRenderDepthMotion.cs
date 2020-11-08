using UnityEngine; 
using UnityEngine.Rendering; 
using UnityEngine.Assertions; 
using UnityEditor;
using System.IO;

namespace DepthMotion
{
    [RequireComponent(typeof(Camera))]
    public class CameraRenderDepthMotion : MonoBehaviour
    {
        public Vector2Int dim;
        [RangeAttribute(2, 8)]
        public int downscalingFactor;

        [RangeAttribute(1, 500)] public int samplingStep;
        
        #region MonoBehaviour Events

        void Awake()
        {
            AssertSystem();
            
            PrepareDims();
            PrepareTextures();
            PrepareCameras();
            
            CreateFileTree();
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
            if (m_currentFrameIndex % samplingStep == 0)
            {
                RenderDepthMotion();
            }
            ++m_currentFrameIndex;
        }
        
        void RenderDepthMotion()
        {
            // SaveFrame();
        }

        void FreeAll()
        {
            m_view.Release();
            m_motion.Release();
            m_depth.Release();

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
            m_cam.depthTextureMode = DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
            // Add callback on end of rendering of frame.
            // Used by Custom Rendering Piplines,
            // as a stand-in for OnPostRender.
            // RenderPipelineManager.endCameraRendering += EndCameraRendering;
            
            // For now will use Shader Replacement.
            m_blitViewCommand = new CommandBuffer   { name = "view" };
            m_blitDepthCommand  = new CommandBuffer { name = "depth" };
            m_blitMotionCommand = new CommandBuffer { name = "motion" };

            m_viewId   = Shader.PropertyToID(c_viewTag);
            m_depthId  = Shader.PropertyToID(c_depthTag);
            m_motionId = Shader.PropertyToID(c_motionTag);
            
            m_blitViewCommand.GetTemporaryRT(m_viewId, -1, -1, 0);
            m_blitDepthCommand.GetTemporaryRT(m_depthId, m_downscaledDim.x, m_downscaledDim.y, 0);
            m_blitMotionCommand.GetTemporaryRT(m_motionId, m_downscaledDim.x, m_downscaledDim.y, 0);
            
            m_blitViewCommand.SetGlobalTexture(c_viewTag, m_view);
            m_blitDepthCommand.SetGlobalTexture(c_depthTag, m_depthId);
            m_blitMotionCommand.SetGlobalTexture(c_motionTag, m_motionId);
            
            /*
            // should set each render target as active for the corresponding buffer
            // then signal that the latter should render in its active target.
            
            m_blitViewCommand.SetRenderTarget(m_view);
            m_blitDepthCommand.SetRenderTarget(m_depth);
            m_blitMotionCommand.SetRenderTarget(m_motion);

            // they actually included the english spelling for 'grey'. wow.
            m_blitViewCommand.ClearRenderTarget(true, true, Color.grey);
            m_blitDepthCommand.ClearRenderTarget(true, true, Color.green);
            m_blitMotionCommand.ClearRenderTarget(true, true, Color.blue);
            */

            m_blitViewCommand.Blit(
                BuiltinRenderTextureType.CameraTarget, 
                m_view
                );
            m_blitDepthCommand.Blit(
                BuiltinRenderTextureType.Depth, 
                m_depthId
                );
            m_blitMotionCommand.Blit(
                BuiltinRenderTextureType.MotionVectors, 
                m_motionId
                );
            
            // TODO: figure out at which CameraEvent we should get the motion vectors.
            // They seem to be available as soon as CameraEvent.BeforeForwardAlpha,
            // But they don't show anything..
            m_cam.AddCommandBuffer(
                CameraEvent.AfterEverything,
                m_blitViewCommand
                );
            m_cam.AddCommandBuffer(
                CameraEvent.AfterDepthTexture,
                m_blitDepthCommand
                );
            m_cam.AddCommandBuffer(
                CameraEvent.AfterEverything,
                m_blitMotionCommand
                );
            
            // Shader Replacement part.
            /*
            m_cam.targetTexture = m_view;
            m_downscaledCameraObject = new GameObject();
            m_downscaledCameraObject.SetActive(false);
            m_downscaledCamera = m_downscaledCameraObject.AddComponent<Camera>();
            */
            
        }

        void PrepareTextures()
        {
            m_view = new RenderTexture(
                dim.x, 
                dim.y, 
                0,
                RenderTextureFormat.ARGB32
                );

            m_motion = new RenderTexture(
                m_downscaledDim.x, 
                m_downscaledDim.y, 
                0,
                RenderTextureFormat.ARGB32
            );
            
            m_depth = new RenderTexture(
                m_downscaledDim.x, 
                m_downscaledDim.y, 
                16,
                RenderTextureFormat.ARGB32
                );

            m_view.Create();
            m_motion.Create();
            m_depth.Create();
            
            m_texture2D = new Texture2D(
                dim.x,
                dim.y, 
                TextureFormat.RGB24, 
                false, 
                false
                );
            m_downscaledTexture2D = new Texture2D(
                m_downscaledDim.x, 
                m_downscaledDim.y,
                TextureFormat.RGB24,
                false,
                false
                );
        }

        void AssertSystem()
        {
            Assert.IsTrue(SystemInfo.supportsMotionVectors);
        }
        #endregion

        #region Save routine

        void CreateFileTree()
        {
            string rootGuid = AssetDatabase.AssetPathToGUID(
                Path.Combine("Assets", "Editor", c_outputRootDir)
            );

            string timeStamp = System.DateTime.Now.ToString("yy_MM_dd_hhmmss");
            string outputDirGuid = AssetDatabase.CreateFolder(
                AssetDatabase.GUIDToAssetPath(rootGuid),
                timeStamp
            );

            m_viewDirGuid = AssetDatabase.CreateFolder(
                AssetDatabase.GUIDToAssetPath(outputDirGuid),
                c_viewDir
            );
            m_depthDirGuid = AssetDatabase.CreateFolder(
                AssetDatabase.GUIDToAssetPath(outputDirGuid),
                c_depthDir
            );
            m_motionDirGuid = AssetDatabase.CreateFolder(
                AssetDatabase.GUIDToAssetPath(outputDirGuid),
                c_motionDir
            );
        }

        void SaveFrame()
        {
            SaveTexture(
                m_view,  
                m_texture2D, 
                Path.Combine(
                    AssetDatabase.GUIDToAssetPath(m_viewDirGuid),
                    m_currentFrameIndex.ToString()
                )
            );
            
            SaveTexture(
                m_depth,  
                m_downscaledTexture2D, 
                Path.Combine(
                    AssetDatabase.GUIDToAssetPath(m_depthDirGuid),
                    m_currentFrameIndex.ToString()
                )
            );
            
            SaveTexture(
                m_motion,  
                m_downscaledTexture2D, 
                Path.Combine(
                    AssetDatabase.GUIDToAssetPath(m_motionDirGuid),
                    m_currentFrameIndex.ToString()
                )
            );
        }

        static void SaveTexture(RenderTexture renderTexture, Texture2D texture2D, string path)
        {
            const string extension = ".png";
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture2D.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(
                Path.ChangeExtension(path, extension), 
                texture2D.EncodeToPNG()
                );
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

        int m_motionId;
        int m_depthId;
        int m_viewId;
        

        CommandBuffer m_blitViewCommand;
        CommandBuffer m_blitDepthCommand;
        CommandBuffer m_blitMotionCommand;

        int m_currentFrameIndex;
        Texture2D m_texture2D;
        Texture2D m_downscaledTexture2D;
        
        string m_viewDirGuid;
        string m_motionDirGuid;
        string m_depthDirGuid;

        const string c_viewTag   = "_View";
        const string c_depthTag  = "_Depth";
        const string c_motionTag = "_Motion";

        const string c_outputRootDir = "OutputData";
        const string c_viewDir       = "View";
        const string c_motionDir     = "Motion";
        const string c_depthDir      = "Depth";

        #endregion
    }
}