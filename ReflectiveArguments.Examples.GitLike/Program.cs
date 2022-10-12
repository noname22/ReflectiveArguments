using System;
using System.ComponentModel;

namespace ReflectiveArguments.Examples.GitLike;

class Program
{
    static int Main(string[] args)
    {
        return new Command("git", "A free and open source distributed version control system.")

            .AddCommand(Clone, "Clone a repository into a new directory")
            .AddCommand(Branch, "List, create, or delete branches")
            .AddCommand(Push, "Update remote refs along with associated objects")
            .AddCommand(Pull, "Fetch from and integrate with another repository or a local branch")

            .AddCommand(new Command("remote", "Manage set of tracked repositories")
                .AddCommand(RemoteAdd, "Add a remote named <name> for the repository at <url>.", "add")
                .AddCommand(RemoteRemove, "Remove the remote named <name>. ", "remove"))

            .HandleCommandLine(args);
    }

    static void Clone(string repo, [Description("Checkout recursively")] bool recursive = false) => Console.WriteLine($"Clone: {repo} (recursive: {recursive})");
    static void Branch(string branch, bool delete = false) => Console.WriteLine($"Branch: {branch} (delete: {delete})");
    static void Push() => Console.WriteLine("Push");
    static void Pull() => Console.WriteLine("Pull");
    static void RemoteAdd(string name, string url) => Console.WriteLine($"Remote Add name: {name}, url: {url}");
    static void RemoteRemove(string name) => Console.WriteLine($"Remote Remove name: {name}");
}
