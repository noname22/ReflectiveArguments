using System;

namespace ReflectiveArguments
{
    public class Settings
    {
        public Action<string> LogInfo { get; set; } = Console.WriteLine;
        public Action<string> LogError { get; set; } = Console.Error.WriteLine;
        public bool AutoHelp { get; set; } = true;
    }
}
