using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using tailp;

// ReSharper disable ConditionIsAlwaysTrueOrFalse
// ReSharper disable ConvertToConstant.Local

namespace tailpTests
{
    [TestClass]
    public class ArchiveSupportTests
    {
//        ArchiveSupport _fixture;

        [TestInitialize]
        public void Initialize()
        {
        }

        [TestMethod]
        public void ArchiveSupportTest00TryGetArchivePath()
        {
            var path = $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}TestFiles{Path.DirectorySeparatorChar}1.zip";

            var expResult = true;
            var expArchive = $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}TestFiles{Path.DirectorySeparatorChar}1.zip";
            var expFile = string.Empty;
            var actResult = ArchiveSupport.TryGetArchivePath(path, out var actArchive, out var actFile);

            Assert.AreEqual(expResult, actResult);
            Assert.AreEqual(expArchive, actArchive);
            Assert.AreEqual(expFile, actFile);
        }

        [TestMethod]
        public void ArchiveSupportTest01TryGetArchivePath()
        {
            var path = $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}TestFiles{Path.DirectorySeparatorChar}1.zip{Path.DirectorySeparatorChar}";

            var expResult = true;
            var expArchive = $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}TestFiles{Path.DirectorySeparatorChar}1.zip";
            var expFile = string.Empty;
            var actResult = ArchiveSupport.TryGetArchivePath(path, out var actArchive, out var actFile);

            Assert.AreEqual(expResult, actResult);
            Assert.AreEqual(expArchive, actArchive);
            Assert.AreEqual(expFile, actFile);
        }

        [TestMethod]
        public void ArchiveSupportTest02TryGetArchivePath()
        {
            var path = $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}TestFiles{Path.DirectorySeparatorChar}1.zip{Path.DirectorySeparatorChar}1{Path.DirectorySeparatorChar}3{Path.DirectorySeparatorChar}Annotations.cs";

            var expResult = true;
            var expArchive = $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}TestFiles{Path.DirectorySeparatorChar}1.zip";
            var expFile = $"1{Path.DirectorySeparatorChar}3{Path.DirectorySeparatorChar}Annotations.cs";
            var actResult = ArchiveSupport.TryGetArchivePath(path, out var actArchive, out var actFile);

            Assert.AreEqual(expResult, actResult);
            Assert.AreEqual(expArchive, actArchive);
            Assert.AreEqual(expFile, actFile);
        }

        [TestMethod]
        public void ArchiveSupportTest03TryGetArchivePath()
        {
            var path = $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}TestFiles{Path.DirectorySeparatorChar}1.txt{Path.DirectorySeparatorChar}1{Path.DirectorySeparatorChar}3{Path.DirectorySeparatorChar}Annotations.cs";

            var expResult = false;
            var actResult = ArchiveSupport.TryGetArchivePath(path, out _, out _);

            Assert.AreEqual(expResult, actResult);
        }

        [TestMethod]
        public void ArchiveSupportTest04TryGetArchivePath()
        {
            var path = $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}TestFiles{Path.DirectorySeparatorChar}1.zip{Path.DirectorySeparatorChar}*.cs";

            var expResult = true;
            var expArchive = $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}TestFiles{Path.DirectorySeparatorChar}1.zip";
            var expFile = $"*.cs";
            var actResult = ArchiveSupport.TryGetArchivePath(path, out var actArchive, out var actFile);

            Assert.AreEqual(expResult, actResult);
            Assert.AreEqual(expArchive, actArchive);
            Assert.AreEqual(expFile, actFile);
        }

        [TestMethod]
        public void ArchiveSupportTest05IsValidArchiveZip()
        {
            var path = $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}TestFiles{Path.DirectorySeparatorChar}1.zip";

            var expResult = true;
            var actResult = ArchiveSupport.IsValidArchive(path);

            Assert.AreEqual(expResult, actResult);
        }

        [TestMethod]
        public void ArchiveSupportTest05IsValidArchiveRar()
        {
            var path = $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}TestFiles{Path.DirectorySeparatorChar}Properties.rar";

            var expResult = true;
            var actResult = ArchiveSupport.IsValidArchive(path);

            Assert.AreEqual(expResult, actResult);
        }

        [TestMethod]
        public void ArchiveSupportTest06IsValidArchive()
        {
            var path = $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}TestFiles{Path.DirectorySeparatorChar}invalid.zip";

            var expResult = false;
            var actResult = ArchiveSupport.IsValidArchive(path);

            Assert.AreEqual(expResult, actResult);
        }

        [TestMethod]
        public void ArchiveSupportTest07IsValidArchive()
        {
            var path = $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}TestFiles{Path.DirectorySeparatorChar}dummytext.txt";

            var expResult = false;
            var actResult = ArchiveSupport.IsValidArchive(path);

            Assert.AreEqual(expResult, actResult);
        }

        [TestMethod]
        [ExpectedException(typeof(TailPArchiveException))]
        public void ArchiveSupportTest08EnumerateFiles()
        {
            var path = $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}TestFiles{Path.DirectorySeparatorChar}dummytext.txt";

            ArchiveSupport.EnumerateFiles(path);
        }

        [TestMethod]
        public void ArchiveSupportTest09EnumerateFiles()
        {
            var path = $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}TestFiles{Path.DirectorySeparatorChar}1.zip{Path.DirectorySeparatorChar}1{Path.DirectorySeparatorChar}3{Path.DirectorySeparatorChar}Annotations.cs";

            var exp = new List<string>()
            {
                @"1/3/Annotations.cs"
            };
            var act = new List<string>(ArchiveSupport.EnumerateFiles(path));
            CollectionAssert.AreEquivalent(exp, act);
        }

        [TestMethod]
        public void ArchiveSupportTest10EnumerateFiles()
        {
            var path = $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}TestFiles{Path.DirectorySeparatorChar}1.zip{Path.DirectorySeparatorChar}*.cs";

            var exp = new List<string>()
            {
                @"Annotations_1.cs",
                @"1/Annotations_.cs",
                @"1/2/Annotations - Copy.cs",
                @"1/2/Annotations.cs",
                @"1/3/Annotations.cs"
            };
            var act = new List<string>(ArchiveSupport.EnumerateFiles(path));
            CollectionAssert.AreEquivalent(exp, act);
        }

        [TestMethod]
        public void ArchiveSupportTest11EnumerateFiles()
        {
            var path = $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}TestFiles{Path.DirectorySeparatorChar}1.zip{Path.DirectorySeparatorChar}1{Path.DirectorySeparatorChar}?{Path.DirectorySeparatorChar}Annotations - Copy.cs";

            var exp = new List<string>()
            {
                @"1/2/Annotations - Copy.cs"
            };
            var act = new List<string>(ArchiveSupport.EnumerateFiles(path));
            CollectionAssert.AreEquivalent(exp, act);
        }

        [TestMethod]
        public void ArchiveSupportTest12EnumerateFiles()
        {
            var path = $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}TestFiles{Path.DirectorySeparatorChar}1.zip";

            var exp = new List<string>()
            {
                @"Annotations_1.cs",
                @"1/Annotations_.cs",
                @"1/2/Annotations - Copy.cs",
                @"1/2/Annotations.cs",
                @"1/3/Annotations.cs"
            };
            var act = new List<string>(ArchiveSupport.EnumerateFiles(path));
            CollectionAssert.AreEquivalent(exp, act);
        }
    }
}
