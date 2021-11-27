using System.IO;

namespace PleaseUndo
{
    public class Logger
    {
        const string logPath = "pu_log_info.txt";
        static bool logToFile = true;

        static bool firstLog = true;

        public static void Log(string fmt, params object[] args)
        {
            if (logToFile)
            {
                LogToFile(string.Format(fmt, args));
            }
            else
            {
                System.Console.WriteLine(string.Format(fmt, args));
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
                throw new System.InvalidOperationException();
            }
        }
    }
}
