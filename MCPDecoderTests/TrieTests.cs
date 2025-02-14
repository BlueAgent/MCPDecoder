using MCPDecoder;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MCPDecoderTests
{
    [TestClass]
    public class TrieTests
    {
        [DataTestMethod]
        [DataRow("(?:word)", "word")]
        [DataRow("(?:word(?:s)?)", "word", "words")]
        [DataRow("(?:word(?:s(?:s)?)?)", "word", "words", "wordss")]
        [DataRow("(?:word(?:s(?:s(?:s)?)?)?)", "word", "words", "wordss", "wordsss")]
        [DataRow("(?:(?:birds|word))", "word", "birds")]
        [DataRow("(?:(?:func_123456|method_123456))", "func_123456", "method_123456")]
        [DataRow("(?:func_(?:123456|654321))", "func_123456", "func_654321")]
        public void TestRegex(string regex, params string[] words)
        {
            var trie = new Trie();

            foreach (var word in words)
            {
                trie.Add(word);
            }

            Assert.AreEqual(regex, trie.ToRegex());
        }

        [DataTestMethod]
        [DataRow("word")]
        [DataRow("word", "words")]
        [DataRow("word", "words", "wordss")]
        [DataRow("word", "words", "wordss", "wordsss")]
        [DataRow("word", "birds")]
        [DataRow("func_123456", "method_123456")]
        [DataRow("func_123456", "func_654321")]
        public void TestRegexMatches(params string[] words)
        {
            var trie = new Trie();

            foreach (var word in words)
            {
                trie.Add(word);
            }

            var regex = new Regex($"^(?:{trie.ToRegex()})$");

            foreach (var word in words)
            {
                Assert.IsTrue(regex.IsMatch(word));
            }
        }
    }
}
