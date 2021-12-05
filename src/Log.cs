using System;
using System.IO;

namespace PleaseUndo
{
    public class Logger
    {
        static string logPath = Environment.GetEnvironmentVariable("PU_LOG_FILE_PATH");
        static string ignoreLog = Environment.GetEnvironmentVariable("PU_LOG_IGNORE");
        static string logToFile = Environment.GetEnvironmentVariable("PU_LOG_CREATE_FILE");
        static string logUseTimestamp = Environment.GetEnvironmentVariable("PU_LOG_USE_TIMESTAMP");

        static bool firstLog = true;

        public static void Log(string fmt, params object[] args)
        {
            if (ignoreLog == null)
            {
                string message = string.Format(fmt, args);

                if (logUseTimestamp != null)
                {
                    message = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff ") + message;
                }

                if (logToFile != null && logPath != null)
                {
                    LogToFile(message);
                }
                else
                {
                    Console.WriteLine(message);
                }
            }

        }

        public static void LogToFile(string message)
        {
            if (firstLog)
            {
                File.WriteAllText(logPath, message);
                firstLog = false;
            }
            else
            {
                File.AppendAllText(logPath, message);
            }
        }

        public static void Assert(bool condition)
        {
            if (!condition)
            {
                Log("Assertion Error!\n");
                throw new System.InvalidOperationException();
            }
        }

        public static void Assert(bool condition, string error_msg)
        {
            if (!condition)
            {
                Log("Assertion Error: {0}\n", error_msg);
                throw new System.InvalidOperationException();
            }
        }
    }
}
