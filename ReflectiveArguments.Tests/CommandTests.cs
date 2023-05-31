using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ReflectiveArguments.Tests
{
    public class CommandTests
    {
        enum MyEnum { A, B, C, D }

        class MyClass 
        {
            public bool Ran { get; private set; }
            
            public int IntArg { get; private set; }
            public bool BoolArg { get; private set; }
            public string StringArg  { get; private set; }

            public void Run(int intArg, bool boolArg, string stringArg = "myString", string AO="ao")
            {
                Ran = true;
                IntArg = intArg;
                BoolArg = boolArg;
                StringArg = stringArg;
            }
        }

        [Fact]
        public async Task Invoke_WithClassMethod_ShouldExecuteSuccessfully()
        {
            var myClass = new MyClass();
            var cmd = new Command("test", "tests");
            cmd.Bind(myClass.Run);
            
            await cmd.InvokeAsync("3", "true", "--ao=OA", "--string-arg=myString");

            myClass.Ran.Should().BeTrue();
            myClass.IntArg.Should().Be(3);
            myClass.BoolArg.Should().Be(true);
            myClass.StringArg.Should().Be("myString");
        }

        [Fact]
        public void Invoke_WithStaticMethod_ShouldExecuteSuccessfully()
        {
            var cmd = new Command("test", "tests");
            cmd.Bind(MyMethod);
            cmd.InvokeAsync("hello");

            staticTestData.Should().Be("hello");
        }

        static string staticTestData = null;

        static void MyMethod(string test)
        {
            staticTestData = test;
        }

        [Fact]
        public async Task InvokeOnSubCommand_WithClassMethod_ShouldExecuteSuccessfully()
        {
            var myClass = new MyClass();
            var root = new Command("test", "tests");
            
            var cmd = new Command("my-class", "tests");
            cmd.Bind(myClass.Run);
            
            root.AddCommand(cmd);

            await root.InvokeAsync("my-class", "3", "true", "--ao=OA", "--string-arg=myString");

            myClass.Ran.Should().BeTrue();
            myClass.IntArg.Should().Be(3);
            myClass.BoolArg.Should().Be(true);
            myClass.StringArg.Should().Be("myString");
        }

        [Fact]
        public void Invoke_WithTooFewArguments_ShouldThrow()
        {
            var cmd = new Command("test", "tests");
            cmd.Bind(MyMethod);
            Assert.ThrowsAsync<ParsingException>(async () => await cmd.InvokeAsync());
        }

        [Fact]
        public void Invoke_WithTooManyArguments_ShouldThrow()
        {
            var cmd = new Command("test", "tests");
            cmd.Bind(MyMethod);
            Assert.ThrowsAsync<ParsingException>(async () => await cmd.InvokeAsync("a", "b", "c"));
        }

        [Fact]
        public void Invoke_WithRepeatArgumentsWhenNotAllowed_ShouldThrow()
        {
            void Method(int parameter = 0) { }
            Assert.ThrowsAsync<ParsingException>(async () => await Command.FromMethod(Method).InvokeAsync("--parameter=3", "--parameter=4"));
        }

        [Fact]
        public async Task Invoke_WithRepeatArgumentsWhenAllowed_ShouldExecuteSuccessfully()
        {
            int[] got = null;
            void Method(int[] parameter = null ) { got = parameter; }
            await Command.FromMethod(Method).InvokeAsync("--parameter=3", "--parameter=4");
            got.Should().BeEquivalentTo(new int[] { 3, 4 });
        }


        [Fact]
        public async Task Invoke_WithRepeatArgumentsEnums_ShouldExecuteSuccessfully()
        {
            MyEnum[] got = new MyEnum[] { };
            void Method(MyEnum[] myEnum = null) { got = myEnum; }
            await Command.FromMethod(Method).InvokeAsync("--my-enum=A", "--my-enum=B", "--my-enum=C");
            got.Should().BeEquivalentTo(new[] { MyEnum.A, MyEnum.B, MyEnum.C });
        }

        [Fact]
        public async Task Invoke_WithRepeatArgumentsAndDefaultValue_ShouldExecuteSuccessfully()
        {
            int[] got = new int[] { };
            void Method(int[] parameter = null) { got = parameter; }
            await Command.FromMethod(Method).InvokeAsync();
            got.Should().BeNull();
        }

        [Fact]
        public async Task Invoke_WithRepeatImplicitArguments_ShouldExecuteSuccessfully()
        {
            int[] got = new int[] { };
            void Method(int[] parameters) { got = parameters; }
            await Command.FromMethod(Method).InvokeAsync("1", "2", "3");
            got.Should().BeEquivalentTo(new[] { 1, 2, 3 });
        }

        [Fact]
        public async Task Invoke_WithNormmalAndRepeatImplicitArguments_ShouldExecuteSuccessfully()
        {
            int[] got = new int[] { };
            int gotA = 0, gotB = 0;
            void Method(int a, int b, int[] parameters) { got = parameters; gotA = a; gotB = b; }
            await Command.FromMethod(Method).InvokeAsync("3", "4", "1", "2", "3");
            got.Should().BeEquivalentTo(new[] { 1, 2, 3 });
            gotA.Should().Be(3);
            gotB.Should().Be(4);
        }

        [Fact]
        public void FromMethod_WithRepeatArgumentsNotLast_ShouldThrow()
        {
            void Method(int[] parameters, int otherParameter) { }
            var ex = Assert.Throws<ArgumentException>(() => Command.FromMethod(Method));
            ex.Message.Should().Be("Only the last explicit argument may accept many values");
        }

        [Fact]
        public void FromMethod_WithMultipleRepeatArguments_ShouldThrow()
        {
            void Method(int[] parameters, int[] otherParameter) { }
            var ex = Assert.Throws<ArgumentException>(() => Command.FromMethod(Method));
            ex.Message.Should().Be("Only the last explicit argument may accept many values");
        }

        [Fact]
        public async Task Invoke_WithUnsupportedArgumentType_ShouldThrow()
        {
            void Method(List<int> parameters) {}
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await Command.FromMethod(Method).InvokeAsync("1", "2", "3"));
            ex.Message.Should().Contain("Unsupported argument type:");
        }
    }
}
