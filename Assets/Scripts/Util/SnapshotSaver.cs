using UnityEngine;

using System;
using System.IO;

using ImageSynthesis;

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

        const string c_extension = ".png";
        
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

        public void Save(int frameIndex, FrameCapturer.PassKind kind, byte[] data, int length)
        {
            CreateFile(data, length, GetPassKindDirectory(kind), frameIndex.ToString(), c_extension);
        }

        string GetPassKindDirectory(FrameCapturer.PassKind kind)
        {
            switch (kind)
            {
            case FrameCapturer.PassKind.EImage: return Path.Combine(m_outputPath, c_viewDir);
            case FrameCapturer.PassKind.EDepth: return Path.Combine(m_outputPath, c_depthDir);
            case FrameCapturer.PassKind.EFlow: return Path.Combine(m_outputPath, c_motionDir);
            default: throw new ArgumentException("Nope.");
            }
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
        static void CreateFile(byte[] data, int length, string dirPath, string fileName, string extension = "")
        {
            byte[] subData = new byte[length];
            Array.Copy(data, subData, length);
            File.WriteAllBytes(
                GetProjectRootFolderPath(Path.Combine(dirPath, Path.ChangeExtension(fileName, extension))),
                subData
                );
            
        }

        #endregion
    }
}