"""Check the captured BFGS recovery gradients and compare the unchanged GMM loop.

Requires NumPy 2.3.5. Run with --check to verify results.json without writing files.
The preceding evidence package supplies the identical bootstrap fixtures and the
independently computed outer fixed points; this script never invokes production code.
"""

from pathlib import Path
import collections
import gzip
import json
import statistics
import sys

import numpy as np

ROOT = Path(__file__).parent
PREVIOUS = ROOT.parent / "b17c-cholesky-evidence-20260917"


def read_rows(path):
    """Read compressed, newline-delimited diagnostic records."""
    return [json.loads(line) for line in gzip.decompress(path.read_bytes()).splitlines()]


def terms(x, fixture):
    """Compute systematic Pearson III moments, derivatives, and covariance directly."""
    mu, sigma, skew = x
    values = np.array(fixture["values"])
    n = len(values)
    c2, c3 = n / (n - 1), n * n / ((n - 1) * (n - 2))
    delta = values - mu
    d1, d2, d3 = np.mean(delta), np.mean(delta**2), np.mean(delta**3)
    moment = np.array([d1, c2 * d2 - sigma**2, c3 * d3 - skew * sigma**3])
    jacobian = np.array([
        [-1, 0, 0], [-2 * c2 * d1, -2 * sigma, 0],
        [-3 * c3 * d2, -3 * skew * sigma**2, -sigma**3],
    ])
    m2, m3 = sigma**2, skew * sigma**3
    m4 = sigma**4 * (3 + 1.5 * skew**2)
    m5 = sigma**5 * skew * (10 + 3 * skew**2)
    m6 = sigma**6 * (15 + 32.5 * skew**2 + 7.5 * skew**4)
    covariance = np.array([
        [m2, m3, m4], [m3, m4 - m2**2, m5 - m2 * m3],
        [m4, m5 - m2 * m3, m6 - m3**2],
    ]) - np.outer(moment, moment)
    return moment, jacobian, covariance


def gradient(x, weight, fixture):
    """Evaluate the fixed-weight gradient including the real-scale skew penalty."""
    moment, jacobian, _ = terms(x, fixture)
    weight = np.array(weight)
    penalty = np.array([0, 0, (x[2] - fixture["center"]) /
                        (len(fixture["values"]) * fixture["mse"])])
    return jacobian.T @ ((weight + weight.T) * 0.5) @ moment + penalty


before_rows = read_rows(PREVIOUS / "diagnostic-inputs.jsonl.gz")
after_rows = read_rows(ROOT / "trace-extract.jsonl.gz")
fixtures = {e["context"]: e for e in before_rows if e["kind"] == "fixture"}
oracles = json.loads((PREVIOUS / "outer-diagnosis.json").read_text())["cases"]
fits = {}
result = {}
for label, rows in [("before", before_rows), ("after", after_rows)]:
    fits[label] = {e["context"]: e for e in rows if e["kind"] == "fit-end"}
    boot = [e for context, e in fits[label].items() if context != "parent"]
    result[label] = dict(
        parent=fits[label]["parent"], bootstrapFits=len(boot),
        evaluations=sum(e["evaluations"] for e in boot),
        fallbacks=sum(e["fallbacks"] for e in boot),
        converged=sum(e["converged"] for e in boot),
        at100=sum(e["passes"] == 100 for e in boot),
        medianPasses=statistics.median(e["passes"] for e in boot),
        atMostFive=sum(e["passes"] <= 5 for e in boot),
        passHistogram=dict(sorted(collections.Counter(e["passes"] for e in boot).items())),
    )

recoveries = [e for e in after_rows if e["kind"] == "roundoff-convergence"]
gradient_errors, gradient_norms = [], []
for event in recoveries:
    independent = gradient(event["trial"], event["weight"], fixtures[event["context"]])
    norm = float(max(abs(independent)))
    assert norm <= event["AbsoluteTolerance"], event
    assert abs(event["value"] - event["initialValue"]) <= event["roundoff"], event
    gradient_norms.append(norm)
    gradient_errors.append(float(max(abs(independent - event["gradient"]))))
result["recoveryChecks"] = dict(
    count=len(recoveries), maximumIndependentGradient=max(gradient_norms),
    maximumGradientDiscrepancy=max(gradient_errors), allWithinExistingTolerance=True,
)

new_caps = [c for c, e in fits["after"].items()
            if e["passes"] == 100 and fits["before"][c]["passes"] < 100]
result["additionalCappedFits"] = {}
for context in new_caps:
    fixture = fixtures[context]
    case = dict(independentRoot=oracles[context]["theta"],
                spectralRadius=oracles[context]["spectralRadius"])
    for label, rows in [("before", before_rows), ("after", after_rows)]:
        tail = [e for e in rows if e["kind"] == "pass-end" and e["context"] == context][-2:]
        previous, current = (np.array(e["values"]) for e in tail)
        weight = np.linalg.inv(terms(previous, fixture)[2])
        case[label] = dict(
            fit=fits[label][context],
            lastInnerGradientNorm=float(max(abs(gradient(current, weight, fixture)))),
            lastOuterStep=float(max(abs(current - previous))),
            distanceToIndependentRoot=float(max(abs(current - oracles[context]["theta"]))),
        )
    result["additionalCappedFits"][context] = case

common = [c for c in fits["before"] if c != "parent" and
          fits["before"][c]["converged"] and fits["after"][c]["converged"]]
changes = []
for context in common:
    old = np.array(fits["before"][context]["values"])
    new = np.array(fits["after"][context]["values"])
    reference = np.array(oracles[context]["theta"])
    changes.append(dict(context=context, change=float(max(abs(old - new))),
                        beforeError=float(max(abs(old - reference))),
                        afterError=float(max(abs(new - reference)))))
changed = [e for e in changes if e["change"] > 0]
result["commonConverged"] = dict(
    count=len(common), unchanged=len(common) - len(changed), changed=len(changed),
    closerToIndependentRoot=sum(e["afterError"] < e["beforeError"] for e in changed),
    maximumBeforeError=max(e["beforeError"] for e in changes),
    maximumAfterError=max(e["afterError"] for e in changes),
    largestChanges=sorted(changes, key=lambda e: -e["change"])[:10],
)

# Normalize JSON's string representation of histogram keys before comparison.
result = json.loads(json.dumps(result))
if "--check" in sys.argv:
    assert result == json.loads((ROOT / "results.json").read_text())
    print("All saved comparison and independent-gradient results reproduced exactly.")
else:
    (ROOT / "results.json").write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps(result["recoveryChecks"], indent=2))
