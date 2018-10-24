using System;
using System.Numerics;
using Carvers.Infra.Math.Geometry;
using Carvers.Infra.Result;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mathf = System.Math;
using Vector = System.Windows.Vector;

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

                line1.IntersectionPoint(line2).Item1.IsFailure.Should().BeTrue();
            }

            [TestMethod]
            public void ShouldHaveNoIntersectionPointIfTheLinesAreParallel()
            {
                var line1 = new Line<double, double>(1d, 1d, 2d, 2d);
                var line2 = new Line<double, double>(1d, 2d, 2d, 3d);

                line1.IntersectionPoint(line2).Item1.IsFailure.Should().BeTrue();
            }

            [TestMethod]
            public void ShouldHaveIntersectionPointIfTheLinesAreIntersecting()
            {
                var line1 = new Line<double, double>(1d, 1d, 3d, 3d);
                var line2 = new Line<double, double>(2d, 3d, 2d, 1d);

                var intersectionPoint = line1.IntersectionPoint(line2);
                intersectionPoint.Item1.IsSuccess.Should().BeTrue();

                intersectionPoint.Item1.ValueOrDefault().X.Should().Be(2d);
                intersectionPoint.Item1.ValueOrDefault().Y.Should().Be(2d);

                intersectionPoint.Item2.Should().BeTrue();
            }

            [TestMethod]
            public void ShouldHaveIntersectionPointIfTheLineSegmentsAreNotIntersectingButTheLinesIntersect()
            {
                var line1 = new Line<double, double>(1d, 1d, 4d, 4d);
                var line2 = new Line<double, double>(2.5d, 3d, 2d, 4d);

                var intersectionPoint = line1.IntersectionPoint(line2);
                intersectionPoint.Item1.IsSuccess.Should().BeTrue();

                intersectionPoint.Item2.Should().BeFalse();
            }
        }


        [TestClass]
        public class IntersectionAngle
        {
            [TestMethod]
            public void ShouldReturnAnAngleOf90degreesForPerpendicularLines()
            {
                var line1 = new Line<double, double>(0,0, 5,0);
                var line2 = new Line<double, double>(3, 1, 3, 0);

                Assert.AreEqual(90, line1.IntersectionAngleAtPoint1(line2));
            }

            [TestMethod]
            public void ShouldReturnAnAngleOfNegative90degreesForPerpendicularLinesInTheNegativeDirection()
            {
                var line1 = new Line<double, double>(0, 0, 5, 0);
                var line2 = new Line<double, double>(3, -1, 3, 0);

                Assert.AreEqual(-90d, line1.IntersectionAngleAtPoint1(line2));
            }

            [TestMethod]
            public void ShouldReturnAnAngleOf45degreesForTwoLinesAt45Degrees()
            {
                var line1 = new Line<double, double>(0, 0, 5, 0);
                var line2 = new Line<double, double>(3, 3, 0, 0);

                Assert.AreEqual(45, line1.IntersectionAngleAtPoint1(line2));
            }

            [TestMethod]
            public void LineAngleToHorizontalAxis()
            {
                var line2 = new Line<double, double>(0, 0, 5, 0);
                var line1 = new Line<double, double>(0, 0, 3, 3);
                var actual = line1.IntersectionAngleAtPoint1(line2);
                Assert.AreEqual(45, actual);

            }

            [TestMethod]
            public void FourthQuadrantLineAngleToHorizontalAxis()
            {
                var line1 = new Line<double, double>(0, 4, 0,8);
                var line2 = new Line<double, double>(5, 0, 3, 0);
                var actual = line1.IntersectionAngleAtPoint1(line2);
                //Assert.AreEqual(45, actual);

                var actual2 = line1.AngleToHorizontalAxis();

                var extedingDownInY = new Line<double, double>(0, 8, 0, 4);
                var shouldBePos90 = extedingDownInY.AngleToHorizontalAxis();

                var extednginUpInY = new Line<double, double>(0, 4, 0, 8);
                var shouldBeNeg90 = extednginUpInY.AngleToHorizontalAxis();

                var line4 = new Line<double, double>(4, 0, 6, 0);
                var shouldBePos180 = line4.AngleToHorizontalAxis();

                var line5 = new Line<double, double>(6, 0, 4, 0);
                var shouldBePos0 = line5.AngleToHorizontalAxis();

                var vector =  new System.Windows.Vector(0d, 1055d);
                var vector2 = new System.Windows.Vector(1d, 1050d);
                var origin = new System.Windows.Vector(2d, 1045d);

                var angle = System.Windows.Vector.AngleBetween(vector, vector2);

                var a = VectorHelper.GetAngle(vector, vector2, origin);
            }
        }
    }
}
