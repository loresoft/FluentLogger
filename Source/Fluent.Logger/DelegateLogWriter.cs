using System;

namespace Fluent.Logger
{
    public class DelegateLogWriter : ILogWriter
    {
        private readonly Action<LogData> _logAction;

        public DelegateLogWriter() : this(null)
        {
        }

        public DelegateLogWriter(Action<LogData> logAction)
        {
            _logAction = logAction ?? DebugWrite;
        }

        public void WriteLog(LogData logData)
        {
            _logAction?.Invoke(logData);
        }

        private static void DebugWrite(LogData logData)
        {
            System.Diagnostics.Debug.WriteLine(logData);
        }

    }
}