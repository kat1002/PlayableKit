using NUnit.Framework;
using PlayableKit.Editor;

namespace PlayableKit.Tests
{
    public class PALunaExternalSourcesTests
    {
        private const string Current = ".\\Library\\PackageCache\\com.kat.playablekit@91bd3aa71c";

        private static string LunaJson(string externalSources)
        {
            return "{\n    \"unity\": {\n        \"scripts\": {\n            \"excludes\": [\n                \"**/Editor/\"\n            ],\n            \"externalSources\": " + externalSources + ",\n            \"externalJSLibraries\": []\n        }\n    }\n}\n";
        }

        [Test]
        public void EmptyList_GetsTheKit_RestOfFileUntouched()
        {
            Assert.IsTrue(PALunaExternalSources.TryRewrite(LunaJson("[]"), Current, out string result, out _));
            string expected = LunaJson("[\n                \".\\\\Library\\\\PackageCache\\\\com.kat.playablekit@91bd3aa71c\"\n            ]");
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void StaleHash_IsReplaced_OtherSourcesKept()
        {
            string json = LunaJson("[\n                \".\\\\Library\\\\PackageCache\\\\com.kat.playablekit@0ldhash\",\n                \".\\\\Library\\\\PackageCache\\\\com.other.lib@1234\"\n            ]");
            Assert.IsTrue(PALunaExternalSources.TryRewrite(json, Current, out string result, out _));
            StringAssert.DoesNotContain("0ldhash", result);
            StringAssert.Contains("com.other.lib@1234", result);
            StringAssert.Contains("com.kat.playablekit@91bd3aa71c", result);
        }

        [Test]
        public void CurrentEntry_LeavesFileByteForByte()
        {
            string json = LunaJson("[ \".\\\\Library\\\\PackageCache\\\\com.kat.playablekit@91bd3aa71c\" ]");
            Assert.IsTrue(PALunaExternalSources.TryRewrite(json, Current, out string result, out _));
            Assert.AreSame(json, result);
        }

        [Test]
        public void CurrentPlusStale_DropsOnlyTheStaleOne()
        {
            string json = LunaJson("[\".\\\\Library\\\\PackageCache\\\\com.kat.playablekit@91bd3aa71c\", \"./Packages/com.kat.playablekit/\"]");
            Assert.IsTrue(PALunaExternalSources.TryRewrite(json, Current, out string result, out _));
            StringAssert.DoesNotContain("./Packages/com.kat.playablekit/", result);
            Assert.AreEqual(1, CountOf(result, "com.kat.playablekit"));
        }

        [Test]
        public void CrlfFile_StaysCrlf()
        {
            string json = LunaJson("[]").Replace("\n", "\r\n");
            Assert.IsTrue(PALunaExternalSources.TryRewrite(json, Current, out string result, out _));
            Assert.AreEqual(0, CountOf(result.Replace("\r\n", ""), "\n"));
        }

        [Test]
        public void NoExternalSourcesKey_ReportsInsteadOfGuessing()
        {
            string json = "{ \"unity\": { \"scripts\": { \"excludes\": [] } } }";
            Assert.IsFalse(PALunaExternalSources.TryRewrite(json, Current, out string result, out string error));
            Assert.AreSame(json, result);
            Assert.IsNotNull(error);
        }

        [Test]
        public void IsKitFolder_MatchesOnlyThisPackage()
        {
            Assert.IsTrue(PALunaExternalSources.IsKitFolder(".\\Library\\PackageCache\\com.kat.playablekit@abc"));
            Assert.IsTrue(PALunaExternalSources.IsKitFolder("./Packages/com.kat.playablekit/"));
            Assert.IsFalse(PALunaExternalSources.IsKitFolder("./Packages/com.kat.playablekit.extras"));
            Assert.IsFalse(PALunaExternalSources.IsKitFolder(".\\Library\\PackageCache\\com.other.lib@abc"));
        }

        private static int CountOf(string text, string part)
        {
            int count = 0;
            for (int i = text.IndexOf(part, System.StringComparison.Ordinal); i >= 0; i = text.IndexOf(part, i + part.Length, System.StringComparison.Ordinal)) count++;
            return count;
        }
    }
}
