using System;
using System.IO;

namespace PleaseUndo
{
    public class Logger
    {
        static string logPath = Environment.GetEnvironmentVariable("PU_LOG_FILE_PATH");
        static bool ignoreLog = Environment.GetEnvironmentVariable("PU_LOG_IGNORE") == null;
        static bool logToFile = Environment.GetEnvironmentVariable("PU_LOG_CREATE_FILE") != null;

        static bool firstLog = true;

        public static void Log(string fmt, params object[] args)
        {
            if (ignoreLog)
            {
                if (logToFile && logPath != null)
                {
                    LogToFile(string.Format(fmt, args));
                }
                else
                {
                    Console.WriteLine(string.Format(fmt, args));
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
                Log("Assertion Error Program Halted\n");
                throw new System.InvalidOperationException();
            }
        }

        public static void Assert(bool condition, string error_msg)
        {
            if (!condition)
            {
                Log("Assertion Error Program Halted: {0}\n", error_msg);
                throw new System.InvalidOperationException();
            }
        }
    }
}
