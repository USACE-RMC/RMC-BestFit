using Numerics.Distributions;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>Protects explicit point-process parameter assignment when automatic initialization is unavailable.</summary>
[TestClass]
public class PointProcessInitializationRegressionTests
{
    /// <summary>Short samples retain the full editable parameter structure and the automatic-prior diagnostic.</summary>
    /// <param name="isSeasonal">Whether two component distributions and changepoints are required.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ShortSample_AllowsExplicitParameters_WithoutClaimingAutomaticPriors(bool isSeasonal)
    {
        var frame = new RMC.BestFit.Models.DataFrame
        {
            ExactSeries = new ExactSeries(new List<ExactData>
            {
                new(new DateTime(2000, 1, 1), 85d),
                new(new DateTime(2000, 1, 2), 100d),
                new(new DateTime(2000, 1, 3), 120d)
            }),
            PointProcessObservationYears = 1.25d
        };
        var model = new PointProcessModel { UseDefaults = false, IsSeasonal = isSeasonal, DataFrame = frame };
        model.Threshold = 80d;
        model.TotalYears = 1.25d;
        model.SetDefaultParameters();
        double[] values = isSeasonal
            ? new[] { 90d, 270d, 100d, 20d, -.1d, 120d, 25d, .1d }
            : new[] { 100d, 20d, -.1d };
        Assert.AreEqual(values.Length, model.Parameters.Count);
        model.SetParameterValues(values);
        CollectionAssert.AreEqual(values, model.Parameters.Select(parameter => parameter.Value).ToArray());
        Assert.IsTrue(double.IsFinite(model.DataLogLikelihood(values)));
        var validation = model.Validate();
        Assert.IsFalse(validation.IsValid);
        Assert.IsTrue(validation.ValidationMessages.Any(message => message.Contains("Insufficient observations", StringComparison.Ordinal)));
        ModelParameter[] existing = model.Parameters.ToArray();
        string[] states = existing.Select(parameter => parameter.ToXElement().ToString()).ToArray();
        model.SetDefaultParameters();
        for (int i = 0; i < existing.Length; i++)
        {
            Assert.AreSame(existing[i], model.Parameters[i]);
            Assert.AreEqual(states[i], model.Parameters[i].ToXElement().ToString());
        }
    }
}
