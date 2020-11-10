using UnityEngine;
using Unity.Collections;

using System;
using System.IO;

namespace Util
{
    public class SnapshotSaver
    {

        #region Paths

        string m_outputPath;

        const string c_outputRootDir = "OutputData";
        const string c_viewDir       = "View";
        const string c_motionDir     = "Motion";
        const string c_depthDir      = "Depth";

        #endregion
        
        #region File Tree creation 
        
        public void CreateFileTree()
        {
            if (!DoesFolderExist("Editor"))
            {
               CreateFolder("", "Editor"); 
            }
            if (!DoesFolderExist(c_outputRootDir))
            {
               CreateFolder("Editor", c_outputRootDir); 
            }
            string timeStamp = DateTime.Now.ToString("yy_MM_dd_hhmmss");

            string outputRootPath = Path.Combine("Editor", c_outputRootDir);
            m_outputPath = CreateFolder(outputRootPath, timeStamp);

            CreateFolder(m_outputPath, c_viewDir);
            CreateFolder(m_outputPath, c_depthDir);
            CreateFolder(m_outputPath, c_motionDir);
        }
        
        #endregion
        
        #region Saving routine

        public void CreateTextures(Vector2Int dim, Vector2Int downscaledDim)
        {
            // Important:
            // Don't change the texture format here,
            // It looks like it is properly saved only
            // only when it's TextureFormat.RBG24, even for depth..
            m_viewTexture2D = new Texture2D(
                dim.x,
                dim.y, 
                TextureFormat.RGB24, 
                false, 
                false
                );
            m_depthTexture2D = new Texture2D(
                downscaledDim.x, 
                downscaledDim.y,
                TextureFormat.R16,
                false,
                false
                );
            m_motionTexture2D = new Texture2D(
                downscaledDim.x, 
                downscaledDim.y,
                TextureFormat.RGHalf,
                false,
                false
                );
        }
        
        public void SaveSnapshot(int frameIndex, RenderTexture view, RenderTexture depth, RenderTexture motion)
        {
            string fileName = frameIndex.ToString();
            SaveTexture(
                view,  
                m_viewTexture2D, 
                Path.Combine(m_outputPath, c_viewDir),
                fileName
            );

            SaveTexture(
                depth,
                m_depthTexture2D,
                Path.Combine(m_outputPath, c_depthDir),
                fileName
            );
            
            SaveTexture(
                motion,  
                m_motionTexture2D, 
                Path.Combine(m_outputPath, c_motionDir),
                fileName
            );
        }

        static void SaveTexture(RenderTexture renderTexture, Texture2D texture2D, string dirPath, string fileName)
        {
            const string extension = ".png";
            var previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            texture2D.ReadPixels(
                new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture2D.Apply();
            RenderTexture.active = previous;
            CreateFile(texture2D.EncodeToPNG(), dirPath, fileName, extension);
        }

        // todo: Find a way to preserve negative values in Motion Vector texture.
        static void SaveTextureMotion(RenderTexture renderTexture, Texture2D texture2D, string dirPath, string fileName)
        {
            const string extension = ".png";
            var previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            texture2D.ReadPixels(
                new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture2D.Apply();
            RenderTexture.active = previous;
            CreateFile(RemapMotionImageInPlace(texture2D.EncodeToPNG()), dirPath, fileName, extension);
        }

        /**
         * From representation of [-1, 1] to [0, 2]??
         */
        static byte[] RemapMotionImageInPlace(byte[] exrData)
        {
            const byte added = 127;
            for (int i = 0; i < exrData.Length; ++i)
            {
                exrData[i] += added;
            }

            return exrData;
        }

        #endregion
        
        #region IO utility


        /**
         * @returns System path of root dir (child of Project/ dir)
         * @warning Only works in Editor mode.
         */
        static string GetProjectRootFolderPath(string rootDir)
        {
            return Path.Combine(Path.GetDirectoryName(Application.dataPath), rootDir);
        }

        /**
         * Create a folder in Project from root directory.
         * 
         * @param parentPath Name of the parent path from Project root.
         * @param dir Name of the folder to create.
         *
         * @returns Path from Project root of newly created directory.
         */
        static string CreateFolder(string parentPath, string dir)
        {
            Directory.CreateDirectory(Path.Combine(GetProjectRootFolderPath(parentPath), dir));
            return Path.Combine(parentPath, dir);
        }

        /**
         * Checks whether the Project directory exists.
         * @param dirPath Path of the dir from Project root.
         */
        static bool DoesFolderExist(string dirPath)
        {
            return Directory.Exists(GetProjectRootFolderPath(dirPath));
        }

        /**
         * Create a file and write data to it.
         * @param filePath Path of file from Project root.
         * @param data Byte array of data.
         */
        static void CreateFile(byte[] data, string dirPath, string fileName, string extension = "")
        {
            File.WriteAllBytes(
                GetProjectRootFolderPath(Path.Combine(dirPath, Path.ChangeExtension(fileName, extension))),
                data
                );
            
        }

        #endregion
        
        #region Private data
        
        // Texture2D's are used to save the RenderTextures to files.
        Texture2D m_viewTexture2D;
        Texture2D m_depthTexture2D;
        Texture2D m_motionTexture2D;
        
        #endregion
    }
}