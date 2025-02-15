using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ReflectiveArguments;

public class Command
{
    Delegate boundDelegate;
    Help help;

    internal ReflectiveArgumentSettings Settings { get; set; }
    internal List<Parameter> Arguments { get; } = new List<Parameter>();
    internal List<Parameter> Options { get; } = new List<Parameter>();
    internal List<Command> SubCommands { get; } = new List<Command>();
    public Command Parent { get; set; }
    public IEnumerable<string> Path => Parent?.Path?.Append(Name) ?? new[] { Name };
    public string Name { get; }
    public string FullName => string.Join(" ", Path);
    public string Description { get; }
    public bool IsBound => boundDelegate is not null;

    private static List<Type> SupportedDataTypes = new() {
        typeof(byte),
        typeof(short),
        typeof(int),
        typeof(long),
        typeof(ushort),
        typeof(uint),
        typeof(ulong),
        typeof(string),
        typeof(bool),
    };

    public Command(string name, string description, ReflectiveArgumentSettings settings = null)
    {
        if (!IsValidName(name))
        {
            throw new ArgumentException("Name must only contain letters, numbers, underscores and dashes", nameof(name));
        }

        Name = name;
        Description = description;
        Settings = settings ?? new ReflectiveArgumentSettings();
    }

    public static Command FromMethod<T>(T bindDelegate, string description = null, string name = null,
        [CallerArgumentExpression("bindDelegate")] string callerName = null, ReflectiveArgumentSettings settings = null)
        where T : Delegate
    {
        if (name is null)
        {
            name = bindDelegate.GetMethodInfo().Name.ToKebabCase();

            if (!IsValidName(name))
            {
                // If eg. a private method is passed in, the name will be invalid.
                // Try to determine name by caller argument instead.

                name = callerName.Split(".").Last().ToKebabCase();

                if (!IsValidName(name))
                {
                    throw new ArgumentException("Name of method could not be determined, please specify.", nameof(bindDelegate));
                }
            }
        }

        return new Command(name, description, settings).Bind(bindDelegate);
    }

    public Command Bind(Delegate bindDelegate)
    {
        boundDelegate = bindDelegate;

        foreach (var p in boundDelegate.GetMethodInfo().GetParameters())
        {
            var parameter = Parameter.FromParameterInfo(p);

            if (!SupportedDataTypes.Contains(parameter.DataType) && !parameter.DataType.IsEnum)
            {
                throw new ArgumentException($"Unsupported argument type: {parameter.DataType}");
            }

            (parameter.ParameterType == ParameterType.Option ? Options : Arguments)
                .Add(parameter);
        }

        if (Arguments.Any(x => x.AcceptsMany)
            && (Arguments.Count(x => x.AcceptsMany) > 1 || !Arguments.Last().AcceptsMany))
        {
            throw new ArgumentException("Only the last argument may accept many values");
        }

        return this;
    }

    public Command AddCommand(Command command)
    {
        if(Arguments.Count > 0)
        {
            throw new ArgumentException("Can't add sub-command to a command with arguments", nameof(command));
        }

        command.Parent = this;
        command.Settings = Settings;
        SubCommands.Add(command);
        return this;
    }

    public Command AddCommand<T>(T bindDelegate, string description = null, string name = null,
        [CallerArgumentExpression("bindDelegate")] string callerName = null)
        where T : Delegate =>
        AddCommand(FromMethod(bindDelegate, description, name, callerName));

    public Task InvokeAsync(params string[] args) => InvokeAsync(new Queue<string>(args));
    public Task InvokeAsync(IEnumerable<string> args) => InvokeAsync(new Queue<string>(args));

