using System.Collections.Generic;
using System.Linq;

namespace ReflectiveArguments;

class Help
{
    readonly Command command;

    public Help(Command command)
    {
        this.command = command;
    }

    public void ShowHelp()
    {
        foreach (var line in GetHelp())
        {
            command.Settings.LogInfo(line);
        }
    }

    public List<string> GetHelp()
    {
        var ret = new List<string>();
        ret.Add(string.IsNullOrWhiteSpace(command.Description) ? command.FullName : $"{command.FullName} - {command.Description}");
        ret.Add(string.Empty);

        string GetDefaultText(Argument arg)
        {
            var single = "default: " + (arg.DefaultValue is null ? "null" : $"{arg.DefaultValue}");
            var multiple = "accepts many, default: none";
            return arg.AcceptsMany ? multiple : single;
        }

        string GetUsage(Argument arg)
        {
            bool isFlag = arg.DataType == typeof(bool);
            var normalUsage = $"--{arg.KebabName}=<{arg.DataType.Name}>";
            var flagUsage = $"{normalUsage} | --{arg.KebabName}";
            string multiUsage = $"{normalUsage}, ...";
            return arg.AcceptsMany ? multiUsage : (isFlag ? flagUsage : normalUsage);
        }

        var options = command.ExplicitArguments.Select(x => (
            Left: GetUsage(x),
            Right: $"{x.Description} ({GetDefaultText(x)})"));

        var arguments = command.ImplicitArguments.Select(x => (
            Left: x.AcceptsMany 
                ? $"<{x.KebabName}> (one or more {x.DataType.Name}s)" 
                : $"<{x.KebabName}> ({x.DataType.Name})",
            Right: x.Description));

        var padBy = command.SubCommands
            .Select(x => x.Name)
            .Concat(arguments.Select(x => x.Left))
            .Concat(options.Select(x => x.Left));

        int pad = (padBy.Any() ? padBy.Max(x => x.Length) : 0) + 2;

        if (command.IsBound)
        {
            var serialOpts = options.Any()
                ? string.Join(" ", options.Select(x => $"[{x.Left}]")) + " "
                : string.Empty;

            var implicitArguments = command.ImplicitArguments.Any()
                ? string.Join(" ", command.ImplicitArguments
                    .Select(x => x.AcceptsMany ? $"<{x.KebabName}> (...)" : $"<{x.KebabName}>"))
                : string.Empty;

            ret.Add($"usage: {string.Join(" ", command.Path)} {serialOpts}{implicitArguments}");
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

        if (command.SubCommands.Any())
        {
            ret.Add("Commands:");

            foreach (var cmd in command.SubCommands)
            {
                ret.Add($"  {cmd.Name.PadRight(pad)} {cmd.Description}");
            }

            ret.Add(string.Empty);
        }

        return ret;
    }
}
