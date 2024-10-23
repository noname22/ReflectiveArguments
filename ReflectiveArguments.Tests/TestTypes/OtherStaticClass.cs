using System.ComponentModel;

namespace ReflectiveArguments.Tests.TestTypes;
public static class OtherStaticClass
{
    public static void StaticMethod(string someArgument)
    {
        SomeArgument = someArgument;
    }

    public static void StaticMethodWithArgumentDescriptions([Description("This is an argument")] string someArgument)
    {
        SomeArgument = someArgument;
    }

    public static string SomeArgument { get; private set; }
}
