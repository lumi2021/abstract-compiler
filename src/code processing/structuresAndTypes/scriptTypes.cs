using Compiler.CodeProcessing.ErrorHandling;
using Compiler.CodeProcessing.Exeptions;

namespace Compiler.CodeProcessing.Scripts
{

    public abstract class Script(string path)
    {
        public string Path { get; private set; } = path;

        private List<BuildException> _errors = [];
        private List<BuildException> _warns = [];

        public BuildException[] Errors => [.. _errors];
        public BuildException[] Warnings => [.. _warns];

        public string Read() => File.ReadAllText( Path );

        public void ThrowError(BuildException errorException)
        {
            _errors.Add(errorException);
            ErrorHandler.RegisterWithError(this);
        }
        public void ThrowWarning(BuildException warnException)
        {
            _warns.Add(warnException);
            ErrorHandler.RegisterWithWarn(this);
        }
    }

    public class HeaderScript(string path, string asmPath) : Script(path)
    {
        public string assemblyPath = asmPath;
    }
    public class LibraryScript(string path) : Script(path) {}
    public class SourceScript(string path) : Script(path) {}

}
