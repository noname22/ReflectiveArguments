namespace ReflectiveArguments.Tests.TestTypes;
public static class OtherStaticClass
{
    public static void StaticMethod(string someArgument)
    {
        SomeArgument = someArgument;
    }

    public static string SomeArgument { get; private set; }
}
