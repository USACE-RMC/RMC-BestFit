using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace RMC.BestFit.App.Tests.GUI.Support
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.DateValuePoint"/>.
    /// </summary>
    [TestClass]
    public class DateValuePointTests
    {
        /// <summary>
        /// Verifies that the constructor stores the DateTime argument correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_SetsDateTime()
        {
            var dt = new DateTime(2000, 10, 15, 12, 30, 0);
            var point = new RMC_BestFit.DateValuePoint(dt, 42.0);
            Assert.AreEqual(dt, point.DateTime);
        }

        /// <summary>
        /// Verifies that the constructor stores the Value argument correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_SetsValue()
        {
            var point = new RMC_BestFit.DateValuePoint(DateTime.Now, 1234.5);
            Assert.AreEqual(1234.5, point.Value, 1e-10);
        }

        /// <summary>
        /// Verifies that the DateTime property is mutable.
        /// </summary>
        [TestMethod]
        public void DateTime_CanBeUpdated()
        {
            var point = new RMC_BestFit.DateValuePoint(DateTime.MinValue, 0.0);
            var newDate = new DateTime(2023, 6, 1);
            point.DateTime = newDate;
            Assert.AreEqual(newDate, point.DateTime);
        }

        /// <summary>
        /// Verifies that the Value property is mutable.
        /// </summary>
        [TestMethod]
        public void Value_CanBeUpdated()
        {
            var point = new RMC_BestFit.DateValuePoint(DateTime.Now, 0.0);
            point.Value = 99.9;
            Assert.AreEqual(99.9, point.Value, 1e-10);
        }

        /// <summary>
        /// Verifies that a zero value is accepted.
        /// </summary>
        [TestMethod]
        public void Constructor_ZeroValue_IsAccepted()
        {
            var point = new RMC_BestFit.DateValuePoint(DateTime.Now, 0.0);
            Assert.AreEqual(0.0, point.Value, 1e-10);
        }

        /// <summary>
        /// Verifies that negative values are accepted.
        /// </summary>
        [TestMethod]
        public void Constructor_NegativeValue_IsAccepted()
        {
            var point = new RMC_BestFit.DateValuePoint(DateTime.Now, -999.99);
            Assert.AreEqual(-999.99, point.Value, 1e-10);
        }
    }
}
