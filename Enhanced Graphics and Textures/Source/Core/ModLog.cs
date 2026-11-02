using System;
using Colossal.Logging;

namespace Advanced_Graphics
{
    // Info and warnings only when log output is enabled in the options; errors are always written.
    public sealed class ModLog
    {
        private readonly ILog m_Log;

        internal ModLog(ILog log) { m_Log = log; }

        internal static bool Enabled => Mod.ActiveSetting?.EnableLogOutput == true;

        public void Info(string message)
        {
            if (Enabled) m_Log.Info(message);
        }

        public void Warn(string message)
        {
            if (Enabled) m_Log.Warn(message);
        }

        public void Error(Exception exception, string message) => m_Log.Error(exception, message);
    }
}
