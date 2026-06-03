using System.Xml.Linq;
using Numerics.Functions;
using RMC.BestFit.Models.LinkFunctions;

namespace RMC.BestFit.Tests.LinkFunctions;

/// <summary>
/// Unit tests for XElement serialization round-trips of BestFit link functions,
/// the BestFitLinkFunctionFactory, and LinkController with BestFit-specific types.
/// </summary>
/// <remarks>
/// <para>
///     <b> Authors: </b>
///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
/// </para>
/// </remarks>
[TestClass]
public class LinkFunctionSerializationTests
{
    /// <summary>
    /// Tolerance for double comparisons.
    /// </summary>
    private const double Tol = 1e-15;

    #region SESLink Serialization

    /// <summary>
    /// Verify SESLink round-trip: non-default properties survive ToXElement → XElement constructor.
    /// </summary>
    [TestMethod]
    public void SESLink_RoundTrip()
    {
        // Arrange — all non-default values
        var original = new SESLink(
            a: 1.5,
            useAdaptiveLambda: false,
            parentIndicator: 2.3,
            lambdaMax: 0.6,
            lambdaSlope: 0.8,
            maxIterations: 30,
            tolerance: 1e-10)
        {
            Lambda = 0.35
        };

        // Act
        var xml = original.ToXElement();
        var restored = new SESLink(xml);

        // Assert — all 8 properties
        Assert.AreEqual(original.A, restored.A, Tol);
        Assert.AreEqual(original.Lambda, restored.Lambda, Tol);
        Assert.AreEqual(original.UseAdaptiveLambda, restored.UseAdaptiveLambda);
        Assert.AreEqual(original.ParentIndicator, restored.ParentIndicator, Tol);
        Assert.AreEqual(original.LambdaMax, restored.LambdaMax, Tol);
        Assert.AreEqual(original.LambdaSlope, restored.LambdaSlope, Tol);
        Assert.AreEqual(original.MaxIterations, restored.MaxIterations);
        Assert.AreEqual(original.Tolerance, restored.Tolerance, Tol);

        // Verify functional equivalence
        Assert.AreEqual(original.Link(5.0), restored.Link(5.0), 1e-12);
        Assert.AreEqual(original.InverseLink(2.0), restored.InverseLink(2.0), 1e-12);
    }

    /// <summary>
    /// Verify SESLink XElement name is correct.
    /// </summary>
    [TestMethod]
    public void SESLink_ToXElement_ElementName()
    {
        var link = new SESLink();
        var xml = link.ToXElement();
        Assert.AreEqual("SESLink", xml.Name.LocalName);
    }

    #endregion

    #region LogSESLink Serialization

    /// <summary>
    /// Verify LogSESLink round-trip: non-default properties survive serialization.
    /// </summary>
    [TestMethod]
    public void LogSESLink_RoundTrip()
    {
        // Arrange
        var original = new LogSESLink(sigma0: 2.5, a: 0.8, lambda: 0.15)
        {
            MaxIterations = 25,
            Tolerance = 1e-10,
            Eps = 1e-10
        };

        // Act
        var xml = original.ToXElement();
        var restored = new LogSESLink(xml);

        // Assert — all 6 properties
        Assert.AreEqual(original.Sigma0, restored.Sigma0, Tol);
        Assert.AreEqual(original.A, restored.A, Tol);
        Assert.AreEqual(original.Lambda, restored.Lambda, Tol);
        Assert.AreEqual(original.MaxIterations, restored.MaxIterations);
        Assert.AreEqual(original.Tolerance, restored.Tolerance, Tol);
        Assert.AreEqual(original.Eps, restored.Eps, Tol);

        // Verify functional equivalence
        Assert.AreEqual(original.Link(3.0), restored.Link(3.0), 1e-12);
        Assert.AreEqual(original.InverseLink(0.5), restored.InverseLink(0.5), 1e-12);
    }

    /// <summary>
    /// Verify LogSESLink XElement name is correct.
    /// </summary>
    [TestMethod]
    public void LogSESLink_ToXElement_ElementName()
    {
        var link = new LogSESLink();
        var xml = link.ToXElement();
        Assert.AreEqual("LogSESLink", xml.Name.LocalName);
    }

    #endregion

    #region LogASinHLink Serialization

