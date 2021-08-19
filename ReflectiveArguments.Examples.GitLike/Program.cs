using System;

namespace ReflectiveArguments.Examples.GitLike
{
    class Program
    {
        static int Main(string[] args)
        {
            return new Command("git", "A free and open source distributed version control system.")
                
                .AddCommand<Program>(nameof(Clone), "Clone a repository into a new directory")
                .AddCommand<Program>(nameof(Branch), "List, create, or delete branches")
                .AddCommand<Program>(nameof(Push), "Update remote refs along with associated objects")
                .AddCommand<Program>(nameof(Pull), "Fetch from and integrate with another repository or a local branch")
                
                .AddCommand(Command.FromMethod<Program>(nameof(Remote), "Manage set of tracked repositories")
                    .AddCommand<Program>(nameof(RemoteAdd), "Add a remote named <name> for the repository at <url>.", "add")
                    .AddCommand<Program>(nameof(RemoteRemove), "Remove the remote named <name>. ", "remove"))
                
                .HandleCommandLine(args);
        }

        static void Clone(string repo, bool recursive = false) => Console.WriteLine($"Clone: {repo} (recursive: {recursive})");
        static void Branch(string branch, bool delete = false) => Console.WriteLine($"Branch: {branch} (delete: {delete})");
        static void Push() => Console.WriteLine("Push");
        static void Pull() => Console.WriteLine("Pull");
        static void Remote() => Console.WriteLine("Remote");
        static void RemoteAdd(string name, string url) => Console.WriteLine($"Remote Add name: {name}, url: {url}");
        static void RemoteRemove(string name) => Console.WriteLine($"Remote Remove name: {name}");
    }
}
