using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.ObjectModel;

namespace RMC.BestFit.App.Tests.GUI.Support.DataGrids
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.XOrdinateRowItem"/>.
    /// </summary>
    [TestClass]
    public class XOrdinateRowItemTests
    {
        /// <summary>
        /// Verifies the parameterless constructor does not throw and the row is usable.
        /// </summary>
        [TestMethod]
        public void Constructor_Parameterless_DoesNotThrow()
        {
            var row = new RMC_BestFit.XOrdinateRowItem();
            Assert.IsNotNull(row);
        }

        /// <summary>
        /// Verifies the parameterized constructor stores the value through the inherited <c>Value</c> getter.
        /// </summary>
        [TestMethod]
        public void Constructor_WithValue_ExposesValue()
        {
            var row = new RMC_BestFit.XOrdinateRowItem(new ObservableCollection<object>(), 1.5);
            Assert.AreEqual(1.5, row.Value, 1e-12);
        }

        /// <summary>
        /// Verifies the X-axis row reports its display label as "X Value" for the <c>Value</c>
        /// column.
        /// </summary>
        [TestMethod]
        public void PropertyDisplayName_Value_ReturnsXValue()
        {
            var row = new RMC_BestFit.XOrdinateRowItem();
            Assert.AreEqual("X Value", row.PropertyDisplayName(nameof(row.Value)));
        }

        /// <summary>
        /// Verifies the display-name accessor returns <c>null</c> for any unknown property.
        /// </summary>
        [TestMethod]
        public void PropertyDisplayName_UnknownProperty_ReturnsNull()
        {
            var row = new RMC_BestFit.XOrdinateRowItem();
            Assert.IsNull(row.PropertyDisplayName("Anything"));
        }
    }
}
