using Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace UnitTests
{
    [TestClass]
    public class JsonTest
    {
        [TestMethod]
        public void StringTest()
        {
            StringWriter writer = new StringWriter();
            JsonSerializer serializer = new JsonSerializer();
            serializer.Serialize(writer, "abc123\t\r\n\b\\\" \u0001\u001f");
            Assert.AreEqual(
                "\"abc123\\t\\r\\n\\b\\\\\\\" \\u0001\\u001F\"",
                writer.ToString());
        }
    }
}
