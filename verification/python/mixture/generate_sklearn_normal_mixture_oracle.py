"""Generate the frozen scikit-learn ordinary-Normal-mixture oracle."""

from __future__ import annotations

import argparse
import json
import math
import platform
from pathlib import Path

import numpy as np
import scipy
from scipy.special import ndtr
import sklearn
from sklearn.mixture import GaussianMixture


SAMPLE_SEED = 12345
FIT_SEED = 22345
SAMPLE_SIZE = 1000
WEIGHTS = np.array([0.3, 0.7], dtype=float)
MEANS = np.array([0.0, 3.0], dtype=float)
STANDARD_DEVIATIONS = np.array([1.0, 0.1], dtype=float)
CDF_X = np.array([-2.0, 0.0, 1.5, 2.9, 3.0, 3.1, 4.0], dtype=float)


def mixture_log_likelihood(
    sample: np.ndarray,
    weights: np.ndarray,
    means: np.ndarray,
    standard_deviations: np.ndarray,
) -> float:
    """Return the independently evaluated Normal-mixture log likelihood."""
    standardized = (sample[:, None] - means[None, :]) / standard_deviations[None, :]
    densities = (
        weights[None, :]
        * np.exp(-0.5 * standardized * standardized)
        / (standard_deviations[None, :] * math.sqrt(2.0 * math.pi))
    )
    return float(np.log(np.sum(densities, axis=1)).sum())


def mixture_cdf(
    x: np.ndarray,
    weights: np.ndarray,
    means: np.ndarray,
    standard_deviations: np.ndarray,
) -> np.ndarray:
    """Return the independently evaluated Normal-mixture CDF ordinates."""
    standardized = (x[:, None] - means[None, :]) / standard_deviations[None, :]
    return np.sum(weights[None, :] * ndtr(standardized), axis=1)


def generate() -> dict[str, object]:
    """Generate the deterministic sample, fit, likelihoods, and response ordinates."""
    rng = np.random.default_rng(SAMPLE_SEED)
    labels = rng.choice(2, size=SAMPLE_SIZE, p=WEIGHTS)
    sample = rng.normal(MEANS[labels], STANDARD_DEVIATIONS[labels])

    fit = GaussianMixture(
        n_components=2,
        covariance_type="diag",
        tol=1.0e-12,
        reg_covar=1.0e-12,
        max_iter=1000,
        n_init=20,
        random_state=FIT_SEED,
        init_params="kmeans",
    )
    fit.fit(sample.reshape(-1, 1))
    order = np.argsort(fit.means_[:, 0])
    fit_weights = fit.weights_[order]
    fit_means = fit.means_[order, 0]
    fit_standard_deviations = np.sqrt(fit.covariances_[order, 0])

    return {
        "schemaVersion": 1,
        "tool": {
            "python": platform.python_version(),
            "numpy": np.__version__,
            "scipy": scipy.__version__,
            "scikitLearn": sklearn.__version__,
        },
        "parameterization": {
            "sampleUnit": "scalar ordinary Normal-mixture observation",
            "sampleSize": SAMPLE_SIZE,
            "sampleSeed": SAMPLE_SEED,
            "rng": "numpy.random.Generator(PCG64)",
            "fitSeed": FIT_SEED,
            "weightConvention": "full-K physical weights summing to one",
            "scaleConvention": "Normal standard deviation; scikit-learn diagonal covariance converted by square root",
            "labelRule": "ascending component mean",
            "covarianceType": "diag",
        },
        "sample": sample.tolist(),
        "parent": {
            "weights": WEIGHTS.tolist(),
            "means": MEANS.tolist(),
            "standardDeviations": STANDARD_DEVIATIONS.tolist(),
            "logLikelihood": mixture_log_likelihood(sample, WEIGHTS, MEANS, STANDARD_DEVIATIONS),
            "cdfX": CDF_X.tolist(),
            "cdf": mixture_cdf(CDF_X, WEIGHTS, MEANS, STANDARD_DEVIATIONS).tolist(),
        },
        "scikitLearnFit": {
            "converged": bool(fit.converged_),
            "iterations": int(fit.n_iter_),
            "weights": fit_weights.tolist(),
            "means": fit_means.tolist(),
            "standardDeviations": fit_standard_deviations.tolist(),
            "logLikelihood": mixture_log_likelihood(
                sample, fit_weights, fit_means, fit_standard_deviations
            ),
            "cdf": mixture_cdf(
                CDF_X, fit_weights, fit_means, fit_standard_deviations
            ).tolist(),
        },
    }


def main() -> None:
    """Write the generated artifact as stable indented JSON."""
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--output",
        type=Path,
        default=Path(__file__).resolve().parents[2]
        / "data"
        / "mixture"
        / "normal-mixture-sklearn-oracle.json",
    )
    args = parser.parse_args()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(generate(), indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
