//using FluentAssertions;
//using Xunit;

//namespace ReflectiveArguments.Tests
//{
//    public class CommandCollectionTests
//    {
//        [Fact]
//        public void RunCommandFromCommandCollection()
//        {
//            var collection = new CommandCollection("test", "test");

//            collection.Add(Command.FromClass(this, methodName: nameof(TestCommand)));
//            collection.Add(Command.FromClass(this, methodName: nameof(TestCommand2)));
            
//            collection.Run("test-command", "test");
//            collection.Run("test-command2", "1");

//            str.Should().Be("test");
//            intVal.Should().Be(1);
//        }

//        string str = null;
//        int intVal = 0;

//        void TestCommand(string str)
//        {
//            this.str = str;
//        }

//        void TestCommand2(int arg)
//        {
//            intVal = arg;
//        }
//    }
//}
