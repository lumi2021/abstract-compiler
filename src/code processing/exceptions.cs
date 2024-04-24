namespace Compiler.CodeProcessing.Exeptions;


public abstract class BuildException(string message) : ApplicationException(message) {}

public abstract class SyntaxException(string message) : BuildException(message) {}
public abstract class EvaluationException(string message) : BuildException(message) {}
public abstract class CompilingException(string message) : BuildException(message) {}

public class MoreThanOneNamespaceException(string message) : EvaluationException(message) {}
public class OutOfPlaceCodeException(string message) : EvaluationException(message) {}
public class InvalidNamingException(string message) : EvaluationException(message) {}


public class InvalidInstructionException(string message) : EvaluationException(message) {}

// assembly
public class AssemblyInvalidInstructionException(string message) : EvaluationException(message) {}
public class AssemblyArgumentsMissingException(string message) : EvaluationException(message) {}
public class AssemblyTooManyArgumentsException(string message) : EvaluationException(message) {}
public class AssemblyNotAllowedTypeException(string message) : EvaluationException(message) {}

