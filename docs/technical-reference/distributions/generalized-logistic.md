<!-- technical-reference-status: complete -->

# Generalized Logistic Distribution

[Distribution index](index.md) · [Logistic limit](logistic.md) · [Parameterization crosswalk](../appendices/parameterization-crosswalk.md)

## Purpose and Parameterization

The generalized logistic (GLO) family adds one-sided skewness to the logistic location-scale model and is widely used with L-moment regional frequency methods. RMC.Numerics uses $(\xi,\alpha,\kappa)$ with $\alpha>0$. Location and scale have observation units and shape is dimensionless. Its shape transform and sign convention match the RMC.Numerics GEV, not necessarily another package's GLO convention.

Let $z=(x-\xi)/\alpha$ and define $y$ by (GEV.1) in the [GEV chapter](generalized-extreme-value.md). Support requires $1-\kappa z>0$: $\kappa<0$ has a finite lower endpoint and heavy unbounded upper tail; $\kappa>0$ has a finite upper endpoint; $\kappa=0$ has all-real support.

## Distribution

On the support,

$$
F(x)=\frac{1}{1+e^{-y}},\qquad
f(x)=\frac{e^{-(1-\kappa)y}}{\alpha(1+e^{-y})^2}. \tag{GLO.1}
$$

The survival is $1-F$, and

$$
Q(p)=\begin{cases}
\xi+\dfrac{\alpha}{\kappa}\left[1-\left(\dfrac{1-p}{p}\right)^\kappa\right],&\kappa\ne0,\\
\xi+\alpha\log\dfrac{p}{1-p},&\kappa=0.
\end{cases} \tag{GLO.2}
$$

The zero-shape limit is Logistic. In the current Numerics properties, the mean is reported for $|\kappa|<1$, standard deviation for $|\kappa|<1/2$, and higher standardized moments under correspondingly stricter existence conditions. Nonzero shape produces a polynomial tail on the unbounded side, so rare-quantile behavior can differ sharply from the ordinary Logistic.

## Likelihood and Inference

For exact observations the log density contribution is

$$
\log f(x_i)=-\log\alpha-(1-\kappa)y_i
-2\log(1+e^{-y_i}). \tag{GLO.3}
$$

The complete RMC.BestFit likelihood combines this factor with CDF, survival, interval, uncertain-observation, low-outlier, and perception-threshold terms. Under parameter trends, the support is checked at every index. Priors—including quantile priors evaluated through (GLO.2)—multiply that data likelihood. Default starts, bounds, and uniform working priors are sample-dependent outputs of the Numerics constraint routine.

Shape and scale may be highly correlated. Near-zero shapes require stable limiting evaluation; Numerics uses a `NearZero` branch. A finite fitted upper endpoint is a model assertion, while a negative shape implies a heavy unbounded upper tail in this implementation. Either conclusion requires engineering scrutiny when extrapolated beyond the record.

## Compile-Checked API Example

<!-- snippet: distribution-generalized-logistic -->
```csharp
private static (double Density, double Cdf, double Quantile) EvaluateGeneralizedLogistic()
{
    var distribution = new global::Numerics.Distributions.GeneralizedLogistic(
        1500.0, 400.0, -0.10);
    return Evaluate(distribution, value: 2200.0, nonExceedanceProbability: 0.99);
}
```

Here $\kappa=-0.10$ means an unbounded heavy upper tail under the Numerics sign convention.

## Validation and Limitations

Release evidence should test $F(Q(p))=p$, normalization, endpoint limits, the $\kappa\to0$ Logistic limit, and moment existence. Independent parity checks must first reconcile parameter order and shape sign. The compiled example supplies API evidence only. A good central fit does not validate rare-tail behavior, and L-moment popularity is not evidence that a GLO is appropriate for every flood population.

## References

<a id="ref-1"></a>[1] J. R. M. Hosking and J. R. Wallis, *Regional Frequency Analysis: An Approach Based on L-Moments*. Cambridge University Press, 1997.
