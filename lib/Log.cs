namespace PleaseUndo
{
    public class Logger
    {
        public static void Log(string fmt, params object[] args)
        {
            System.Console.WriteLine(string.Format(fmt, args));
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
