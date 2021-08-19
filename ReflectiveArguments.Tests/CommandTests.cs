using FluentAssertions;
using Xunit;

namespace ReflectiveArguments.Tests
{
    public class CommandTests
    {
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
        public void ClassTest()
        {
            var myClass = new MyClass();
            var cmd = new Command("test", "tests");
            cmd.Bind(myClass, nameof(MyClass.Run));
            
            cmd.Invoke("3", "true", "--ao=OA", "--string-arg=myString");

            myClass.Ran.Should().BeTrue();
            myClass.IntArg.Should().Be(3);
            myClass.BoolArg.Should().Be(true);
            myClass.StringArg.Should().Be("myString");
        }

        [Fact]
        public void StaticMethodTest()
        {
            var cmd = new Command("test", "tests");
            cmd.Bind<CommandTests>(nameof(MyMethod));
            cmd.Invoke("hello");

            staticTestData.Should().Be("hello");
        }

        static string staticTestData = null;

        static void MyMethod(string test)
        {
            staticTestData = test;
        }

        [Fact]
        public void SubCommandTest()
        {
            var myClass = new MyClass();
            var root = new Command("test", "tests");
            
            var cmd = new Command("my-class", "tests");
            cmd.Bind(myClass, nameof(MyClass.Run));
            
            root.AddCommand(cmd);

            root.Invoke("my-class", "3", "true", "--ao=OA", "--string-arg=myString");

            myClass.Ran.Should().BeTrue();
            myClass.IntArg.Should().Be(3);
            myClass.BoolArg.Should().Be(true);
            myClass.StringArg.Should().Be("myString");
        }

        [Fact]
        public void TooFewArguments()
        {
            var cmd = new Command("test", "tests");
            cmd.Bind<CommandTests>(nameof(MyMethod));
            Assert.Throws<ParsingException>(() => cmd.Invoke());
        }

        [Fact]
        public void TooManyArguments()
        {
            var cmd = new Command("test", "tests");
            cmd.Bind<CommandTests>(nameof(MyMethod));
            Assert.Throws<ParsingException>(() => cmd.Invoke("a", "b", "c"));
        }
    }
}
