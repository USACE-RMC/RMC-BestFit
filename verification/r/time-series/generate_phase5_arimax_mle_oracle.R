# Generate the independent Phase 5 ARIMAX(1,1,0) conditional MLE/MAP oracle.
#
# The likelihood is implemented directly from the approved date-indexed,
# level-covariate recurrence. The script reads the committed fixture but does
# not call Numerics or RMC.BestFit.

script_argument <- grep("^--file=", commandArgs(trailingOnly = FALSE), value = TRUE)
script_path <- normalizePath(sub("^--file=", "", script_argument[[1L]]), winslash = "/", mustWork = TRUE)
repository_root <- normalizePath(
  file.path(dirname(script_path), "..", "..", ".."),
  winslash = "/",
  mustWork = TRUE
)
verification_r_root <- file.path(repository_root, "verification", "r")
project_library <- file.path(
  verification_r_root,
  "renv",
  "library",
  "windows",
  paste0("R-", R.version$major, ".", strsplit(R.version$minor, "\\.")[[1L]][[1L]]),
  R.version$platform
)
.libPaths(c(project_library, .libPaths()))

fixture_path <- file.path(
  repository_root,
  "verification",
  "data",
  "time-series",
  "phase5-recovery-fixtures.json"
)
output_path <- file.path(
  repository_root,
  "verification",
  "data",
  "time-series",
  "phase5-arimax-mle-recovery-oracle.json"
)

git_revision <- function(path) {
  revision <- tryCatch(
    system2("git", c("-C", shQuote(path), "rev-parse", "HEAD"), stdout = TRUE),
    error = function(...) character()
  )
  if (length(revision) == 0L) NA_character_ else revision[[1L]]
}

arimax_fixture <- jsonlite::fromJSON(fixture_path)$arimax
raw <- arimax_fixture$raw
differences <- diff(raw)

# Differenced model index k maps to raw response index k+d. With d=1, the
# first difference is aligned to the second raw response and therefore to the
# second level-covariate observation. The first difference conditions the AR
# recurrence and is excluded from the conditional likelihood.
level_covariate <- arimax_fixture$covariate[-1L]
stopifnot(length(level_covariate) == length(differences))
conditional_count <- length(differences) - 1L

conditional_residuals <- function(intercept, beta, phi) {
  conditional_mean <- intercept + beta * level_covariate
  fitted <- conditional_mean[-1L] +
    phi * (differences[-length(differences)] - conditional_mean[-length(conditional_mean)])
  differences[-1L] - fitted
}

profile_negative_log_likelihood <- function(coefficients) {
  epsilon <- conditional_residuals(coefficients[[1L]], coefficients[[2L]], coefficients[[3L]])
  length(epsilon) / 2.0 * log(mean(epsilon^2))
}

training_mean <- mean(differences)
intercept_minimum <- 10^(floor(log10(abs(training_mean))) - 1.0)
intercept_maximum <- 10^(ceiling(log10(abs(training_mean))) + 1.0)
coefficient_lower <- c(intercept_minimum, -10.0, -2.0)
coefficient_upper <- c(intercept_maximum, 10.0, 2.0)
default_start <- c(training_mean, 0.0, 0.0)
truth_start <- c(arimax_fixture$intercept, arimax_fixture$beta, arimax_fixture$phi)

fit_from_default <- stats::optim(
  default_start,
  profile_negative_log_likelihood,
  method = "L-BFGS-B",
  lower = coefficient_lower,
  upper = coefficient_upper,
  control = list(factr = 1.0, pgtol = 1e-12, maxit = 100000L)
)
fit_from_truth <- stats::optim(
  truth_start,
  profile_negative_log_likelihood,
  method = "L-BFGS-B",
  lower = coefficient_lower,
  upper = coefficient_upper,
  control = list(factr = 1.0, pgtol = 1e-12, maxit = 100000L)
)
stopifnot(fit_from_default$convergence == 0L)
stopifnot(fit_from_truth$convergence == 0L)
stopifnot(max(abs(fit_from_default$par - fit_from_truth$par)) < 1e-6)

intercept_hat <- unname(fit_from_default$par[[1L]])
beta_hat <- unname(fit_from_default$par[[2L]])
phi_hat <- unname(fit_from_default$par[[3L]])
fitted_residuals <- conditional_residuals(intercept_hat, beta_hat, phi_hat)
sigma_hat <- sqrt(mean(fitted_residuals^2))
gaussian_log_likelihood <- sum(stats::dnorm(fitted_residuals, 0.0, sigma_hat, log = TRUE))

