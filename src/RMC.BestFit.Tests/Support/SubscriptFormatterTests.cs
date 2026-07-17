using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.Support;

/// <summary>
/// Unit tests for the <c>SubscriptFormatter</c> Unicode-subscript helper.
/// </summary>
[TestClass]
public class SubscriptFormatterTests
{
    /// <summary>Verifies that to subscript returns subscript zero when zero.</summary>
    [TestMethod]
    public void Test_ToSubscript_Zero_ReturnsSubscriptZero()
    {
        Assert.AreEqual("₀", SubscriptFormatter.ToSubscript(0));
    }

    /// <summary>Verifies that to subscript single digit maps to unicode subscript.</summary>
    [TestMethod]
    public void Test_ToSubscript_SingleDigit_MapsToUnicodeSubscript()
    {
        // U+2080..U+2089 are the subscript digits 0..9.
        Assert.AreEqual("₁", SubscriptFormatter.ToSubscript(1));
        Assert.AreEqual("₅", SubscriptFormatter.ToSubscript(5));
        Assert.AreEqual("₉", SubscriptFormatter.ToSubscript(9));
    }

    /// <summary>Verifies that to subscript multi digit maps all digits.</summary>
    [TestMethod]
    public void Test_ToSubscript_MultiDigit_MapsAllDigits()
    {
        // 12 → "₁₂"
        Assert.AreEqual("₁₂", SubscriptFormatter.ToSubscript(12));
        // 1234567890 → all ten digits
        var expected = "₁₂₃₄₅₆₇₈₉₀";
        Assert.AreEqual(expected, SubscriptFormatter.ToSubscript(1234567890));
    }

    /// <summary>Verifies that to subscript preserves sign and converts digits for negative number.</summary>
    [TestMethod]
    public void Test_ToSubscript_NegativeNumber_PreservesSignAndConvertsDigits()
    {
        // Minus sign isn't in the digit lookup, so it falls through unchanged.
        // Followed by subscript digits.
        Assert.AreEqual("-₁₂", SubscriptFormatter.ToSubscript(-12));
    }

    /// <summary>Verifies that to subscript large value maps correctly.</summary>
    [TestMethod]
    public void Test_ToSubscript_LargeValue_MapsCorrectly()
    {
        // Sanity check on int.MaxValue (2147483647).
        var result = SubscriptFormatter.ToSubscript(int.MaxValue);

        Assert.AreEqual(int.MaxValue.ToString().Length, result.Length);
        // Every character should be a subscript digit (no unmapped ASCII).
        foreach (var c in result)
        {
            Assert.IsTrue(c >= '₀' && c <= '₉',
                $"Unexpected character '{c}' (U+{(int)c:X4}) in subscript output.");
        }
    }
}
