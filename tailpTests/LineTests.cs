using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tailp;

namespace tailpTests
{
    [TestClass]
    public class LineTests
    {
        Line _fixture;

        [TestInitialize]
        public void Initialize()
        {
            _fixture = new Line
            {
                new Token(Types.None,        "item1"),
                new Token(Types.Highlight,   "item2", 0),
                new Token(Types.None,        "item3"),
                new Token(Types.None,        "item4"),
            };
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void LineTest00SubstringMin()
        {
            _fixture.Substring(int.MinValue);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void LineTest00SubstringMax()
        {
            _fixture.Substring(int.MaxValue);
        }

        [TestMethod]
        public void LineTest01Substring()
        {
            var act = _fixture.Substring(0, 2);
            var exp = new Line
            {
                new Token(Types.None,        "it"),
            };
            CollectionAssert.AreEqual(exp, act);
        }

        [TestMethod]
        public void LineTest02Substring()
        {
            var act = _fixture.Substring(2, 2);
            var exp = new Line
            {
                new Token(Types.None,        "em"),
            };
            CollectionAssert.AreEqual(exp, act);
        }

        [TestMethod]
        public void LineTest03Substring()
        {
            var act = _fixture.Substring(2);
            var exp = new Line
            {
                new Token(Types.None,        "em1"),
                new Token(Types.Highlight,   "item2", 0),
                new Token(Types.None,        "item3"),
                new Token(Types.None,        "item4"),
            };
            CollectionAssert.AreEqual(exp, act);
        }

        [TestMethod]
        public void LineTest04Substring()
        {
            var act = _fixture.Substring(4, 2);
            var exp = new Line
            {
                new Token(Types.None,        "1"),
                new Token(Types.Highlight,   "i", 0),
            };
            CollectionAssert.AreEqual(exp, act);
        }

        [TestMethod]
        public void LineTest05Substring()
        {
            var act = _fixture.Substring(19);
            var exp = new Line
            {
                new Token(Types.None,        "4"),
            };
            CollectionAssert.AreEqual(exp, act);
        }

        [TestMethod]
        public void LineTest06Truncate()
        {
            _fixture.Truncate(2);
            var exp = new Line
            {
                new Token(Types.None, "i"),
                new Token(Types.Truncated, Constants.TRUNCATED_MARKER_END),
            };
            CollectionAssert.AreEqual(exp, _fixture);
        }

        [TestMethod]
        public void LineTest07Truncate()
        {
            _fixture.Truncate(10);
            var exp = new Line
            {
                new Token(Types.None,        "item1"),
                new Token(Types.Highlight,   "item", 0),
                new Token(Types.Truncated,   Constants.TRUNCATED_MARKER_END),
            };
            CollectionAssert.AreEqual(exp, _fixture);
        }

        [TestMethod]
        public void LineTest08Truncate()
        {
            _fixture.Truncate(15);
            var exp = new Line
            {
                new Token(Types.None,        "item1"),
                new Token(Types.Highlight,   "item2", 0),
                new Token(Types.None,        "item"),
                new Token(Types.Truncated,   Constants.TRUNCATED_MARKER_END),
            };
            CollectionAssert.AreEqual(exp, _fixture);
        }

        [TestMethod]
        public void LineTest09Truncate()
        {
            _fixture.Truncate(200000);
            var exp = new Line
            {
                new Token(Types.None,        "item1"),
                new Token(Types.Highlight,   "item2", 0),
                new Token(Types.None,        "item3"),
                new Token(Types.None,        "item4"),
            };
            CollectionAssert.AreEqual(exp, _fixture);
        }

        [TestMethod]
        public void LineTest10Truncate()
        {
            _fixture = new Line
            {
                new Token(Types.Highlight,   "item1long", 0),
                new Token(Types.None,        "item2long"),
                new Token(Types.Highlight,   "item3long", 1),
                new Token(Types.None,        "item4long"),
                new Token(Types.Highlight,   "item5long", 2),
            };
            _fixture.Truncate(40);
            var exp = new Line
            {
                new Token(Types.Highlight,   "item1long", 0),
                new Token(Types.Truncated,   Constants.TRUNCATED_MARKER_MIDDLE),
                new Token(Types.Highlight,   "item3long", 1),
                new Token(Types.None,        "i"),
                new Token(Types.Truncated,   Constants.TRUNCATED_MARKER_MIDDLE),
                new Token(Types.None,        "ng"),
                new Token(Types.Highlight,   "item5long", 2),
            };
            CollectionAssert.AreEqual(exp, _fixture);
        }
    }
}
