<!-- technical-reference-status: complete -->

# Trend and Link Functions

[Back to Technical Reference](../index.md)

This stable landing page preserves the original documentation URL. The audited material is split because trend models and link functions have different probability roles:

- [Trend functions](trend-functions.md) gives every implemented equation, coefficient order, centering rule, numerical safeguard, nonstationary likelihood mapping, and extrapolation limitation.
- [Link functions](link-functions.md) gives forward and inverse maps, domains, derivatives, BestFit-specific links, Newton inversion, adaptive behavior, and serialization fallback.

A trend maps an index or covariate row to a distribution parameter. A link maps between natural and working coordinates. Neither operation automatically contributes a probability-density Jacobian; a Jacobian is required only when a caller defines a density under a change of random-variable coordinates.

The [parameterization crosswalk](../appendices/parameterization-crosswalk.md) records how trend coefficients, natural parameters, Numerics distribution parameters, and displayed results correspond.

---

[Back to Technical Reference](../index.md)
