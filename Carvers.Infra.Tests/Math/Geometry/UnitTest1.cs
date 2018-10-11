using Carvers.Infra.Math.Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Carvers.Infra.Tests.Math.Geometry
{
    [TestClass]
    public class LineExtensionTests
    {
        [TestMethod]
        public void FindIntersectionOnSameLineTest()
        {
            var line1 = new Line<double, double>(1d, 1d, 2d, 2d);
            var line2 = new Line<double, double>(1d, 1d, 2d, 2d);

            Assert.AreEqual(line1.FindIntersection(line2).X, double.NaN);
            Assert.AreEqual(line1.FindIntersection(line2).Y, double.NaN);
        }
    }
}
