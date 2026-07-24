<!-- technical-reference-status: complete -->

# Link Functions

[<- Trend functions](trend-functions.md) | [Technical reference index](../index.md) | [Parameterization crosswalk ->](../appendices/parameterization-crosswalk.md)

## Purpose and contract

A link function maps a natural-space value $x$ to a working value $\eta=h(x)$; `InverseLink` evaluates $h^{-1}(\eta)`. RMC.Numerics defines the `ILinkFunction` contract used by RMC.BestFit:

| Member | Meaning |
|---|---|
| `Link(x)` | $\eta=h(x)$ |
| `InverseLink(eta)` | $x=h^{-1}(\eta)$ |
| `DLink(x)` | $dh(x)/dx$ evaluated in natural space |
| `ToXElement()` | invariant-culture configuration serialization |

The direction is important: `DLink` is not $dx/d\eta$. The link object exposes the derivative but does not itself add a change-of-variables term to a likelihood. A caller that defines a probability density in link coordinates is responsible for the appropriate Jacobian. When a link is merely a deterministic regression mapping, no density transformation is implied.

## Standard Numerics links

The following implementations come from RMC.Numerics 2.1.4 at commit `828664650c9327b309ee8332e707ccca73588e93`.

| Class | Natural domain | $h(x)$ | $h^{-1}(\eta)$ | $h'(x)$ |
|---|---|---|---|---|
| `IdentityLink` | $\mathbb R$ | $x$ | $\eta$ | $1$ |
| `LogLink` | $x>0$ | $\log x$ | $\exp\eta$ | $1/x$ |
| `LogitLink` | $0<x<1$ | $\log[x/(1-x)]$ | $[1+\exp(-\eta)]^{-1}$ | $1/[x(1-x)]$ |
| `ProbitLink` | $0<x<1$ | $\Phi^{-1}(x)$ | $\Phi(\eta)$ | $1/\phi[\Phi^{-1}(x)]$ |
| `ComplementaryLogLogLink` | $0<x<1$ | $\log[-\log(1-x)]$ | $1-\exp[-\exp(\eta)]$ | $1/\{(1-x)[-\log(1-x)]\}$ |
| `FisherZLink` | $-1<x<1$ | $\operatorname{atanh}x$ | $\tanh\eta$ | $1/(1-x^2)$ |

`LogLink` validates positivity and uses a positive numerical floor after validation. Probability links require the open unit interval and use interior clamps for stable inverse/derivative calculations. `FisherZLink` similarly requires the open correlation interval. A caller should treat domain exceptions or boundary saturation as a model-specification issue, not silently coerce a scientifically invalid value.

## Yeo-Johnson link

`YeoJohnsonLink(lambda)` supports all real $x$ and requires finite $\lambda\in[-5,5]`. Its default is $\lambda=1$, the identity transformation. The forward transformation is [1](#ref-1)

$$
h_\lambda(x)=
\begin{cases}
\{(x+1)^\lambda-1\}/\lambda, & x\ge0,\ \lambda\ne0,\\
\log(x+1), & x\ge0,\ \lambda=0,\\
-\{(1-x)^{2-\lambda}-1\}/(2-\lambda), & x<0,\ \lambda\ne2,\\
-\log(1-x), & x<0,\ \lambda=2.
\end{cases}
\tag{1}
$$

and

$$
h_\lambda'(x)=
\begin{cases}
(x+1)^{\lambda-1}, & x\ge0,\\
(1-x)^{1-\lambda}, & x<0.
\end{cases}
\tag{2}
$$

The constructor taking representative values estimates $\lambda$ through the Numerics Yeo-Johnson fitter and requires at least two finite values. Fitted transformation parameters add uncertainty if treated as fixed in subsequent inference; the API does not automatically propagate uncertainty in the estimated $\lambda$.

## Centering and scaling wrapper

`CenteredLink(inner, mu0, scale)` composes any link with an affine standardization. With $s=\max(\mathrm{scale},10^{-12})$,

$$
z=\frac{x-\mu_0}{s},\qquad
h_C(x)=h(z),
\tag{3}
$$

$$
h_C^{-1}(\eta)=\mu_0+s h^{-1}(\eta),
\qquad
h_C'(x)=\frac{h'(z)}{s}.
\tag{4}
$$

