using Microsoft.VisualStudio.TestTools.UnitTesting;
using TailP;
using System;
// ReSharper disable ConvertToConstant.Local

namespace tailpTests
{
    [TestClass()]
    public class TimespanExtensionsTests
    {
        [TestMethod()]
        public void ToHumanReadableStringTest01()
        {
            var act = TimeSpan.Zero.ToHumanReadableString();
            var exp = @"0 second(s)";
            Assert.AreEqual(exp, act);
        }

        [TestMethod()]
        public void ToHumanReadableStringTest02()
        {
            var act = TimeSpan.FromSeconds(5).ToHumanReadableString();
            var exp = @"5 second(s)";
            Assert.AreEqual(exp, act);
        }

        [TestMethod()]
        public void ToHumanReadableStringTest03()
        {
            var act = TimeSpan.FromSeconds(2 * 60 + 3).ToHumanReadableString();
            var exp = @"2:03 minute(s)";
            Assert.AreEqual(exp, act);
        }

        [TestMethod()]
        public void ToHumanReadableStringTest04()
        {
            var act = TimeSpan.FromSeconds(4 * 3600 + 2 * 60 + 3).ToHumanReadableString();
            var exp = @"4:02 hour(s)";
            Assert.AreEqual(exp, act);
        }

        [TestMethod()]
        public void ToHumanReadableStringTest05()
        {
            var act = TimeSpan.FromSeconds(24* 3600 + 4 * 3600 + 2 * 60 + 3).ToHumanReadableString();
            var exp = @"over 28 hour(s)";
            Assert.AreEqual(exp, act);
        }

        [TestMethod()]
        public void ToHumanReadableStringTest06()
        {
            var act = TimeSpan.FromSeconds(48 * 3600 + 4 * 3600 + 2 * 60 + 3).ToHumanReadableString();
            var exp = @"over 2 day(s)";
            Assert.AreEqual(exp, act);
        }
    }
}