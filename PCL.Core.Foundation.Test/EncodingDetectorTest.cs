using System.Text;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.Codecs;

namespace PCL.Core.Test;
[TestClass]
public class EncodingDetectorTest
{
    [TestMethod]
    public void TestEncoding()
    {
        var utf8 = Encoding.UTF8.GetBytes("Hi, There!");
        Assert.AreEqual(EncodingDetector.DetectEncoding(utf8), Encoding.UTF8);
        utf8 = Encoding.UTF8.GetBytes("棍斤拷烫烫烫");
        Assert.AreEqual(EncodingDetector.DetectEncoding(utf8), Encoding.UTF8);
        var utf16 = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("你好世界")).ToArray();
        Assert.AreEqual(Encoding.Unicode.WebName, EncodingDetector.DetectEncoding(utf16).WebName);
        byte[] nonEncode = [0xfe, 0x5f, 0xa1];
        Assert.AreEqual(Encoding.Default, EncodingDetector.DetectEncoding(nonEncode));
    }
}
