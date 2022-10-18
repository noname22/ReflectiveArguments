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
}