The inner link's domain applies to $z$, not directly to $x$. For example, a centered `LogLink` requires $(x-\mu_0)/s>0$.

<!-- snippet: links-centered-log -->
```cs
private static (double LinkValue, double RoundTrip) TransformPositiveScale()
{
    ILinkFunction positiveLink = new LogLink();
    var centered = new CenteredLink(positiveLink, mu0: 0.0, scale: 100.0);

    const double naturalScale = 250.0;
    double linkValue = centered.Link(naturalScale);
    double roundTrip = centered.InverseLink(linkValue);

    return (linkValue, roundTrip);
}
```

Here `linkValue` is $\log(2.5)$ and `roundTrip` returns 250 to floating-point precision. Centering constants and scales are part of the scientific parameterization and must be serialized and reported.

## Sinh-arcsinh link

`ASinHLink` adapts the sinh-arcsinh transformation of Jones and Pewsey [2](#ref-2) as a monotone link. Let

$$
z=\frac{x-\gamma_0}{s},\quad s>0,\quad\delta>0.
\tag{5}
$$

Then

$$
h_A(x)=\sinh\{\delta\operatorname{asinh}(z)-\epsilon\},
\tag{6}
$$

$$
h_A^{-1}(\eta)=\gamma_0+s\sinh\left[
\frac{\operatorname{asinh}(\eta)+\epsilon}{\delta}\right],
\tag{7}
$$

and

$$
h_A'(x)=\frac{\delta}{s}
\frac{\cosh\{\delta\operatorname{asinh}(z)-\epsilon\}}
{\sqrt{1+z^2}}.
\tag{8}
$$

The implementation floors $s$ and $\delta$ at $10^{-12}$. With `UseAdaptiveEpsilon`, it replaces the fixed skew parameter by

$$
\epsilon_{\mathrm{eff}}=epsilon_{\max}
\tanh(k_\epsilon I),
\tag{9}
$$

where $I$ is `ParentIndicator` and $k_\epsilon$ is `EpsilonSlope`. This is a deterministic coupling to the supplied parent indicator; it is not an independently fitted hierarchical relationship unless the consuming model estimates that indicator.

## SES link

`SESLink` defines its inverse link analytically and computes its forward link numerically. With curvature $a>0$ and asymmetry $\lambda$,

$$
x=\gamma(\eta)=\frac{\exp(\lambda\eta)\sinh(a\eta)}{a},
\tag{10}
$$

$$
\frac{d\gamma}{d\eta}=exp(\lambda\eta)
\left[\frac{\lambda}{a}\sinh(a\eta)+\cosh(a\eta)\right],
\qquad
h'(x)=\left.\left(\frac{d\gamma}{d\eta}\right)^{-1}\right|_{\eta=h(x)}.
\tag{11}
$$

`A` is floored at $10^{-12}$. Effective $\lambda$ is clamped to $[-0.999,0.999]$ to preserve global monotonicity. In adaptive mode,

$$
\lambda_{\mathrm{eff}}=\lambda_{\max}\tanh(k_\lambda I)
\tag{12}
$$

before the monotonicity clamp.

`Link(x)` solves Equation (10) by Newton iteration. Derivatives are floored at $10^{-16}$; non-finite updates are halved; steps with magnitude above 4 are damped to magnitude 4 after the convergence check. `MaxIterations` and `Tolerance` are configurable and serialized. The method returns its last iterate even if convergence fails, records `LastInverseConverged` and `LastInverseResidual`, and writes a debug diagnostic. Consumers must inspect convergence state when the transform is used in consequential calculations.

## Positive-scale SES and sinh-arcsinh links

`LogSESLink` applies the SES mapping to relative log scale. Define

$$
r(\eta)=\frac{\exp(\lambda\eta)\sinh(a\eta)}{a},
\qquad
\sigma(\eta)=\sigma_0\exp\{r(\eta)\}.
\tag{13}
$$

The forward link solves $r(\eta)=\log(\sigma/\sigma_0)$ with the same guarded Newton scheme, and

$$
\frac{d\eta}{d\sigma}=
\frac{1}{\sigma}\left(\frac{dr}{d\eta}\right)^{-1}.
\tag{14}
$$

`LogASinHLink` instead transforms the standardized log ratio

$$
z=\frac{\log(\sigma/\sigma_0)}{s},
\qquad
h_{LA}(\sigma)=\sinh\{\delta\operatorname{asinh}(z)-\epsilon\},
\tag{15}
$$

with inverse

$$
\sigma=\exp\left[
\log\sigma_0+s\sinh\left\{
\frac{\operatorname{asinh}(\eta)+\epsilon}{\delta}
\right\}\right]
\tag{16}
$$

and derivative

$$
\frac{d\eta}{d\sigma}=
\frac{\delta\cosh\{\delta\operatorname{asinh}(z)-\epsilon\}}
{s\sigma\sqrt{1+z^2}}.
\tag{17}
$$

Both positive-scale links floor non-positive inputs internally to an epsilon. That protects finite differences; it does not make a negative physical scale valid. Validate the parent model before interpreting a transformed value.

## Selection, identifiability, and derivatives

- Use identity only for an unconstrained parameter whose regression can remain in valid support.
- Use log for strictly positive parameters when multiplicative change is interpretable.
- Use logit/probit/cloglog for probabilities, but state which link because equal linear predictors imply different tail behavior.
- Use Fisher $z$ for correlations in $(-1,1)$; it does not guarantee that a matrix assembled from several correlations is positive definite.
- Flexible ASinH/SES links add tuning constants and can trade off with regression coefficients. Fixing them without sensitivity analysis can understate structural uncertainty.
- Evaluate round-trip error $|h^{-1}(h(x))-x|$ across the full fitted and extrapolation range, and compare analytic `DLink` with finite differences away from boundaries.
- Large derivatives amplify uncertainty and optimization curvature. A finite inverse alone is not sufficient numerical validation.

## Serialization compatibility

`BestFitLinkFunctionFactory.CreateFromXElement` recognizes the five BestFit-specific link classes and delegates standard types to the Numerics factory. Legacy `YeoJohnsonLink` XML lacking a valid `Lambda` is restored as `IdentityLink` because the removed legacy implementation defaulted to identity behavior. The fallback writes a debug message; it does not throw. Peer-review archives should migrate and resave such projects so the effective transformation is explicit.

## Implementation and evidence

| Concern | Source of truth | Evidence |
|---|---|---|
| Standard links | `C:/GIT/Numerics/Numerics/Functions/Link Functions/` at the pinned commit | Numerics link-function tests |
| ASinH, centered, SES variants | `src/RMC.BestFit/Models/LinkFunctions/` | `src/RMC.BestFit.Tests/Models/LinkFunctions/` |
| XML compatibility | `BestFitLinkFunctionFactory.cs` | factory and serialization unit tests |
| Compile-checked example | `src/RMC.BestFit.Tests/Documentation/Examples/FoundationExamples.cs` | `TechnicalReferenceDocumentationTests` |

## References

<a id="ref-1"></a>[1] I.-K. Yeo and R. A. Johnson, "A new family of power transformations to improve normality or symmetry," *Biometrika*, vol. 87, no. 4, pp. 954-959, 2000. doi: 10.1093/biomet/87.4.954.

<a id="ref-2"></a>[2] M. C. Jones and A. Pewsey, "Sinh-arcsinh distributions," *Biometrika*, vol. 96, no. 4, pp. 761-780, 2009. doi: 10.1093/biomet/asp053.

[<- Trend functions](trend-functions.md) | [Technical reference index](../index.md) | [Parameterization crosswalk ->](../appendices/parameterization-crosswalk.md)
