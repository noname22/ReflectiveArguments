using System;

namespace ReflectiveArguments.Examples.Simple
{
    class Program
    {
        static int Main(string[] args)
        {
            return Command.FromMethod<Program>(nameof(Example))
                .HandleCommandLine(args);
        }

        static void Example(string info, bool flag = false, int width = 320, int height = 240)
        {
            Console.WriteLine($"Width: {width}");
            Console.WriteLine($"Height: {height}");
            Console.WriteLine($"Info: {info}");
            Console.WriteLine($"Flag: {flag}");
        }
    }
}
