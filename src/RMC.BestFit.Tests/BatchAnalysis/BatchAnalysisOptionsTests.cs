using RMC.BestFit.Analyses;

namespace RMC.BestFit.Tests.BatchAnalysis;

/// <summary>
/// Unit tests for the <see cref="BatchAnalysisOptions"/> configuration class.
/// </summary>
[TestClass]
public class BatchAnalysisOptionsTests
{
    /// <summary>Verifies that defaults match serial continue on error order by dependency.</summary>
    [TestMethod]
    public void Test_Defaults_Match_SerialContinueOnError_OrderByDependency()
    {
        var options = new BatchAnalysisOptions();

        Assert.AreEqual(1, options.MaxDegreeOfParallelism, "Default is serial execution.");
        Assert.IsTrue(options.ContinueOnError);
        Assert.IsTrue(options.OrderByDependency);
    }

    /// <summary>Verifies that max degree of parallelism positive value stores value.</summary>
    [TestMethod]
    public void Test_MaxDegreeOfParallelism_PositiveValue_StoresValue()
    {
        var options = new BatchAnalysisOptions { MaxDegreeOfParallelism = 4 };

        Assert.AreEqual(4, options.MaxDegreeOfParallelism);
    }

    /// <summary>Verifies that max degree of parallelism throws when zero.</summary>
    [TestMethod]
    public void Test_MaxDegreeOfParallelism_Zero_ThrowsArgumentOutOfRange()
    {
        var options = new BatchAnalysisOptions();

        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => options.MaxDegreeOfParallelism = 0);
    }

    /// <summary>Verifies that max degree of parallelism throws when negative.</summary>
    [TestMethod]
    public void Test_MaxDegreeOfParallelism_Negative_ThrowsArgumentOutOfRange()
    {
        var options = new BatchAnalysisOptions();

        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => options.MaxDegreeOfParallelism = -1);
    }

    /// <summary>Verifies that continue on error can be disabled.</summary>
    [TestMethod]
    public void Test_ContinueOnError_CanBeDisabled()
    {
        var options = new BatchAnalysisOptions { ContinueOnError = false };

        Assert.IsFalse(options.ContinueOnError);
    }

    /// <summary>Verifies that order by dependency can be disabled.</summary>
    [TestMethod]
    public void Test_OrderByDependency_CanBeDisabled()
    {
        var options = new BatchAnalysisOptions { OrderByDependency = false };

        Assert.IsFalse(options.OrderByDependency);
    }
}
