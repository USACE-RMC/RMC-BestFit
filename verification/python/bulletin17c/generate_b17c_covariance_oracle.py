"""Generate frozen complete-data Bulletin 17C GMM covariance reference values.

The calculation is deliberately independent of RMC.BestFit and Numerics.  It uses
only Python's standard library, published central moments, the centered-moment
Jacobian, and the just-identified sandwich covariance D^-1 S D^-T / N.
"""

from __future__ import annotations

import hashlib
import json
import math
import platform
from pathlib import Path


def inverse(matrix: list[list[float]]) -> list[list[float]]:
    """Invert a small dense matrix with Gauss-Jordan elimination."""
    size = len(matrix)
    augmented = [
        list(row) + [1.0 if i == j else 0.0 for j in range(size)]
        for i, row in enumerate(matrix)
    ]
    for column in range(size):
        pivot = max(range(column, size), key=lambda row: abs(augmented[row][column]))
        if pivot != column:
            augmented[column], augmented[pivot] = augmented[pivot], augmented[column]
        divisor = augmented[column][column]
        if abs(divisor) <= 1.0e-30:
            raise ValueError("The analytical Jacobian is singular.")
        augmented[column] = [value / divisor for value in augmented[column]]
        for row in range(size):
            if row == column:
                continue
            multiplier = augmented[row][column]
            augmented[row] = [
                left - multiplier * right
                for left, right in zip(augmented[row], augmented[column])
            ]
    return [row[size:] for row in augmented]


def multiply(left: list[list[float]], right: list[list[float]]) -> list[list[float]]:
    """Multiply two dense matrices."""
    return [
        [sum(left[i][k] * right[k][j] for k in range(len(right))) for j in range(len(right[0]))]
        for i in range(len(left))
    ]


def transpose(matrix: list[list[float]]) -> list[list[float]]:
    """Transpose a dense matrix."""
    return [list(column) for column in zip(*matrix)]


def pearson_central_moments(sigma: float, skew: float) -> tuple[float, ...]:
    """Return central moments two through six for the Pearson III family."""
    sigma2 = sigma * sigma
    sigma3 = sigma2 * sigma
    sigma4 = sigma2 * sigma2
    sigma5 = sigma4 * sigma
    sigma6 = sigma4 * sigma2
    skew2 = skew * skew
    skew4 = skew2 * skew2
    return (
        sigma2,
        skew * sigma3,
        sigma4 * (3.0 + 1.5 * skew2),
        sigma5 * skew * (10.0 + 3.0 * skew2),
        sigma6 * (15.0 + 32.5 * skew2 + 7.5 * skew4),
    )


def normal_central_moments(sigma: float) -> tuple[float, ...]:
    """Return central moments two through six for a Normal distribution."""
    sigma2 = sigma * sigma
    sigma4 = sigma2 * sigma2
    sigma6 = sigma4 * sigma2
    return sigma2, 0.0, 3.0 * sigma4, 0.0, 15.0 * sigma6


def covariance(
    family: str,
    parameters: list[float],
    sample_size: int,
) -> list[list[float]]:
    """Evaluate the independently derived complete-data GMM sandwich covariance."""
    if family == "Exponential":
        _, alpha = parameters
        mu2, mu3, mu4, _, _ = pearson_central_moments(alpha, 2.0)
        jacobian = [[-1.0, -1.0], [0.0, -2.0 * alpha]]
        moment_covariance = [[mu2, mu3], [mu3, mu4 - mu2 * mu2]]
    elif family == "Gamma":
        theta, kappa = parameters
        sigma = theta * math.sqrt(kappa)
        skew = 2.0 / math.sqrt(kappa)
        mu2, mu3, mu4, _, _ = pearson_central_moments(sigma, skew)
        jacobian = [
            [-kappa, -theta],
            [-2.0 * theta * kappa, -(theta * theta)],
        ]
        moment_covariance = [[mu2, mu3], [mu3, mu4 - mu2 * mu2]]
    elif family in {"Normal", "LogNormal"}:
        _, sigma = parameters
        mu2, mu3, mu4, _, _ = normal_central_moments(sigma)
        jacobian = [[-1.0, 0.0], [0.0, -2.0 * sigma]]
        moment_covariance = [[mu2, mu3], [mu3, mu4 - mu2 * mu2]]
    elif family in {"PearsonTypeIII", "LogPearsonTypeIII"}:
        _, sigma, skew = parameters
        mu2, mu3, mu4, mu5, mu6 = pearson_central_moments(sigma, skew)
        bessel_ratio = sample_size / (sample_size - 2.0)
        jacobian = [
            [-1.0, 0.0, 0.0],
            [0.0, -2.0 * sigma, 0.0],
            [
                -3.0 * bessel_ratio * sigma * sigma,
                -3.0 * skew * sigma * sigma,
                -(sigma * sigma * sigma),
            ],
        ]
        moment_covariance = [
            [mu2, mu3, mu4],
            [mu3, mu4 - mu2 * mu2, mu5 - mu2 * mu3],
            [mu4, mu5 - mu2 * mu3, mu6 - mu3 * mu3],
        ]
    else:
        raise ValueError(f"Unsupported family: {family}")

    jacobian_inverse = inverse(jacobian)
    unscaled = multiply(multiply(jacobian_inverse, moment_covariance), transpose(jacobian_inverse))
    return [[value / sample_size for value in row] for row in unscaled]


