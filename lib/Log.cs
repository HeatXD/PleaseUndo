using System.IO;

namespace PleaseUndo
{
    public class Logger
    {
        static bool firstLog = true;
        public static void Log(string fmt, params object[] args)
        {

            LogToFile(string.Format(fmt, args));
        }

        public static void LogToFile(string message)
        {

        }
    }
}