    /// <summary>
    /// Verify LogASinHLink round-trip: non-default properties survive serialization.
    /// </summary>
    [TestMethod]
    public void LogASinHLink_RoundTrip()
    {
        var original = new LogASinHLink(sigma0: 2.5, logScale: 0.35, epsilon: 0.2, delta: 0.9)
        {
            UseAdaptiveEpsilon = true,
            ParentIndicator = 0.6,
            EpsilonMax = 0.75,
            EpsilonSlope = 1.3,
            Eps = 1e-10
        };

        var xml = original.ToXElement();
        var restored = new LogASinHLink(xml);

        Assert.AreEqual(original.Sigma0, restored.Sigma0, Tol);
        Assert.AreEqual(original.LogScale, restored.LogScale, Tol);
        Assert.AreEqual(original.Epsilon, restored.Epsilon, Tol);
        Assert.AreEqual(original.Delta, restored.Delta, Tol);
        Assert.AreEqual(original.UseAdaptiveEpsilon, restored.UseAdaptiveEpsilon);
        Assert.AreEqual(original.ParentIndicator, restored.ParentIndicator, Tol);
        Assert.AreEqual(original.EpsilonMax, restored.EpsilonMax, Tol);
        Assert.AreEqual(original.EpsilonSlope, restored.EpsilonSlope, Tol);
        Assert.AreEqual(original.Eps, restored.Eps, Tol);
        Assert.AreEqual(original.Link(3.0), restored.Link(3.0), 1e-12);
        Assert.AreEqual(original.InverseLink(0.5), restored.InverseLink(0.5), 1e-12);
    }

    /// <summary>
    /// Verify LogASinHLink XElement name is correct.
    /// </summary>
    [TestMethod]
    public void LogASinHLink_ToXElement_ElementName()
    {
        var link = new LogASinHLink();
        var xml = link.ToXElement();
        Assert.AreEqual("LogASinHLink", xml.Name.LocalName);
    }

    #endregion

    #region CenteredLink Serialization

    /// <summary>
    /// Verify CenteredLink round-trip with SESLink as inner link.
    /// </summary>
    [TestMethod]
    public void CenteredLink_RoundTrip_WithSESInner()
    {
        // Arrange
        var innerOriginal = new SESLink(a: 1.2, useAdaptiveLambda: false) { Lambda = 0.25 };
        var original = new CenteredLink(innerOriginal, mu0: 100.0, scale: 20.0);

        // Act
        var xml = original.ToXElement();
        var restored = new CenteredLink(xml);

        // Assert outer properties
        Assert.AreEqual(original.Mu0, restored.Mu0, Tol);
        Assert.AreEqual(original.Scale, restored.Scale, Tol);

        // Assert inner type and properties
        Assert.IsInstanceOfType(restored.Inner, typeof(SESLink));
        var restoredInner = (SESLink)restored.Inner;
        Assert.AreEqual(innerOriginal.A, restoredInner.A, Tol);
        Assert.AreEqual(innerOriginal.Lambda, restoredInner.Lambda, Tol);
        Assert.AreEqual(innerOriginal.UseAdaptiveLambda, restoredInner.UseAdaptiveLambda);

        // Verify functional equivalence
        Assert.AreEqual(original.Link(110.0), restored.Link(110.0), 1e-12);
        Assert.AreEqual(original.InverseLink(0.5), restored.InverseLink(0.5), 1e-12);
    }

    /// <summary>
    /// Verify CenteredLink round-trip with IdentityLink as inner link.
    /// </summary>
    [TestMethod]
    public void CenteredLink_RoundTrip_WithIdentityInner()
    {
        // Arrange
        var original = new CenteredLink(new IdentityLink(), mu0: 50.0, scale: 10.0);

        // Act
        var xml = original.ToXElement();
        var restored = new CenteredLink(xml);

        // Assert
        Assert.AreEqual(original.Mu0, restored.Mu0, Tol);
        Assert.AreEqual(original.Scale, restored.Scale, Tol);
        Assert.IsInstanceOfType(restored.Inner, typeof(IdentityLink));

        Assert.AreEqual(original.Link(55.0), restored.Link(55.0), 1e-12);
    }

    /// <summary>
    /// Verify CenteredLink XElement name is correct.
    /// </summary>
    [TestMethod]
    public void CenteredLink_ToXElement_ElementName()
    {
        var link = new CenteredLink(new IdentityLink(), 0.0);
        var xml = link.ToXElement();
        Assert.AreEqual("CenteredLink", xml.Name.LocalName);
    }

    #endregion

    #region BestFitLinkFunctionFactory

