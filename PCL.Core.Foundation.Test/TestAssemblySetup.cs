using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PCL.Core.Test;

[TestClass]
public static class TestAssemblySetup
{
    [AssemblyInitialize]
    public static void Initialize(TestContext _)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
}