def main() -> None:
    """Write the deterministic JSON artifact next to the verification data manifest."""
    script_path = Path(__file__).resolve()
    repository_root = script_path.parents[3]
    output_path = repository_root / "verification" / "data" / "bulletin17c" / "b17c-gmm-covariance-oracle.json"
    cases = [
        ("Exponential_N25", "Exponential", [10.0, 50.0], 25),
        ("Exponential_N100", "Exponential", [10.0, 50.0], 100),
        ("Gamma_N25", "Gamma", [5.0, 2.0], 25),
        ("Gamma_N100", "Gamma", [5.0, 2.0], 100),
        ("Normal_N25", "Normal", [100.0, 15.0], 25),
        ("Normal_N100", "Normal", [100.0, 15.0], 100),
        ("PearsonTypeIII_N25", "PearsonTypeIII", [100.0, 20.0, 0.5], 25),
        ("PearsonTypeIII_N100", "PearsonTypeIII", [100.0, 20.0, 0.5], 100),
        ("LogNormal_N25", "LogNormal", [3.0, 0.5], 25),
        ("LogNormal_N100", "LogNormal", [3.0, 0.5], 100),
        ("LogPearsonTypeIII_N25", "LogPearsonTypeIII", [3.0, 0.5, 0.2], 25),
        ("LogPearsonTypeIII_N100", "LogPearsonTypeIII", [3.0, 0.5, 0.2], 100),
        (
            "LogPearsonTypeIII_Bulletin17CExample1_N68",
            "LogPearsonTypeIII",
            [3.328623159, 0.140287994, 0.396626124],
            68,
        ),
    ]
    artifact = {
        "metadata": {
            "generated": "2026-08-30",
            "generator": "verification/python/bulletin17c/generate_b17c_covariance_oracle.py",
            "generator_sha256": hashlib.sha256(script_path.read_bytes()).hexdigest(),
            "python": platform.python_version(),
            "dependencies": "Python standard library only",
            "sources": [
                {
                    "title": "Guidelines for Determining Flood Flow Frequency - Bulletin 17C, version 1.1",
                    "doi": "10.3133/tm4B5",
                    "url": "https://pubs.usgs.gov/publication/tm4B5",
                },
                {
                    "title": "Confidence intervals for expected moments algorithm flood quantile estimates",
                    "doi": "10.1029/2001WR900016",
                    "url": "https://pubs.usgs.gov/publication/70023515",
                },
            ],
            "formula": "Complete-data just-identified sandwich covariance D^-1 S D^-T / N from central moments mu2..mu6; three-coordinate D[2,0] uses -3*(N/(N-2))*sigma^2 for the B17C c3/c2 centered-moment derivative.",
            "parameterization": "Exponential (Xi,Alpha); Gamma (Theta scale,Kappa shape); Normal (Mu,Sigma); Pearson III (Mu,Sigma,Gamma); log families use the same coordinates in base-10 log space.",
            "runtime_production_dependencies": "None; the generator imports no RMC.BestFit or Numerics code.",
        },
        "cases": [
            {
                "id": identifier,
                "family": family,
                "parameters": parameters,
                "sampleSize": sample_size,
                "covariance": covariance(family, parameters, sample_size),
            }
            for identifier, family, parameters, sample_size in cases
        ],
    }
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(artifact, indent=2) + "\n", encoding="utf-8", newline="\n")


if __name__ == "__main__":
    main()
