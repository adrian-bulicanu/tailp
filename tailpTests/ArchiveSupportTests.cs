using Microsoft.VisualStudio.TestTools.UnitTesting;
using TailP;
using System.Collections.Generic;

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
            string path = @"..\..\TestFiles\1.zip";

            var expResult = true;
            var expArchive = @"..\..\TestFiles\1.zip";
            var expFile = string.Empty;

            var actArchive = string.Empty;
            var actFile = string.Empty;
            var actResult = ArchiveSupport.TryGetArchivePath(path, out actArchive, out actFile);

            Assert.AreEqual(expResult, actResult);
            Assert.AreEqual(expArchive, actArchive);
            Assert.AreEqual(expFile, actFile);
        }

        [TestMethod]
        public void ArchiveSupportTest01TryGetArchivePath()
        {
            string path = @"..\..\TestFiles\1.zip\";

            var expResult = true;
            var expArchive = @"..\..\TestFiles\1.zip";
            var expFile = string.Empty;

            var actArchive = string.Empty;
            var actFile = string.Empty;
            var actResult = ArchiveSupport.TryGetArchivePath(path, out actArchive, out actFile);

            Assert.AreEqual(expResult, actResult);
            Assert.AreEqual(expArchive, actArchive);
            Assert.AreEqual(expFile, actFile);
        }

        [TestMethod]
        public void ArchiveSupportTest02TryGetArchivePath()
        {
            string path = @"..\..\TestFiles\1.zip\1\3\Annotations.cs";

            var expResult = true;
            var expArchive = @"..\..\TestFiles\1.zip";
            var expFile = @"1\3\Annotations.cs";

            var actArchive = string.Empty;
            var actFile = string.Empty;
            var actResult = ArchiveSupport.TryGetArchivePath(path, out actArchive, out actFile);

            Assert.AreEqual(expResult, actResult);
            Assert.AreEqual(expArchive, actArchive);
            Assert.AreEqual(expFile, actFile);
        }

        [TestMethod]
        public void ArchiveSupportTest03TryGetArchivePath()
        {
            string path = @"..\..\TestFiles\1.txt\1\3\Annotations.cs";

            var expResult = false;

            var actArchive = string.Empty;
            var actFile = string.Empty;
            var actResult = ArchiveSupport.TryGetArchivePath(path, out actArchive, out actFile);

            Assert.AreEqual(expResult, actResult);
        }

        [TestMethod]
        public void ArchiveSupportTest04TryGetArchivePath()
        {
            string path = @"..\..\TestFiles\1.zip\*.cs";

            var expResult = true;
            var expArchive = @"..\..\TestFiles\1.zip";
            var expFile = @"*.cs";

            var actArchive = string.Empty;
            var actFile = string.Empty;
            var actResult = ArchiveSupport.TryGetArchivePath(path, out actArchive, out actFile);

            Assert.AreEqual(expResult, actResult);
            Assert.AreEqual(expArchive, actArchive);
            Assert.AreEqual(expFile, actFile);
        }

        [TestMethod]
        public void ArchiveSupportTest05IsValidArchiveZip()
        {
            string path = @"..\..\TestFiles\1.zip";

            var expResult = true;
            var actResult = ArchiveSupport.IsValidArchive(path);

            Assert.AreEqual(expResult, actResult);
        }

        [TestMethod]
        public void ArchiveSupportTest05IsValidArchiveRar()
        {
            string path = @"..\..\TestFiles\Properties.rar";

            var expResult = true;
            var actResult = ArchiveSupport.IsValidArchive(path);

            Assert.AreEqual(expResult, actResult);
        }

        [TestMethod]
        public void ArchiveSupportTest06IsValidArchive()
        {
            string path = @"..\..\TestFiles\invalid.zip";

            var expResult = false;
            var actResult = ArchiveSupport.IsValidArchive(path);

            Assert.AreEqual(expResult, actResult);
        }

        [TestMethod]
        public void ArchiveSupportTest07IsValidArchive()
        {
            string path = @"..\..\TestFiles\dummytext.txt";

            var expResult = false;
            var actResult = ArchiveSupport.IsValidArchive(path);

            Assert.AreEqual(expResult, actResult);
        }

        [TestMethod]
        public void ArchiveSupportTest08EnumerateFiles()
        {
            string path = @"..\..\TestFiles\dummytext.txt";

            Assert.ThrowsException<TailPExceptionArchive>(() => ArchiveSupport.EnumerateFiles(path));
        }

        [TestMethod]
        public void ArchiveSupportTest09EnumerateFiles()
        {
            string path = @"..\..\TestFiles\1.zip\1\3\Annotations.cs";

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
            string path = @"..\..\TestFiles\1.zip\*.cs";

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
            string path = @"..\..\TestFiles\1.zip\1\?\Annotations - Copy.cs";

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
            string path = @"..\..\TestFiles\1.zip";

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
