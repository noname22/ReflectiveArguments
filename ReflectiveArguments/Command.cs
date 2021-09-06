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
        Help help;

        internal Settings Settings { get; set; }
        internal List<Argument> ImplicitArguments { get; } = new List<Argument>();
        internal List<Argument> ExplicitArguments { get; } = new List<Argument>();
        internal List<Command> SubCommands { get; } = new List<Command>();
        public Command Parent { get; set; }
        public IEnumerable<string> Path => Parent?.Path?.Append(Name) ?? new[] { Name };
        public string Name { get; }
        public string Description { get; }
        public bool IsBound => boundMethod != null;

        public Command(string name, string description, Settings settings = null)
        {
            Name = name;
            Description = description;
            Settings = settings ?? new Settings();
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
                (argument.ArgumentType == ArgumentType.Explicit ? ExplicitArguments : ImplicitArguments)
                    .Add(argument);
            }

            return this;
        }

        public Command AddCommand(Command command)
        {
            command.Parent = this;
            command.Settings = Settings;
            SubCommands.Add(command);
            return this;
        }

        public Command AddCommand<T>(T instance, string methodName, string description = null, string name = null) =>
            AddCommand(new Command(name ?? methodName.ToKebabCase(), description).Bind(instance, methodName));

        public Command AddCommand<T>(Func<T> getInstance, string methodName, string description = null, string name = null) =>
            AddCommand(new Command(name ?? methodName.ToKebabCase(), description).Bind(getInstance, methodName));

        public Command AddCommand<T>(string methodName, string description = null, string name = null) =>
            AddCommand(new Command(name ?? methodName.ToKebabCase(), description).Bind<T>(methodName));

        public void Invoke(params string[] args) => Invoke(new Queue<string>(args));
        public void Invoke(IEnumerable<string> args) => Invoke(new Queue<string>(args));

        public void Invoke(Queue<string> args)
        {
            Dictionary<string, object> explicitValues = new Dictionary<string, object>();
            List<object> implicitValues = new List<object>();

            foreach (var opt in ExplicitArguments)
            {
                explicitValues[opt.Name] = opt.DefaultValue;
            }

            int implicitIdx = 0;

            if(help == null && Settings.AutoHelp)
            {
                help = new Help(this);
            }

            while (args.Any())
            {
                var arg = args.Dequeue();
                var subCmd = SubCommands.FirstOrDefault(x => x.Name == arg);

                if (help != null && arg == "--help" || arg == "--help=true")
                {
                    help.ShowHelp();
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
                    if(implicitIdx >= ImplicitArguments.Count)
                    {
                        throw new ParsingException($"Too many arguments for: {Name}. Expected {ImplicitArguments.Count}.");
                    }

                    var opt = ImplicitArguments[implicitIdx];
                    implicitValues.Add(opt.ParseValue(arg));
                    implicitIdx++;
                }
            }

            if (boundMethod != null)
            {
                var paramOrder = boundMethod.GetParameters().Select(x => x.Name).ToList();
                var explictValuesOrdered = explicitValues.OrderBy(x => paramOrder.IndexOf(x.Key)).Select(x => x.Value).ToArray();
                var paramValues = implicitValues.Concat(explictValuesOrdered).ToArray();

                if (implicitValues.Count != ImplicitArguments.Count)
                {
                    throw new ParsingException($"Too few arguments for: {Name}. " +
                        $"Expected {ImplicitArguments.Count} but got {implicitValues.Count}.");
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
                Settings.LogError(ex.Message);
                Settings.LogInfo("See --help for more information");

                return 1;
            }

            return 0;
        }

        void HandleExplicitArgument(Dictionary<string, object> values, string arg)
        {
            var optArg = arg.Substring(2).Split('=');

            if (optArg.Length > 2)
            {
                throw new ParsingException($"Expected --option=VALUE or --flag");
            }

            string kebabName = optArg[0];

            var opt = ExplicitArguments.FirstOrDefault(x => x.Name.ToKebabCase() == kebabName);

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
