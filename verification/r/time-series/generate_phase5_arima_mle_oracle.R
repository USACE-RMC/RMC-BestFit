# Generate the independent Phase 5 ARIMA(1,1,1) MLE recovery oracle.
#
# The conditional ARMA likelihood, optimizer, and profile-likelihood intervals
# are implemented directly in R. The script reads the committed raw fixture but
# does not call Numerics or RMC.BestFit.

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
  "phase5-arima-mle-recovery-oracle.json"
)

git_revision <- function(path) {
  revision <- tryCatch(
    system2("git", c("-C", shQuote(path), "rev-parse", "HEAD"), stdout = TRUE),
    error = function(...) character()
  )
  if (length(revision) == 0L) NA_character_ else revision[[1L]]
}

arima_fixture <- jsonlite::fromJSON(fixture_path)$arima
raw <- arima_fixture$raw
differences <- diff(log(raw))
conditional_count <- length(differences) - 1L
coefficient_bounds <- c(-0.99, 0.99)

conditional_residuals <- function(phi, theta) {
  epsilon <- numeric(length(differences))
  for (index in 2:length(differences)) {
    epsilon[[index]] <- differences[[index]] -
      phi * differences[[index - 1L]] -
      theta * epsilon[[index - 1L]]
  }
  epsilon[-1L]
}

profile_negative_log_likelihood <- function(coefficients) {
  epsilon <- conditional_residuals(coefficients[[1L]], coefficients[[2L]])
  sigma_squared <- mean(epsilon^2)
  length(epsilon) / 2.0 * log(sigma_squared)
}

fit_from_zero <- stats::optim(
  c(0.0, 0.0),
  profile_negative_log_likelihood,
  method = "Nelder-Mead",
  control = list(reltol = 1e-14, maxit = 100000L),
  hessian = TRUE
)
fit_from_truth <- stats::optim(
  c(arima_fixture$phi, arima_fixture$theta),
  profile_negative_log_likelihood,
  method = "Nelder-Mead",
  control = list(reltol = 1e-14, maxit = 100000L)
)
stopifnot(fit_from_zero$convergence == 0L)
stopifnot(fit_from_truth$convergence == 0L)
stopifnot(max(abs(fit_from_zero$par - fit_from_truth$par)) < 1e-6)

phi_hat <- unname(fit_from_zero$par[[1L]])
theta_hat <- unname(fit_from_zero$par[[2L]])
fitted_residuals <- conditional_residuals(phi_hat, theta_hat)
sigma_hat <- sqrt(mean(fitted_residuals^2))
gaussian_log_likelihood <- sum(stats::dnorm(fitted_residuals, 0.0, sigma_hat, log = TRUE))

# ARIMA evaluates the logarithmic-transform Jacobian on raw indices
# d + max(p,q) through T - 1. R is one-based, so this is raw[3:T].
log_jacobian <- -sum(log(raw[3:length(raw)]))
data_log_likelihood <- gaussian_log_likelihood + log_jacobian

profile_cutoff_delta <- stats::qchisq(0.95, df = 1L) / 2.0
profile_cutoff <- fit_from_zero$value + profile_cutoff_delta

theta_profile <- function(theta) {
  stats::optimize(
    function(phi) profile_negative_log_likelihood(c(phi, theta)),
    interval = coefficient_bounds,
    tol = 1e-12
  )$objective
}
phi_profile <- function(phi) {
  stats::optimize(
    function(theta) profile_negative_log_likelihood(c(phi, theta)),
    interval = coefficient_bounds,
    tol = 1e-12
  )$objective
}

phi_interval <- c(
  stats::uniroot(
    function(phi) phi_profile(phi) - profile_cutoff,
    c(coefficient_bounds[[1L]], phi_hat),
    tol = 1e-12
  )$root,
  stats::uniroot(
    function(phi) phi_profile(phi) - profile_cutoff,
    c(phi_hat, coefficient_bounds[[2L]]),
    tol = 1e-12
  )$root
)
theta_interval <- c(
  stats::uniroot(
    function(theta) theta_profile(theta) - profile_cutoff,
    c(coefficient_bounds[[1L]], theta_hat),
    tol = 1e-12
  )$root,
  stats::uniroot(
    function(theta) theta_profile(theta) - profile_cutoff,
    c(theta_hat, coefficient_bounds[[2L]]),
    tol = 1e-12
  )$root
)

full_negative_log_likelihood <- function(parameters) {
  epsilon <- conditional_residuals(parameters[[1L]], parameters[[2L]])
  sigma <- exp(parameters[[3L]])
  length(epsilon) * log(sigma) +
    sum(epsilon^2) / (2.0 * sigma^2) +
    length(epsilon) / 2.0 * log(2.0 * pi)
}
full_fit <- stats::optim(
  c(phi_hat, theta_hat, log(sigma_hat)),
  full_negative_log_likelihood,
  method = "Nelder-Mead",
  control = list(reltol = 1e-14, maxit = 100000L)
)
stopifnot(full_fit$convergence == 0L)
stopifnot(max(abs(full_fit$par[1:2] - c(phi_hat, theta_hat))) < 1e-6)

