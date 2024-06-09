using Compiler.CodeProcessing.Scripts;

namespace Compiler.CodeProcessing.ErrorHandling
{

    public static class ErrorHandler
    {

        private static List<Script> _scriptsWithWarns = [];
        private static List<Script> _scriptsWithErrors = [];

        public static bool CompilationFailed => _scriptsWithErrors.Count > 0;

        public static void RegisterWithError(Script script)
        {
            if (!_scriptsWithErrors.Contains(script)) _scriptsWithErrors.Add(script);
        }
        public static void RegisterWithWarn(Script script)
        {
            if (!_scriptsWithWarns.Contains(script)) _scriptsWithWarns.Add(script);
        }

        public static Script[] GetScriptsWithErrors() => [.. _scriptsWithErrors];
        public static Script[] GetScriptsWithWarns() => [.. _scriptsWithWarns];

        public static void LogErrors()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write($"(/): {_scriptsWithErrors.Count}; ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"/!\\: {_scriptsWithWarns.Count};\n");
            Console.ResetColor();

            if (_scriptsWithWarns.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Warnings:");
                Console.ResetColor();

                foreach (var script in _scriptsWithWarns)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"\"{Path.GetFullPath(script.Path)}\":");
                    Console.ResetColor();
                    foreach (var e in script.Warnings)
                    {
                        Console.WriteLine(e);
                    }
                }
            }

            if (_scriptsWithErrors.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Errors:");
                Console.ResetColor();

                foreach (var script in _scriptsWithErrors)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"\"{Path.GetFullPath(script.Path)}\":");
                    Console.ResetColor();
                    foreach (var e in script.Errors)
                    {
                        Console.WriteLine(e);
                    }
                }
            }

        }

    }

}
