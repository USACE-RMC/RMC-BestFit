<!-- technical-reference-status: complete -->

# Technical Reference Documentation Contract

[Back to Documentation Index](../index.md)

This contract defines the minimum scientific and software evidence required before a chapter is classified as complete. It applies to the public model library and to the RMC.Numerics 2.1.4 algorithms on which that library depends.

## Required Chapter Structure

Every statistical model chapter must state the engineering purpose, notation, parameterization, support, data-generating model, likelihood, prior and posterior when applicable, estimation method, numerical implementation, identifiability, assumptions, limitations, validation evidence, and public API workflow. Analysis chapters must additionally explain orchestration, uncertainty propagation, outputs, validation behavior, cancellation, and serialization.

A chapter may combine short sections when a topic does not apply, but it must say why the topic is inapplicable. Silence is not evidence of inapplicability.

## Source Hierarchy

Documentation is reconciled in the following order:

1. Production source defines implemented behavior and public API shape.
2. Unit tests define guarded programmatic behavior.
3. Verification tests and reports provide numerical or external-reference evidence.
4. Primary standards, journal papers, and scholarly books establish theoretical context.

When these sources disagree, the chapter must not silently select the most convenient account. The discrepancy is recorded in the review findings, the implemented behavior is stated accurately, and any production correction is handled as a separately authorized change.

## Mathematical Conventions

- Inline mathematics uses `$...$`; displayed mathematics uses `$$...$$`.
- Important equations receive a stable `\tag{chapter.number}` and are referenced by that number.
- Every symbol is defined at first use and added to the global notation appendix when it recurs across chapters.
- Parameterizations state support, units, natural-space constraints, link-space representation, and any sign or logarithm-base convention inherited from Numerics.
- A likelihood identifies the observational unit and all independence or dependence assumptions. Proportional expressions state which constants were omitted.
- Numerical approximations identify quadrature bounds, convergence criteria, clipping, fallbacks, or other behavior that can affect results.

## API Examples

All C# blocks on completed pages are copied from named source regions compiled by `RMC.BestFit.Tests`. The required form is:

The marker immediately precedes the C# fence; for example, a page uses a
`snippet: Unique.Snippet.Identifier` marker followed by the exact content of
the corresponding compiled source region.

Conceptual algorithms and incomplete fragments use `text` or `pseudocode`, never `cs` or `csharp`. Examples use small inline data, declare units, distinguish demonstration settings from defensible production settings, and avoid fabricated stochastic output.

## Evidence and Citations

- Scientific claims use primary literature, official standards, or authoritative scholarly texts.
- Page-local references use IEEE-style numbering and stable anchors such as `<a id="ref-1"></a>`.
- Validation claims identify the exact test, dataset, published table, or report section that supports them.
- Source links identify symbols rather than brittle line numbers.
- Future work is labeled explicitly and never described as implemented behavior.

## Completion States

| State | Meaning |
|---|---|
| Legacy | Existing material not yet audited against this contract |
| Draft | Rewritten formulation, still awaiting API or evidence reconciliation |
| Complete | Formulation, API, citations, links, and traceability pass automated and manual review |

Only complete pages carry the marker `technical-reference-status: complete`. Automated tests apply the strictest snippet and citation checks to those pages while the reference is upgraded incrementally.

## Review Standard

Completion means a technically qualified reviewer can determine exactly what model was fitted, how every observation contributes to the likelihood, what assumptions were made, how the software evaluates the model, how uncertainty is propagated, and what evidence supports the implementation.

---

[Back to Documentation Index](../index.md)
