using Carvers.Infra.Math.Geometry;
using Carvers.Infra.Result;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Carvers.Infra.Tests.Math.Geometry
{
    [TestClass]
    public class LineExtensionTests
    {
        [TestClass]
        public class IntersectionPoint
        {
            [TestMethod]
            public void ShouldHaveNoIntersectionPointIfTheLinesAreSame()
            {
                var line1 = new Line<double, double>(1d, 1d, 2d, 2d);
                var line2 = new Line<double, double>(1d, 1d, 2d, 2d);

                line1.IntersectionPoint(line2).IsFailure.Should().BeTrue();
            }

            [TestMethod]
            public void ShouldHaveNoIntersectionPointIfTheLinesAreParallel()
            {
                var line1 = new Line<double, double>(1d, 1d, 2d, 2d);
                var line2 = new Line<double, double>(1d, 2d, 2d, 3d);

                line1.IntersectionPoint(line2).IsFailure.Should().BeTrue();
            }

            [TestMethod]
            public void ShouldHaveIntersectionPointIfTheLinesAreIntersecting()
            {
                var line1 = new Line<double, double>(1d, 1d, 3d, 3d);
                var line2 = new Line<double, double>(2d, 3d, 2d, 1d);

                var intersectionPoint = line1.IntersectionPoint(line2);
                intersectionPoint.IsSuccess.Should().BeTrue();

                intersectionPoint.ValueOrDefault().X.Should().Be(2d);
                intersectionPoint.ValueOrDefault().Y.Should().Be(2d);
            }

            [TestMethod]
            public void ShouldHaveNoIntersectionPointIfTheLinesAreDivergingFromOnePoint()
            {
                var line1 = new Line<double, double>(1d, 1d, 4d, 4d);
                var line2 = new Line<double, double>(2.5d, 3d, 2d, 4d);

                var intersectionPoint = line1.IntersectionPoint(line2);
                intersectionPoint.IsFailure.Should().BeTrue();
            }
        }
    }
}
