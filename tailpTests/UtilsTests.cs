using Microsoft.VisualStudio.TestTools.UnitTesting;
using tailp;

// ReSharper disable ConditionIsAlwaysTrueOrFalse
// ReSharper disable ConvertToConstant.Local

namespace tailpTests
{
    [TestClass]
    public class UtilsTests
    {
        [TestMethod]
        public void IsMatchMaskTest01()
        {
            var path = @"c:\1.txt";
            var mask = @"1.txt";

            var exp = true;
            var act = Utils.IsMatchMask(path, mask);

            Assert.AreEqual(exp, act);
        }

        [TestMethod]
        public void IsMatchMaskTest02()
        {
            var path = @"c:\1.txt";
            var mask = @"2.txt";

            var exp = false;
            var act = Utils.IsMatchMask(path, mask);

            Assert.AreEqual(exp, act);
        }

        [TestMethod]
        public void IsMatchMaskTest03()
        {
            var path = @"c:\1.txt";
            var mask = @"1.*";

            var exp = true;
            var act = Utils.IsMatchMask(path, mask);

            Assert.AreEqual(exp, act);
        }

        [TestMethod]
        public void IsMatchMaskTest04()
        {
            var path = @"C:\1.TXT";
            var mask = @"1.txt";

            var exp = true;
            var act = Utils.IsMatchMask(path, mask);

            Assert.AreEqual(exp, act);
        }

        [TestMethod]
        public void IsMatchMaskTest05()
        {
            var path = @"c:\1.TXT";
            var mask = @"*.txt";

            var exp = true;
            var act = Utils.IsMatchMask(path, mask);

            Assert.AreEqual(exp, act);
        }

        [TestMethod]
        public void IsMatchMaskTest06()
        {
            var path = @"c:\1.txt1";
            var mask = @"1.txt";

            var exp = false;
            var act = Utils.IsMatchMask(path, mask);

            Assert.AreEqual(exp, act);
        }

        [TestMethod]
        public void IsMatchMaskTest07()
        {
            var path = @"c:\1.txt";
            var mask = @"1.t?t";

            var exp = true;
            var act = Utils.IsMatchMask(path, mask);

            Assert.AreEqual(exp, act);
        }

        [TestMethod]
        public void IsMatchMaskTest08()
        {
            var path = @"c:\1.txt1";
            var mask = @"1.t?t";

            var exp = false;
            var act = Utils.IsMatchMask(path, mask);

            Assert.AreEqual(exp, act);
        }
    }
}
