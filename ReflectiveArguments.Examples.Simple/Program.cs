using ReflectiveArguments;
using System;

return Command.FromMethod(Example)
    .HandleCommandLine(args);

static void Example(string info, string[] additionalInfo, bool flag = false, int width = 320, int height = 240, string[] option = null)
{
    Console.WriteLine($"Width: {width}");
    Console.WriteLine($"Height: {height}");
    Console.WriteLine($"Info: {info}");
    Console.WriteLine($"Flag: {flag}");
    Console.WriteLine($"Additional Info: {string.Join(", ", additionalInfo)}");

    if (option is not null)
    {
        Console.WriteLine($"Options: {string.Join(", ", option)}");
    }
}