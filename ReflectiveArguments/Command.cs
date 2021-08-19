using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ReflectiveArguments
{
    public class Command
    {
        MethodInfo boundMethod;
        Func<object> getInstance;
        Settings settings;

        List<Argument> implicitArguments = new List<Argument>();
        List<Argument> explicitArguments = new List<Argument>();
        List<Command> subCommands = new List<Command>();

        public Command Parent { get; set; }
        protected IEnumerable<string> Path => Parent?.Path?.Append(Name) ?? new[] { Name };
        public string Name { get; }
        public string Description { get; }

        public Command(string name, string description, Settings settings = null)
        {
            Name = name;
            Description = description;
            this.settings = settings ?? new Settings();
        }

        public static Command FromMethod<T>(T instance, string methodName, string description = null, string name = null) =>
            new Command(name ?? methodName.ToKebabCase(), description).Bind(instance, methodName);

        public static Command FromMethod<T>(Func<T> getInstance, string methodName, string description = null, string name = null) =>
            new Command(name ?? methodName.ToKebabCase(), description).Bind(getInstance, methodName);

        public static Command FromMethod<T>(string methodName, string description = null, string name = null) =>
            new Command(name ?? methodName.ToKebabCase(), description).Bind<T>(methodName);

        public Command Bind<T>(T instance, string methodName) => Bind(typeof(T), () => instance, methodName);
        public Command Bind<T>(Func<T> getInstance, string methodName) => Bind(typeof(T), () => getInstance(), methodName);
        public Command Bind<T>(string methodName) => Bind(typeof(T), null, methodName);

        public Command Bind(Type type, Func<object> getInstance, string methodName)
        {
            if (!type.IsClass)
            {
                throw new ArgumentException("Expected a class", type.Name);
            }

            boundMethod = type.GetMethod(methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

            if (boundMethod == null)
            {
                throw new ArgumentException($"{type.Name} does not have a method: {methodName}");
            }

            if (!boundMethod.IsStatic && getInstance == null)
            {
                throw new ArgumentException($"Instance or instance provider must be supplied for non-static methods");
            }

            if (!boundMethod.IsStatic)
            {
                this.getInstance = getInstance == null ? (() => Activator.CreateInstance(type)) : getInstance;
            }

            foreach (var p in boundMethod.GetParameters())
            {
                var argument = Argument.FromParameterInfo(p);
                (argument.ArgumentType == ArgumentType.Explicit ? explicitArguments : implicitArguments)
                    .Add(argument);
            }

            return this;
        }

        public Command AddCommand(Command command)
        {
            command.Parent = this;
            command.settings = settings;
            subCommands.Add(command);
            return this;
        }

        public Command AddCommand<T>(T instance, string methodName, string description = null, string name = null) =>
            AddCommand(new Command(name ?? methodName.ToKebabCase(), description).Bind(instance, methodName));

        public Command AddCommand<T>(Func<T> getInstance, string methodName, string description = null, string name = null) =>
            AddCommand(new Command(name ?? methodName.ToKebabCase(), description).Bind(getInstance, methodName));

        public Command AddCommand<T>(string methodName, string description = null, string name = null) =>
            AddCommand(new Command(name ?? methodName.ToKebabCase(), description).Bind<T>(methodName));

        public List<string> GetHelp()
        {
            var ret = new List<string>();
            ret.Add($"{string.Join(' ', Path)} - {Description}");
            ret.Add(string.Empty);

            var options = explicitArguments.Select(x => (Left: $"--{x.KebabName}={x.DataType.Name}", Right: x.Description));
            var arguments = implicitArguments.Select(x => (Left: $"{x.SnakeName} ({x.DataType.Name})", Right: x.Description));

            var padBy = subCommands
                .Select(x => x.Name)
                .Concat(arguments.Select(x => x.Left))
                .Concat(options.Select(x => x.Left));

            int pad = (padBy.Any() ? padBy.Max(x => x.Length) : 0) + 2;

            if (boundMethod != null)
            {
                var serialOpts = options.Any()
                    ? string.Join(" ", options.Select(x => $"({x.Left})")) + " "
                    : string.Empty;

                var serialArguments = implicitArguments.Any()
                    ? string.Join(" ", implicitArguments.Select(x => $"{x.SnakeName}"))
                    : string.Empty;

                ret.Add($"usage: {string.Join(" ", Path)} {serialOpts}{serialArguments}");
                ret.Add(string.Empty);
            }

            if (arguments.Any())
            {
                ret.Add("Arguments:");

                foreach (var arg in arguments)
                {
                    ret.Add($"  {arg.Left.PadRight(pad)} {arg.Right}");
                }

                ret.Add(string.Empty);
            }

            if (options.Any())
            {
                ret.Add("Options:");

                foreach (var opt in options)
                {
                    ret.Add($"  {opt.Left.PadRight(pad)} {opt.Right}");
                }

                ret.Add(string.Empty);
            }

            if (subCommands.Any())
            {
                ret.Add("Commands:");

                foreach (var cmd in subCommands)
                {
                    ret.Add($"  {cmd.Name.PadRight(pad)} {cmd.Description}");
                }

                ret.Add(string.Empty);
            }

            return ret;
        }

        public void Invoke(params string[] args) => Invoke(new Queue<string>(args));
        public void Invoke(IEnumerable<string> args) => Invoke(new Queue<string>(args));

        public void Invoke(Queue<string> args)
        {
            Dictionary<string, object> explicitValues = new Dictionary<string, object>();
            List<object> implicitValues = new List<object>();

            foreach (var opt in explicitArguments)
            {
                explicitValues[opt.Name] = opt.DefaultValue;
            }

            int implicitIdx = 0;

            while (args.Any())
            {
                var arg = args.Dequeue();
                var subCmd = subCommands.FirstOrDefault(x => x.Name == arg);

                if (arg == "--help" || arg == "--help=true")
                {
                    ShowHelp();
                    return;
                }
                else if (boundMethod == null && subCmd == null)
                {
                    throw new ParsingException($"No such command in this context: {arg}");
                }
                else if (subCmd != null)
                {
                    subCmd.Invoke(args);
                    return;
                }
                else if (arg.StartsWith("--"))
                {
                    HandleExplicitArgument(explicitValues, arg);
                }
                else
                {
                    if(implicitIdx >= implicitArguments.Count)
                    {
                        throw new ParsingException($"Too many arguments for: {Name}. Expected {implicitArguments.Count}.");
                    }

                    var opt = implicitArguments[implicitIdx];
                    implicitValues.Add(opt.ParseValue(arg));
                    implicitIdx++;
                }
            }

            if (boundMethod != null)
            {
                var paramOrder = boundMethod.GetParameters().Select(x => x.Name).ToList();
                var explictValuesOrdered = explicitValues.OrderBy(x => paramOrder.IndexOf(x.Key)).Select(x => x.Value).ToArray();
                var paramValues = implicitValues.Concat(explictValuesOrdered).ToArray();

                if (implicitValues.Count != implicitArguments.Count)
                {
                    throw new ParsingException($"Too few arguments for: {Name}. " +
                        $"Expected {implicitArguments.Count} but got {implicitValues.Count}.");
                }

                boundMethod.Invoke(getInstance != null ? getInstance() : null, paramValues);
            }
            else
            {
                throw new ParsingException("No command specified");
            }
        }

        public int HandleCommandLine(IEnumerable<string> args)
        {
            try
            {
                Invoke(args);
            }
            catch (ParsingException ex)
            {
                settings.LogError(ex.Message);
                settings.LogInfo("See --help for more information");

                return 1;
            }

            return 0;
        }

        void ShowHelp()
        {
            foreach (var line in GetHelp())
            {
                settings.LogInfo(line);
            }
        }

        void HandleExplicitArgument(Dictionary<string, object> values, string arg)
        {
            var optArg = arg.Substring(2).Split('=');

            if (optArg.Length > 2)
            {
                throw new ParsingException($"Expected --option=VALUE or --flag");
            }

            string kebabName = optArg[0];

            var opt = explicitArguments.FirstOrDefault(x => x.Name.ToKebabCase() == kebabName);

            if (opt == null)
            {
                throw new ParsingException($"No such option: {kebabName}");
            }

            if (optArg.Length == 1 && opt.DataType != typeof(bool))
            {
                throw new ParsingException($"Option {kebabName} is not a boolean and can not be specified as a flag");
            }

            try
            {
                values[opt.Name] = optArg.Length == 1 ? true : opt.ParseValue(optArg[1]);
            }
            catch (InvalidCastException e)
            {
                throw new ParsingException($"Expected option {kebabName} to be of type {opt.DataType}", e);
            }
        }
    }
}
