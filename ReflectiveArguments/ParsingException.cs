using System;

namespace ReflectiveArguments;

public class ParsingException : Exception
{
    public Command Command { get; set; }
    public ParsingException(string message, Command command, Exception innerException = null) : base(message, innerException) => Command = command;
}
