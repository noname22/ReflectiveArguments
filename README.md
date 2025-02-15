# ReflectiveArguments

ReflectiveArguments is a .NET 6+ library designed to simplify command line argument parsing and execution using reflection. This library allows developers to define commands and their associated parameters as methods within classes, and automatically handles the parsing and invocation of these commands based on the provided command line arguments.

It is based around the idea that methods can called directly from the command line.

## Features

* **Automatic Command Parsing**: Define commands as methods and let ReflectiveArguments handle the parsing of command line arguments.
* **Reflection-Based Execution**: Utilize reflection to dynamically invoke methods based on parsed arguments.
* **Flexible Parameter Handling**: Support for various parameter types, including optional and required parameters.
* **Automatic Help Generation**: Generate help text based on defined commands and parameters.
  - **Customizable Help Text**: Use attributes to customize the help text for commands and parameters.

## Structure
ReflectiveArguments is based on *commands* and *parameters*. Commands are defined as methods within classes, and parameters are defined as method parameters. The library uses reflection to parse command line arguments and invoke the appropriate method based on the provided arguments.

Much like *git** or *dotnet* CLI, ReflectiveArguments uses a hierarchical structure to define commands. The root command is the entry point for the application, and subcommands can be added to that. Each subcommand can have its own parameters, which are defined as method parameters.

### Commands
Commands are defined as methods within classes. Each command method must have a return type of `void` or `Task` and can have zero or more parameters. By default, the method name is converted to `kebab-case` used as the command name.

### Parameters
Parameters are defined as method parameters. Parameters can be options or arguments, and can be of various types, including `string`, `int`, `bool`, and arrays of these types.

#### Options
Options are defined as method parameters with default values. If a parameter is not provided in the command line arguments, the default value is used.

If a parameter is of type `bool`, it is considered a flag. Flags do not require a value to be provided in the command line arguments. If the flag is present, the parameter is set to `true`; otherwise, it is set to its provided default value.

Note: this means that if a flag parameter has a default value of `true`, the user must provide `--flag=false` to set the value to `false`.

If an optional parameter is an array (eg. `string[] myOption = null`), it is considered an serial option. Serial options can accept multiple values and are defined as `--my-option=value1 --my-option=value2 ...`. If no option parameters are provided by the user, the parameter is left at its default value of `null`.

#### Arguments
Arguments are required parameters and defined as method parameters without default values. If an argument is not provided in the command line arguments, an error is displayed.

If multiple arguments are defined, they are expected to be provided in the order they are defined in the method signature.

The last argument of a command method can be an array parameter. This allows the method to accept an arbitrary number of arguments of the specified type.

## Usage

See the provided examples in this repository for further usage information.

### Simple
A simple example demonstrating the basic usage of a program with a single command, suitable for a simple CLI tool.

It accepts a string `info`, an array of strings `additionalInfo`, a boolean flag `flag`, an integer `width`, an integer `height`, and an array of strings `option`.

```csharp
using ReflectiveArguments;
using System;

return Command.FromMethod(Example, "Simple example of how to use Reflective Arguments")
    .HandleCommandLine(args);

static void Example(string info, string[] additionalInfo, bool flag = false, int width = 320, int height = 240, string[] option = null)
{
    // your code here
}
```

Help text generated for the above example:
```
example - Simple example of how to use Reflective Arguments

usage: example [--flag=<Boolean>, --flag] [--height=<Int32>] [--option=<String>, ...] [--width=<Int32>] [--help] <info> <additional-info> (...)

Arguments:
  <info> (String)
  <additional-info> (one or more Strings)

Options:
  --flag=<Boolean>, --flag                   (default: False)
  --height=<Int32>                           (default: 240)
  --option=<String>, ...                     (accepts many, default: none)
  --width=<Int32>                            (default: 320)
  --help                                     Show this help text
```

### Advanced

An advanced example demonstrating the usage of subcommands and custom help text. This example is based on a simplified version of the `git` CLI tool.

It also uses asynchronous methods.

```csharp

using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace ReflectiveArguments.Examples.GitLike;

class Program
{
    static async Task<int> Main(string[] args)
    {
        return await new Command("git", "A free and open source distributed version control system.")

            .AddCommand(Clone, "Clone a repository into a new directory")
            .AddCommand(Branch, "List, create, or delete branches")
            .AddCommand(Push, "Update remote refs along with associated objects")
            .AddCommand(Pull, "Fetch from and integrate with another repository or a local branch")
            .AddCommand(Add, "Add file contents to the index")

            .AddCommand(new Command("remote", "Manage set of tracked repositories")
                .AddCommand(RemoteAdd, "Add a remote named <name> for the repository at <url>.", "add")
                .AddCommand(RemoteRemove, "Remove the remote named <name>. ", "remove"))

            .HandleCommandLineAsync(args);
    }

    static async Task Clone(string repo, [Description("Checkout recursively")] bool recursive = false)
    {
        Console.WriteLine($"Clone: {repo} (recursive: {recursive})");
        await Task.Delay(10);
    }

    static void Branch(string branch, bool delete = false) => Console.WriteLine($"Branch: {branch} (delete: {delete})");
    static async Task<int> Push()
    {
        Console.WriteLine("Push");
        await Task.Delay(10);
        return 1;
    }

    static void Pull() => Console.WriteLine("Pull");
    static void RemoteAdd(string name, string url) => Console.WriteLine($"Remote Add name: {name}, url: {url}");
    static void RemoteRemove(string name) => Console.WriteLine($"Remote Remove name: {name}");
    static void Add(string[] path, bool force = false) => Console.WriteLine($"Add files: {string.Join(" ", path)} (force: {force})");
}
```

Help text generated for the above example:
```
git - A free and open source distributed version control system.

usage: git [--help] <command>

Options:
  --help    Show this help text

Commands:
  clone    Clone a repository into a new directory
  branch   List, create, or delete branches
  push     Update remote refs along with associated objects
  pull     Fetch from and integrate with another repository or a local branch
  add      Add file contents to the index
  remote   Manage set of tracked repositories

Run 'git [command] --help' for more information on a command.
```

Additionally, help text can be generated for each subcommand by running `git [command] --help`. Here is the help text for the `git clone` command:
```
git clone - Clone a repository into a new directory

usage: git clone [--recursive=<Boolean>, --recursive] [--help] <repo>

Arguments:
  <repo> (String)

Options:
  --recursive=<Boolean>, --recursive   Checkout recursively (default: False)
  --help                                Show this help text
```

### Settings
The ReflectiveArgumentsSettings class can be used to customize the behavior of the library. The following settings are available:

```csharp
public class ReflectiveArgumentSettings
{
    public Action<string> LogInfo { get; set; } = Console.WriteLine;
    public Action<string> LogError { get; set; } = Console.Error.WriteLine;
    public bool AutoHelp { get; set; } = true;
}
```

LogInfo and LogError are used to log information and errors, respectively. By default, they write to the console.

AutoHelp determines whether help text is automatically generated when the `--help` option is provided. If set to false, the user must manually handle the `--help` option in their command methods.

The settings can be passed to the `Command` constructor or the static `Command.FromMethod()` method.

## License
See the [LICENSE.txt](LICENSE.txt) file for license rights and limitations (MIT).
