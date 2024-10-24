﻿using System;
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

    internal Settings Settings { get; set; }
    internal List<Argument> ImplicitArguments { get; } = new List<Argument>();
    internal List<Argument> ExplicitArguments { get; } = new List<Argument>();
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

    public Command(string name, string description, Settings settings = null)
    {
        if (!IsValidName(name))
        {
            throw new ArgumentException("Name must only contain letters, numbers, underscores and dashes", nameof(name));
        }

        Name = name;
        Description = description;
        Settings = settings ?? new Settings();
    }

    public static Command FromMethod<T>(T bindDelegate, string description = null, string name = null,
        [CallerArgumentExpression("bindDelegate")] string callerName = null)
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

        return new Command(name, description).Bind(bindDelegate);
    }

    public Command Bind(Delegate bindDelegate)
    {
        boundDelegate = bindDelegate;

        foreach (var p in boundDelegate.GetMethodInfo().GetParameters())
        {
            var argument = Argument.FromParameterInfo(p);

            if (!SupportedDataTypes.Contains(argument.DataType) && !argument.DataType.IsEnum)
            {
                throw new ArgumentException($"Unsupported argument type: {argument.DataType}");
            }

            (argument.ArgumentType == ArgumentType.Explicit ? ExplicitArguments : ImplicitArguments)
                .Add(argument);
        }

        if (ImplicitArguments.Any(x => x.AcceptsMany)
            && (ImplicitArguments.Count(x => x.AcceptsMany) > 1 || !ImplicitArguments.Last().AcceptsMany))
        {
            throw new ArgumentException("Only the last implicit argument may accept many values");
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

    public Command AddCommand<T>(T bindDelegate, string description = null, string name = null,
        [CallerArgumentExpression("bindDelegate")] string callerName = null)
        where T : Delegate =>
        AddCommand(FromMethod(bindDelegate, description, name, callerName));

    public Task InvokeAsync(params string[] args) => InvokeAsync(new Queue<string>(args));
    public Task InvokeAsync(IEnumerable<string> args) => InvokeAsync(new Queue<string>(args));

    public async Task InvokeAsync(Queue<string> args)
    {
        var explicitValues = new Dictionary<string, object>();
        var implicitValues = new List<object>();

        int implicitIdx = 0;

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
                HandleExplicitArgument(explicitValues, arg);
            }
            else
            {
                if (implicitIdx >= ImplicitArguments.Count)
                {
                    throw new ParsingException($"Too many arguments for: {Name}. Expected {ImplicitArguments.Count}.", this);
                }

                var opt = ImplicitArguments[implicitIdx];
                var value = opt.ParseValue(arg, this);

                if (opt.AcceptsMany)
                {
                    if (implicitIdx >= implicitValues.Count)
                    {
                        implicitValues.Add(null);
                    }

                    implicitValues[implicitIdx] = ArrayConcat(implicitValues[implicitIdx], opt.DataType, value);
                }
                else
                {
                    implicitValues.Add(value);
                    implicitIdx++;
                }
            }
        }

        foreach (var opt in ExplicitArguments)
        {
            if (!explicitValues.ContainsKey(opt.Name))
            {
                explicitValues[opt.Name] = opt.DefaultValue;
            }
        }

        if (boundDelegate is not null)
        {
            var paramOrder = boundDelegate.GetMethodInfo().GetParameters().Select(x => x.Name).ToList();
            var explictValuesOrdered = explicitValues.OrderBy(x => paramOrder.IndexOf(x.Key)).Select(x => x.Value).ToArray();

            var paramValues = implicitValues.Concat(explictValuesOrdered).ToArray();

            if (implicitValues.Count != ImplicitArguments.Count)
            {
                throw new ParsingException($"Too few arguments for: {Name}. " +
                    $"Expected {ImplicitArguments.Count} but got {implicitValues.Count}.", this);
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

    void HandleExplicitArgument(Dictionary<string, object> values, string arg)
    {
        var optArg = arg.Substring(2).Split('=');

        if (optArg.Length > 2)
        {
            throw new ParsingException($"Expected --option=VALUE or --flag", this);
        }

        string kebabName = optArg[0];

        var opt = ExplicitArguments.FirstOrDefault(x => x.Name.ToKebabCase() == kebabName);

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