sigma_profile <- function(sigma) {
  stats::optim(
    c(phi_hat, theta_hat),
    function(coefficients) full_negative_log_likelihood(c(coefficients, log(sigma))),
    method = "Nelder-Mead",
    control = list(reltol = 1e-13, maxit = 10000L)
  )$value
}
full_profile_cutoff <- full_fit$value + profile_cutoff_delta
sigma_interval <- c(
  stats::uniroot(
    function(sigma) sigma_profile(sigma) - full_profile_cutoff,
    c(sigma_hat / 4.0, sigma_hat),
    tol = 1e-12
  )$root,
  stats::uniroot(
    function(sigma) sigma_profile(sigma) - full_profile_cutoff,
    c(sigma_hat, sigma_hat * 4.0),
    tol = 1e-12
  )$root
)

covariance <- solve(fit_from_zero$hessian)
theta_standard_error <- sqrt(covariance[[2L, 2L]])
theta_fixed_gate <- 0.15 * abs(arima_fixture$theta)
theta_fixed_gate_coverage <- 2.0 * stats::pnorm(theta_fixed_gate / theta_standard_error) - 1.0

css_fit <- stats::arima(
  differences,
  order = c(1L, 0L, 1L),
  include.mean = FALSE,
  method = "CSS",
  transform.pars = FALSE
)
exact_fit <- stats::arima(
  differences,
  order = c(1L, 0L, 1L),
  include.mean = FALSE,
  method = "ML",
  transform.pars = FALSE
)

truth <- c(arima_fixture$phi, arima_fixture$theta, arima_fixture$sigma)
profile_intervals <- rbind(phi_interval, theta_interval, sigma_interval)
stopifnot(all(truth >= profile_intervals[, 1L] & truth <= profile_intervals[, 2L]))

artifact <- list(
  metadata = list(
    artifact_id = "TS-PHASE5-ARIMA-MLE-001",
    finding = "PHASE5-RECOVERY-ARIMA-MLE",
    generator = "verification/r/time-series/generate_phase5_arima_mle_oracle.R",
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
      deterministic_recurrence_absolute = 1e-12
    ),
    profile_confidence_level = 0.95,
    profile_log_likelihood_cutoff = profile_cutoff_delta
  ),
  fixture = list(
    seed = arima_fixture$seed,
    burn_in = arima_fixture$burn_in,
    raw_sample_size = length(raw),
    difference_count = length(differences),
    conditional_likelihood_count = conditional_count,
    transform = arima_fixture$transform,
    differencing_order = arima_fixture$differencing_order,
    ar_order = arima_fixture$ar_order,
    ma_order = arima_fixture$ma_order,
    include_intercept = arima_fixture$include_intercept,
    truth = list(
      phi = arima_fixture$phi,
      theta = arima_fixture$theta,
      sigma = arima_fixture$sigma
    )
  ),
  conditional_mle = list(
    phi = phi_hat,
    theta = theta_hat,
    sigma = sigma_hat,
    gaussian_log_likelihood = unname(gaussian_log_likelihood),
    log_jacobian = unname(log_jacobian),
    data_log_likelihood = unname(data_log_likelihood),
    start_zero_function_evaluations = unname(fit_from_zero$counts[[1L]]),
    start_zero_convergence = unname(fit_from_zero$convergence),
    start_truth_convergence = unname(fit_from_truth$convergence)
  ),
  profile_likelihood_95 = list(
    phi = unname(phi_interval),
    theta = unname(theta_interval),
    sigma = unname(sigma_interval)
  ),
  diagnosis = list(
    theta_standard_error = unname(theta_standard_error),
    theta_observed_z = unname((theta_hat - arima_fixture$theta) / theta_standard_error),
    theta_two_sided_probability = unname(
      2.0 * stats::pnorm(-abs((theta_hat - arima_fixture$theta) / theta_standard_error))
    ),
    theta_fixed_15_percent_absolute_gate = unname(theta_fixed_gate),
    theta_fixed_gate_asymptotic_coverage = unname(theta_fixed_gate_coverage),
    stats_css = list(
      phi = unname(css_fit$coef[[1L]]),
      theta = unname(css_fit$coef[[2L]]),
      sigma = unname(sqrt(css_fit$sigma2))
    ),
    stats_exact_ml = list(
      phi = unname(exact_fit$coef[[1L]]),
      theta = unname(exact_fit$coef[[2L]]),
      sigma = unname(sqrt(exact_fit$sigma2))
    )
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
