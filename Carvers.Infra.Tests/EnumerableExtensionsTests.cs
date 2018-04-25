using System;
using System.Collections.Generic;
using System.Linq;
using Carvers.Infra.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Carvers.Infra.Tests
{
    [TestClass]
    public class EnumerableExtensionsTests
    {
        [TestMethod]
        public void GroupByTests()
        {
            var startTime = new DateTimeOffset(2018,2,1,0,0,0,TimeSpan.Zero);
            var data = new Dictionary<DateTimeOffset, double>();

            Enumerable.Range(0, 120)
                .Select(val => startTime.AddMinutes(val))
                .Foreach(time => data.Add(time, time.Minute));

            data.Foreach(kvp => Console.WriteLine(kvp.Key + ":" + kvp.Value));

            data.GroupBy(60)
                .Select(grp =>
                    new KeyValuePair<DateTimeOffset, double>(grp.Last().Key, grp.Average(kvp => kvp.Value)))
                .Foreach(kvp => Console.WriteLine(kvp.Key + ":" + kvp.Value));


        }


        [TestMethod]
        public void IsInAscendingTest()
        {
            var values = new double[] {1, 2, 3, 4};
            Assert.IsTrue(values.IsInAscending());

            values = new double[] { 1, 2, 4, 3 };
            Assert.IsFalse(values.IsInAscending());

            values = new double[] { 1, 2, 3, 3 };
            Assert.IsFalse(values.IsInAscending());


            Assert.IsTrue(values.Take(2).IsInAscending());

            // Take last will return [4, 3]
            values = new double[] { 3, 2, 3, 4 };
            Assert.IsFalse(values.TakeLastInReverse(2).IsInAscending());


            // Take last will return [4, 5]
            values = new double[] { 3, 2, 5, 4 };
            Assert.IsTrue(values.TakeLastInReverse(2).IsInAscending());
        }


        [TestMethod]
        public void IsInDescendingTest()
        {
            var values = new double[] { 1, 2, 3, 4 };
            Assert.IsFalse(values.IsInDescending());

            values = new double[] { 4,3,2,1 };
            Assert.IsTrue(values.IsInDescending());

            values = new double[] { 1, 2, 3, 3 };
            Assert.IsFalse(values.IsInDescending());

            // Take last will return [4, 3]
            values = new double[] { 3, 2, 5, 4 };
            Assert.IsFalse(values.TakeLastInReverse(2).IsInDescending());

            // Take last will return [5, 4]
            values = new double[] { 3, 2, 4, 5 };
            Assert.IsTrue(values.TakeLastInReverse(2).IsInDescending());

            values = new double[] { 3, 2, 4, 5 };
            Assert.IsTrue(values.Take(2).IsInDescending());


            values = new double[] { 3, 2, 4, 5 };
            Assert.IsFalse(values.Take(3).IsInDescending());
        }


    }
}