    public async Task InvokeAsync(Queue<string> args)
    {
        var optionValues = new Dictionary<string, object>();
        var argumentValues = new List<object>();

        int argumentIdx = 0;

        if (help is null && Settings.AutoHelp)
        {
            help = new Help(this);
        }

        while (args.Any())
        {
            var arg = args.Dequeue();
            var subCmd = SubCommands.FirstOrDefault(x => x.Name == arg);

            if (help is not null && arg == "--help" || arg == "--help=true")
            {
                help.ShowHelp();
                return;
            }
            else if (boundDelegate is null && subCmd is null)
            {
                throw new ParsingException($"No such command in this context: {arg}", this);
            }
            else if (subCmd is not null)
            {
                await subCmd.InvokeAsync(args);
                return;
            }
            else if (arg.StartsWith("--"))
            {
                HandleOption(optionValues, arg);
            }
            else
            {
                if (argumentIdx >= Arguments.Count)
                {
                    throw new ParsingException($"Too many arguments for: {Name}. Expected {Arguments.Count}.", this);
                }

                var opt = Arguments[argumentIdx];
                var value = opt.ParseValue(arg, this);

                if (opt.AcceptsMany)
                {
                    if (argumentIdx >= argumentValues.Count)
                    {
                        argumentValues.Add(null);
                    }

                    argumentValues[argumentIdx] = ArrayConcat(argumentValues[argumentIdx], opt.DataType, value);
                }
                else
                {
                    argumentValues.Add(value);
                    argumentIdx++;
                }
            }
        }

        foreach (var opt in Options)
        {
            if (!optionValues.ContainsKey(opt.Name))
            {
                optionValues[opt.Name] = opt.DefaultValue;
            }
        }

        if (boundDelegate is not null)
        {
            var paramOrder = boundDelegate.GetMethodInfo().GetParameters().Select(x => x.Name).ToList();
            var optionValuesOrdered = optionValues.OrderBy(x => paramOrder.IndexOf(x.Key)).Select(x => x.Value).ToArray();

            var paramValues = argumentValues.Concat(optionValuesOrdered).ToArray();

            if (argumentValues.Count != Arguments.Count)
            {
                throw new ParsingException($"Too few arguments for: {Name}. " +
                    $"Expected {Arguments.Count} but got {argumentValues.Count}.", this);
            }

            var returned = boundDelegate.DynamicInvoke(paramValues);

            if (returned is Task task)
            {
                await task;
            }
        }
        else
        {
            throw new ParsingException(Parent is null ? "No command specified" : "No sub-command specified", this);
        }
    }

    public async Task<int> HandleCommandLineAsync(IEnumerable<string> args)
    {
        try
        {
            await InvokeAsync(args);
        }
        catch (ParsingException ex)
        {
            Settings.LogError(ex.Message);

            string help = string.Join(" ", ex.Command.Path.Append("--help"));
            Settings.LogInfo($"See: {help} for more information");

            return 1;
        }

        return 0;
    }

    public int HandleCommandLine(IEnumerable<string> args) => HandleCommandLineAsync(args).GetAwaiter().GetResult();

    void HandleOption(Dictionary<string, object> values, string arg)
    {
        var optArg = arg.Substring(2).Split('=');

        if (optArg.Length > 2)
        {
            throw new ParsingException($"Expected --option=VALUE or --flag", this);
        }

        string kebabName = optArg[0];

        var opt = Options.FirstOrDefault(x => x.Name.ToKebabCase() == kebabName);

        if (opt is null)
        {
            throw new ParsingException($"No such option: {kebabName}", this);
        }

        if (optArg.Length == 1 && opt.DataType != typeof(bool))
        {
            throw new ParsingException($"Option {kebabName} is not a boolean and can not be specified as a flag", this);
        }

        try
        {
            var value = optArg.Length == 1 ? true : opt.ParseValue(optArg[1], this);

            if (values.TryGetValue(opt.Name, out var currentValue) && !opt.AcceptsMany)
            {
                throw new ParsingException($"{kebabName} has already been specified", this);
            }

            if (opt.AcceptsMany)
            {
                values[opt.Name] = ArrayConcat(currentValue, opt.DataType, value);
            }
            else
            {
                values.Add(opt.Name, value);
            }
        }
        catch (InvalidCastException ex)
        {
            throw new ParsingException($"Expected option {kebabName} to be of type {opt.DataType}", this, ex);
        }
    }

    private static bool IsValidName(string name)
    {
        return Regex.IsMatch(name, @"^[a-zA-Z0-9_\-]*$");
    }

    Array ArrayConcat(object array, Type type, object value)
    {
        var newArray = Array.CreateInstance(type, (((Array)array)?.Length ?? 0) + 1);

        if (array != null)
        {
            Array.Copy((Array)array, newArray, newArray.Length - 1);
        }

        newArray.SetValue(value, newArray.Length - 1);

        return newArray;
    }
}
