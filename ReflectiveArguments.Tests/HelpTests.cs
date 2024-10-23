using FluentAssertions;
using ReflectiveArguments.Tests.TestTypes;
using System;
using Xunit;

namespace ReflectiveArguments.Tests;
public class HelpTests
{
    [Fact]
    public void GetHelp_WithMethodContainingDescriptionAttribute_ShouldUseDescriptionInHelp()
    {
        var cmd = Command.FromMethod(OtherStaticClass.StaticMethodWithArgumentDescriptions);
        var help = new Help(cmd);
        var helpText = string.Join(Environment.NewLine, help.GetHelp());
        helpText.Should().Contain("This is an argument");
    }
}