    /// <summary>
    /// Verify BestFitLinkFunctionFactory handles all known Numerics and BestFit link types.
    /// </summary>
    [TestMethod]
    public void BestFitLinkFunctionFactory_AllTypes()
    {
        // Numerics types
        Assert.IsInstanceOfType(BestFitLinkFunctionFactory.CreateFromXElement(new XElement("IdentityLink")), typeof(IdentityLink));
        Assert.IsInstanceOfType(BestFitLinkFunctionFactory.CreateFromXElement(new XElement("LogLink")), typeof(LogLink));
        Assert.IsInstanceOfType(BestFitLinkFunctionFactory.CreateFromXElement(new XElement("LogitLink")), typeof(LogitLink));
        Assert.IsInstanceOfType(BestFitLinkFunctionFactory.CreateFromXElement(new XElement("ProbitLink")), typeof(ProbitLink));
        Assert.IsInstanceOfType(BestFitLinkFunctionFactory.CreateFromXElement(new XElement("ComplementaryLogLogLink")), typeof(ComplementaryLogLogLink));

        // BestFit types
        Assert.IsInstanceOfType(BestFitLinkFunctionFactory.CreateFromXElement(new XElement("SESLink")), typeof(SESLink));
        Assert.IsInstanceOfType(BestFitLinkFunctionFactory.CreateFromXElement(new XElement("LogSESLink")), typeof(LogSESLink));
        Assert.IsInstanceOfType(BestFitLinkFunctionFactory.CreateFromXElement(new XElement("LogASinHLink")), typeof(LogASinHLink));

        // CenteredLink needs at minimum a valid element (will default to IdentityLink inner)
        var centeredXml = new XElement("CenteredLink");
        Assert.IsInstanceOfType(BestFitLinkFunctionFactory.CreateFromXElement(centeredXml), typeof(CenteredLink));
    }

    /// <summary>
    /// Verify BestFitLinkFunctionFactory throws for unknown types.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void BestFitLinkFunctionFactory_UnknownType_Throws()
    {
        BestFitLinkFunctionFactory.CreateFromXElement(new XElement("UnknownLink"));
    }

    #endregion

    #region LinkController with BestFit Links

    /// <summary>
    /// Verify LinkController round-trip with BestFit-specific link types using manual deserialization.
    /// </summary>
    [TestMethod]
    public void LinkController_RoundTrip_WithBestFitLinks()
    {
        // Arrange — controller with SESLink and LogSESLink
        var sesLink = new SESLink(a: 1.3, useAdaptiveLambda: false) { Lambda = 0.2 };
        var logSesLink = new LogSESLink(sigma0: 2.0, a: 0.9);
        var original = new LinkController(sesLink, logSesLink, null);

        // Act — serialize
        var xml = original.ToXElement();

        // Deserialize with BestFit factory (manual, same pattern as B17C XElement ctor)
        var slots = xml.Elements("Link").ToList();
        var links = new ILinkFunction?[slots.Count];
        foreach (var slot in slots)
        {
            var indexAttr = slot.Attribute("Index");
            if (indexAttr == null) continue;
            if (!int.TryParse(indexAttr.Value, out int index)) continue;
            if (index < 0 || index >= links.Length) continue;
            var child = slot.Elements().FirstOrDefault();
            if (child != null)
                links[index] = BestFitLinkFunctionFactory.CreateFromXElement(child);
        }
        var restored = new LinkController(links);

        // Assert
        Assert.AreEqual(3, restored.Count);
        Assert.IsInstanceOfType(restored[0], typeof(SESLink));
        Assert.IsInstanceOfType(restored[1], typeof(LogSESLink));
        Assert.IsNull(restored[2]);

        // Verify properties survived
        var restoredSes = (SESLink)restored[0]!;
        Assert.AreEqual(sesLink.A, restoredSes.A, Tol);
        Assert.AreEqual(sesLink.Lambda, restoredSes.Lambda, Tol);

        var restoredLogSes = (LogSESLink)restored[1]!;
        Assert.AreEqual(logSesLink.Sigma0, restoredLogSes.Sigma0, Tol);
        Assert.AreEqual(logSesLink.A, restoredLogSes.A, Tol);
    }

    /// <summary>
    /// Verify LinkController round-trip with all null slots.
    /// </summary>
    [TestMethod]
    public void LinkController_RoundTrip_NullSlots()
    {
        var original = new LinkController(null, null, null);
        var xml = original.ToXElement();
        var restored = new LinkController(xml);

        Assert.AreEqual(3, restored.Count);
        Assert.IsNull(restored[0]);
        Assert.IsNull(restored[1]);
        Assert.IsNull(restored[2]);
    }

    #endregion
}
