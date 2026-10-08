"""Generate frozen stationary Poisson-GPA values without RMC.BestFit or Numerics.

SciPy uses Coles generalized-Pareto shape ``c = xi`` and the opposite
``genextreme`` shape ``c = -xi``.  RMC.BestFit/Numerics stores Hosking
``Kappa = -xi``.  The output is intentionally limited to stationary claims
supported by SciPy's univariate distributions.
"""

from __future__ import annotations

import json
import platform
from pathlib import Path

import numpy as np
import scipy
from scipy.stats import genextreme, genpareto, poisson


OUTPUT = (
    Path(__file__).resolve().parents[2]
    / "data"
    / "point-process"
    / "stationary-poisson-gpa-scipy-oracle.json"
)


def main() -> None:
    """Evaluate and freeze the predeclared stationary compatibility cell."""
    location = 100.0
    scale = 20.0
    coles_shape = 0.1
    numerics_kappa = -coles_shape
    threshold = 80.0
    exposure_years = 1.25
    exact_values = np.asarray([85.0, 100.0, 120.0], dtype=float)
    tail_value = 120.0
    annual_maximum_probability = 0.99

    threshold_support = 1.0 + coles_shape * (threshold - location) / scale
    threshold_intensity = threshold_support ** (-1.0 / coles_shape)
    gpa_scale = scale + coles_shape * (threshold - location)
    excesses = exact_values - threshold
    conditional_log_densities = genpareto.logpdf(
        excesses,
        c=coles_shape,
        loc=0.0,
        scale=gpa_scale,
    )
    point_process_log_likelihood = (
        -exposure_years * threshold_intensity
        + exact_values.size * np.log(threshold_intensity)
        + np.sum(conditional_log_densities)
    )

    output = {
        "metadata": {
            "generator": "verification/python/point-process/generate_point_process_scipy_oracle.py",
            "python": platform.python_version(),
            "scipy": scipy.__version__,
            "numpy": np.__version__,
            "scope": "stationary Poisson count plus conditional generalized-Pareto excesses",
            "seasonalCompatibility": False,
        },
        "crosswalk": {
            "thresholdConvention": "exact values strictly above u; excess y=x-u",
            "exposureUnit": "years",
            "intensityUnit": "events per year",
            "rmcBestFitParameters": ["Mu", "Sigma", "Kappa"],
            "scipyGenparetoParameters": ["c=xi", "loc=0", "scale=sigma_u"],
            "scipyGenextremeShape": "c=-xi",
            "shapeIdentity": "Numerics Kappa = -Coles xi",
        },
        "inputs": {
            "location": location,
            "scale": scale,
            "colesShape": coles_shape,
            "numericsKappa": numerics_kappa,
            "threshold": threshold,
            "exposureYears": exposure_years,
            "exactValues": exact_values.tolist(),
            "tailValue": tail_value,
            "annualMaximumProbability": annual_maximum_probability,
        },
        "expected": {
            "thresholdIntensity": float(threshold_intensity),
            "gpaScaleAtThreshold": float(gpa_scale),
            "pointProcessLogLikelihood": float(point_process_log_likelihood),
            "conditionalSurvivalAtTailValue": float(
                genpareto.sf(
                    tail_value - threshold,
                    c=coles_shape,
                    loc=0.0,
                    scale=gpa_scale,
                )
            ),
            "annualMaximumQuantile": float(
                genextreme.ppf(
                    annual_maximum_probability,
                    c=-coles_shape,
                    loc=location,
                    scale=scale,
                )
            ),
            "eventCountProbability": float(
                poisson.pmf(exact_values.size, exposure_years * threshold_intensity)
            ),
        },
    }

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(output, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
