using System;
using System.IO;
using Colossal.Logging;

namespace Advanced_Graphics
{
    // Own log file: the game's logger broke (NullReferenceException in UnityLogger.Internal_WriteStream) and then showed error windows.
    public sealed class ModLog
    {
        private static readonly object s_Lock = new object();
        private static string s_Path;
        private readonly ILog m_Log;

        internal ModLog(ILog log) { m_Log = log; }

        internal static bool Enabled => Mod.ActiveSetting?.EnableLogOutput == true;

        // Called once on the main thread (Application.persistentDataPath is main-thread only).
        internal static void Initialize(string dataPath)
        {
            try
            {
                string folder = Path.Combine(dataPath, "Logs");
                Directory.CreateDirectory(folder);
                lock (s_Lock)
                {
                    s_Path = Path.Combine(folder, "Enhanced Graphics and Textures - Details.log");
                    File.WriteAllText(s_Path, string.Empty);
                }
            }
            catch (Exception) { s_Path = null; }
        }

        private static void Write(string level, string message)
        {
            if (s_Path == null) return;
            try
            {
                lock (s_Lock)
                    File.AppendAllText(s_Path, "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss,fff") + "] [" + level + "]  " + message + Environment.NewLine);
            }
            catch (Exception) { }
        }

        public void Info(string message)
        {
            if (Enabled) Write("INFO", message);
        }

        public void Warn(string message)
        {
            if (Enabled) Write("WARN", message);
        }

        public void Error(Exception exception, string message) =>
            Write("ERROR", message + (exception != null ? Environment.NewLine + exception : string.Empty));
    }
}
