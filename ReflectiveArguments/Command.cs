using System;
using System.Collections.Generic;
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
    public string Description { get; }
    public bool IsBound => boundDelegate is not null;

    public Command(string name, string description, Settings settings = null)
    {
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
            if (callerName is null || !Regex.IsMatch(callerName, "^[a-zA-Z0-9_]*$"))
            {
                throw new ArgumentException("Name of method could not be determined, please specify.", nameof(bindDelegate));
            }

            name = callerName.ToKebabCase();
        }

        return new Command(name, description).Bind(bindDelegate);
    }

    public Command Bind(Delegate bindDelegate)
    {
        boundDelegate = bindDelegate;

        foreach (var p in boundDelegate.GetMethodInfo().GetParameters())
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

    public Command AddCommand<T>(T bindDelegate, string description = null, string name = null,
        [CallerArgumentExpression("bindDelegate")] string callerName = null)
        where T : Delegate =>
        AddCommand(FromMethod(bindDelegate, description, name, callerName));

    public Task InvokeAsync(params string[] args) => InvokeAsync(new Queue<string>(args));
    public Task InvokeAsync(IEnumerable<string> args) => InvokeAsync(new Queue<string>(args));

    public async Task InvokeAsync(Queue<string> args)
    {
        Dictionary<string, object> explicitValues = new Dictionary<string, object>();
        List<object> implicitValues = new List<object>();

        foreach (var opt in ExplicitArguments)
        {
            explicitValues[opt.Name] = opt.DefaultValue;
        }

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
                implicitValues.Add(opt.ParseValue(arg, this));
                implicitIdx++;
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
            values[opt.Name] = optArg.Length == 1 ? true : opt.ParseValue(optArg[1], this);
        }
        catch (InvalidCastException ex)
        {
            throw new ParsingException($"Expected option {kebabName} to be of type {opt.DataType}", this, ex);
        }
    }
}