# Uniform parameter priors are constant within the model bounds. Jeffreys'
# scale prior changes only the conditional posterior mode for sigma.
posterior_sigma_hat <- sqrt(sum(fitted_residuals^2) / (length(fitted_residuals) + 1.0))
posterior_data_log_likelihood <- sum(
  stats::dnorm(fitted_residuals, 0.0, posterior_sigma_hat, log = TRUE)
)
sigma_upper <- 10^(ceiling(log10(stats::sd(differences))) + 1.0)
posterior_prior_log_likelihood <-
  -log(intercept_maximum - intercept_minimum) -
  log(20.0) -
  log(4.0) -
  log(sigma_upper - .Machine$double.eps) -
  log(posterior_sigma_hat)
posterior_log_likelihood <- posterior_data_log_likelihood + posterior_prior_log_likelihood

truth <- c(arimax_fixture$intercept, arimax_fixture$beta, arimax_fixture$phi)
truth_residuals <- conditional_residuals(truth[[1L]], truth[[2L]], truth[[3L]])
truth_data_log_likelihood <- sum(
  stats::dnorm(truth_residuals, 0.0, arimax_fixture$sigma, log = TRUE)
)

artifact <- list(
  metadata = list(
    artifact_id = "TS-PHASE5-ARIMAX-MLE-001",
    finding = "PHASE5-RECOVERY-ARIMAX-MLE",
    generator = "verification/r/time-series/generate_phase5_arimax_mle_oracle.R",
    generator_sha256 = unname(digest::digest(file = script_path, algo = "sha256")),
    input_fixture = "verification/data/time-series/phase5-recovery-fixtures.json",
    input_fixture_sha256 = unname(digest::digest(file = fixture_path, algo = "sha256")),
    source_commit = git_revision(repository_root),
    r_version = R.version.string,
    jsonlite_version = as.character(utils::packageVersion("jsonlite")),
    digest_version = as.character(utils::packageVersion("digest")),
    generated_utc = format(Sys.time(), tz = "UTC", usetz = TRUE),
    tolerances = list(
      optimizer_coefficient_absolute = 1e-3,
      optimizer_scale_absolute = 1e-5,
      log_likelihood_absolute = 1e-5,
      sampled_map_relative = 0.05,
      sampled_map_absolute_floor = 1e-3,
      posterior_log_likelihood_absolute = 1e-5,
      deterministic_recurrence_absolute = 1e-12
    )
  ),
  fixture = list(
    seed = arimax_fixture$seed,
    burn_in = arimax_fixture$burn_in,
    raw_sample_size = length(raw),
    difference_count = length(differences),
    conditional_likelihood_count = conditional_count,
    transform = arimax_fixture$transform,
    differencing_order = arimax_fixture$differencing_order,
    ar_order = arimax_fixture$ar_order,
    ma_order = arimax_fixture$ma_order,
    covariate_lag_order = arimax_fixture$covariate_lag_order,
    include_intercept = arimax_fixture$include_intercept,
    truth = list(
      intercept = arimax_fixture$intercept,
      beta = arimax_fixture$beta,
      phi = arimax_fixture$phi,
      sigma = arimax_fixture$sigma
    )
  ),
  default_parameter_state = list(
    intercept = training_mean,
    beta = 0.0,
    phi = 0.0,
    sigma = stats::sd(differences),
    lower = unname(c(coefficient_lower, .Machine$double.eps)),
    upper = unname(c(coefficient_upper, sigma_upper))
  ),
  conditional_mle = list(
    intercept = intercept_hat,
    beta = beta_hat,
    phi = phi_hat,
    sigma = sigma_hat,
    data_log_likelihood = unname(gaussian_log_likelihood),
    start_default_function_evaluations = unname(fit_from_default$counts[[1L]]),
    start_default_convergence = unname(fit_from_default$convergence),
    start_truth_convergence = unname(fit_from_truth$convergence)
  ),
  conditional_posterior_map = list(
    intercept = intercept_hat,
    beta = beta_hat,
    phi = phi_hat,
    sigma = unname(posterior_sigma_hat),
    data_log_likelihood = unname(posterior_data_log_likelihood),
    prior_log_likelihood = unname(posterior_prior_log_likelihood),
    posterior_log_likelihood = unname(posterior_log_likelihood),
    intercept_prior = sprintf("Uniform(%s,%s)", intercept_minimum, intercept_maximum),
    covariate_prior = "Uniform(-10,10)",
    ar_prior = "Uniform(-2,2)",
    scale_prior = sprintf("Uniform(.Machine$double.eps,%s)", sigma_upper),
    jeffreys_scale_prior = TRUE
  ),
  same_point_truth = list(
    intercept = arimax_fixture$intercept,
    beta = arimax_fixture$beta,
    phi = arimax_fixture$phi,
    sigma = arimax_fixture$sigma,
    data_log_likelihood = unname(truth_data_log_likelihood)
  )
)

dir.create(dirname(output_path), recursive = TRUE, showWarnings = FALSE)
jsonlite::write_json(
  artifact,
  output_path,
  auto_unbox = TRUE,
  pretty = TRUE,
  digits = NA
)

message(sprintf("Wrote %s", output_path))
