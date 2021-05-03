using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tailp;

namespace tailpTests
{
    [TestClass]
    public class MatcherTestsRegexEnabled
    {
        [TestInitialize]
        public void Initialize()
        {
            Configs.Regex = true;
        }

        [TestMethod]
        public void MatcherTest01()
        {
            var exp = new Tuple<int, int>(-1, 0);
            var act = Matcher.GetMatchTextIndex(@"aaa", @"bbb");

            Assert.AreEqual(exp.Item1, act.Item1);
        }
        [TestMethod]
        public void MatcherTest02()
        {
            var exp = new Tuple<int, int>(-1, 0);
            var act = Matcher.GetMatchTextIndex(@"", @"bbb");

            Assert.AreEqual(exp.Item1, act.Item1);
        }
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void MatcherTest03()
        {
            Matcher.GetMatchTextIndex(@"aaa", @"");
        }
        [TestMethod]
        public void MatcherTest04()
        {
            var exp = new Tuple<int, int>(0, 3);
            var act = Matcher.GetMatchTextIndex(@"bbb", @"bbb");

            Assert.AreEqual(exp, act);
        }
        [TestMethod]
        public void MatcherTest05()
        {
            var exp = new Tuple<int, int>(0, 2);
            var act = Matcher.GetMatchTextIndex(@"bbb", @"bb");

            Assert.AreEqual(exp, act);
        }
        [TestMethod]
        public void MatcherTest06()
        {
            var exp = new Tuple<int, int>(1, 2);
            var act = Matcher.GetMatchTextIndex(@"zbb", @"bb");

            Assert.AreEqual(exp, act);
        }
        [TestMethod]
        public void MatcherTest07()
        {
            var exp = new Tuple<int, int>(0, 2);
            var act = Matcher.GetMatchTextIndex(@"bbbbbbbb", @"bb");

            Assert.AreEqual(exp, act);
        }
        [TestMethod]
        public void MatcherTest08()
        {
            var exp = new Tuple<int, int>(-1, 0);
            var act = Matcher.GetMatchTextIndex(@"bbbbbbbb", @"BB");

            Assert.AreEqual(exp.Item1, act.Item1);
        }
    }
}
