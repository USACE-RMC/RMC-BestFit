using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// One (x, y) evaluation point of a bivariate analysis's joint-exceedance grid: the joint
    /// probability P(X &gt; x AND Y &gt; y) is evaluated at each point.
    /// </summary>
    public class XyOrdinateDto
    {
        /// <summary>
        /// The marginal-X magnitude of the evaluation point.
        /// </summary>
        [JsonPropertyName("x")]
        public double X { get; set; }

        /// <summary>
        /// The marginal-Y magnitude of the evaluation point.
        /// </summary>
        [JsonPropertyName("y")]
        public double Y { get; set; }
    }
}
