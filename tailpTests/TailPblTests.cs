using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using tailp;

// ReSharper disable ConvertToConstant.Local
// ReSharper disable ConditionIsAlwaysTrueOrFalse

namespace tailpTests
{
    [TestClass()]
    public class TailPblTests
    {
        private TailPbl _fixture;

        [TestInitialize]
        public void Initialize()
        {
            _fixture = new TailPbl((l, i) => { });
        }

        [TestMethod()]
        public void ParseArgsTest01()
        {
            try
            {
                _fixture.ParseArgs(new string[0]);
                Assert.Fail("An exception should have been thrown");
            }
            catch (TailPException ex)
            {
                Assert.AreEqual("Invalid args", ex.Message);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Unexpected exception of type {ex.GetType()} caught: {ex.Message}");
            }
        }

        [TestMethod()]
        public void ParseArgsTest02()
        {
            _fixture.ParseArgs(new[] { @"-f", @"dummy_file" });
            var act = Configs.Follow;
            var exp = true;
            Assert.AreEqual(exp, act);
        }

        [TestMethod()]
        public void ParseArgsTest03()
        {
            _fixture.ParseArgs(new[] { @"--logical-lines", @"<<<", @"dummy_file" });
            var act = Configs.LogicalLineMarker;
            var exp = @"<<<";
            Assert.AreEqual(exp, act);
        }

        [TestMethod()]
        public void GetHelpTest()
        {
            var act = TailPbl.GetHelp();
            Assert.AreNotEqual(string.Empty, act);
        }
    }
}