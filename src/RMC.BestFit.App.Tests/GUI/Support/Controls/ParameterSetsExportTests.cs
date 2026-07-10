using DatabaseManager;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Data;
using System.IO;

namespace RMC.BestFit.App.Tests.GUI.Support.Controls
{
    /// <summary>
    /// Regression tests for parameter-set table export behavior.
    /// </summary>
    [TestClass]
    public class ParameterSetsExportTests
    {
        /// <summary>
        /// Verifies that an in-memory parameter table can be exported to XLSX through
        /// <see cref="DataTableView"/> without referencing Excel packages directly.
        /// </summary>
        [TestMethod]
        public void ExportToXlsx_FromInMemoryParameterTable_CreatesWorkbook()
        {
            var table = new DataTable("Parameter Sets");
            table.Columns.Add("Mu", typeof(double));
            table.Columns.Add("Sigma", typeof(double));
            table.Rows.Add(3.322475019522793, 0.4389);

            string path = Path.Combine(
                Path.GetTempPath(),
                "BestFitParameterSets_" + Guid.NewGuid().ToString("N") + ".xlsx");

            try
            {
                var reader = new InMemoryReader(table);
                DataTableView view = reader.GetTableManager(table.TableName);

                view.ExportToXlsx(path);

                var exportedFile = new FileInfo(path);
                Assert.IsTrue(exportedFile.Exists, "XLSX export should create the workbook file.");
                Assert.IsTrue(exportedFile.Length > 0, "XLSX export should write workbook content.");
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
