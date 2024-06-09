namespace Compiler.CodeProcessing.Exeptions;


public abstract class BuildException() : ApplicationException("") {}

public abstract class SyntaxException() : BuildException() {}
public abstract class EvaluationException() : BuildException() {}
public abstract class CompilingException() : BuildException() {}

public class UnexpectedTokenException() : SyntaxException() {}

public class MoreThanOneNamespaceException() : EvaluationException() {}
public class OutOfPlaceCodeException() : EvaluationException() {}
public class InvalidNamingException() : EvaluationException() {}

public abstract class NotFoundException() : EvaluationException() {}

public class MethodNotFoundException() : NotFoundException() {}
public class FieldNotFoundException() : NotFoundException() {}
public class LocalVariableNotFoundException() : NotFoundException() {}

public class InvalidInstructionException() : EvaluationException() {}

// assembly
public class AssemblyInvalidInstructionException() : EvaluationException() {}
public class AssemblyArgumentsMissingException() : EvaluationException() {}
public class AssemblyTooManyArgumentsException() : EvaluationException() {}
public class AssemblyNotAllowedTypeException() : EvaluationException() {}


// dev exeptions
public class InstructionProcessingNotImplementedException() : EvaluationException() {}
