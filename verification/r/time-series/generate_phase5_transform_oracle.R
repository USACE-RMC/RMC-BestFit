# Generate the independent Phase 5 training-only transform oracle for TR-036/TR-046.
#
# The Box-Cox and Yeo-Johnson profile objectives are implemented directly in R.
# Neither implementation calls Numerics or RMC.BestFit. The manual-state fixture
# also evaluates the conditional Gaussian time-series likelihood directly from
# the transformed observations and the change-of-variables Jacobian.

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

output_path <- file.path(
  repository_root,
  "verification",
  "data",
  "time-series",
  "phase5-transform-lambda-oracle.json"
)

box_cox_transform <- function(x, lambda) {
  if (abs(lambda) < 1e-8) log(x) else (x^lambda - 1.0) / lambda
}

box_cox_jacobian <- function(x, lambda) {
  sum((lambda - 1.0) * log(x))
}

box_cox_profile <- function(x, lambda) {
  transformed <- box_cox_transform(x, lambda)
  sigma_squared <- mean((transformed - mean(transformed))^2)
  -length(x) / 2.0 * log(2.0 * pi) -
    length(x) / 2.0 * log(sigma_squared) -
    sum((transformed - mean(transformed))^2) / (2.0 * sigma_squared) +
    box_cox_jacobian(x, lambda)
}

yeo_johnson_transform <- function(x, lambda) {
  transformed <- numeric(length(x))
  nonnegative <- x >= 0.0

  if (abs(lambda) < 1e-8) {
    transformed[nonnegative] <- log1p(x[nonnegative])
  } else {
    transformed[nonnegative] <- ((x[nonnegative] + 1.0)^lambda - 1.0) / lambda
  }

  negative <- !nonnegative
  if (abs(lambda - 2.0) < 1e-8) {
    transformed[negative] <- -log1p(-x[negative])
  } else {
    transformed[negative] <- -((1.0 - x[negative])^(2.0 - lambda) - 1.0) / (2.0 - lambda)
  }

  transformed
}

yeo_johnson_jacobian <- function(x, lambda) {
  log_derivative <- numeric(length(x))
  nonnegative <- x >= 0.0
  log_derivative[nonnegative] <- (lambda - 1.0) * log1p(x[nonnegative])
  log_derivative[!nonnegative] <- (1.0 - lambda) * log1p(-x[!nonnegative])
  sum(log_derivative)
}

yeo_johnson_profile <- function(x, lambda) {
  transformed <- yeo_johnson_transform(x, lambda)
  sigma_squared <- mean((transformed - mean(transformed))^2)
  -length(x) / 2.0 * log(2.0 * pi) -
    length(x) / 2.0 * log(sigma_squared) -
    sum((transformed - mean(transformed))^2) / (2.0 * sigma_squared) +
    yeo_johnson_jacobian(x, lambda)
}

gaussian_log_density <- function(x, sigma) {
  -0.5 * log(2.0 * pi) - log(sigma) - 0.5 * (x / sigma)^2
}

box_cox_training <- c(1.1, 1.3, 1.8, 2.7, 5.0, 12.0)
box_cox_holdout <- c(18.0, 31.0, 54.0)
box_cox_alternate_holdout <- c(1800.0, 0.031, 5400.0)
box_cox_fit <- stats::optimize(
  function(lambda) box_cox_profile(box_cox_training, lambda),
  interval = c(-5.0, 5.0),
  maximum = TRUE,
  tol = 1e-14
)

yeo_johnson_training <- c(-4.0, -2.0, -0.5, 0.5, 2.0, 4.0)
yeo_johnson_holdout <- c(-12.0, 9.0, 20.0)
yeo_johnson_alternate_holdout <- c(-1200.0, 900.0, 0.01)
yeo_johnson_fit <- stats::optimize(
  function(lambda) yeo_johnson_profile(yeo_johnson_training, lambda),
  interval = c(-5.0, 5.0),
  maximum = TRUE,
  tol = 1e-14
)

manual_raw <- c(-3.5, -1.7, -0.4, 0.2, 1.1, 2.4, 4.0, 6.2, -30.0, 40.0)
manual_training_steps <- 8L
manual_lambda <- 0.6
manual_phi <- 0.35
manual_theta <- -0.25
manual_sigma <- 0.8
manual_training_raw <- manual_raw[seq_len(manual_training_steps)]
manual_transformed <- yeo_johnson_transform(manual_raw, manual_lambda)
manual_training_transformed <- manual_transformed[seq_len(manual_training_steps)]

ar_residuals <- manual_training_transformed[-1L] -
  manual_phi * manual_training_transformed[-manual_training_steps]
ar_jacobian <- yeo_johnson_jacobian(manual_training_raw[-1L], manual_lambda)
ar_log_likelihood <- sum(gaussian_log_density(ar_residuals, manual_sigma)) + ar_jacobian

ma_residuals <- numeric(manual_training_steps)
for (i in seq_len(manual_training_steps)) {
  prediction <- if (i == 1L) 0.0 else manual_theta * ma_residuals[[i - 1L]]
  ma_residuals[[i]] <- manual_training_transformed[[i]] - prediction
}
ma_jacobian <- yeo_johnson_jacobian(manual_training_raw, manual_lambda)
ma_log_likelihood <- sum(gaussian_log_density(ma_residuals, manual_sigma)) + ma_jacobian

git_revision <- function(path) {
  revision <- tryCatch(
    system2("git", c("-C", shQuote(path), "rev-parse", "HEAD"), stdout = TRUE),
    error = function(...) character()
  )
  if (length(revision) == 0L) NA_character_ else revision[[1L]]
}

artifact <- list(
  metadata = list(
    artifact_id = "TS-PHASE5-TRANSFORM-001",
    findings = c("TR-036", "TR-046"),
    generator = "verification/r/time-series/generate_phase5_transform_oracle.R",
    generator_sha256 = unname(digest::digest(file = script_path, algo = "sha256")),
    source_commit = git_revision(repository_root),
    r_version = R.version.string,
    jsonlite_version = as.character(utils::packageVersion("jsonlite")),
    digest_version = as.character(utils::packageVersion("digest")),
    generated_utc = format(Sys.time(), tz = "UTC", usetz = TRUE),
    training_boundary = 6L,
    tolerances = list(
      cross_language_absolute = 1e-8,
      cross_language_relative = 1e-7,
      deterministic_state_absolute = 1e-12
    )
  ),
  fitted = list(
    box_cox = list(
      raw = c(box_cox_training, box_cox_holdout),
      alternate_holdout = box_cox_alternate_holdout,
      expected_lambda = unname(box_cox_fit$maximum),
      expected_profile_log_likelihood = unname(box_cox_fit$objective),
      expected_training_transformed = unname(box_cox_transform(box_cox_training, box_cox_fit$maximum)),
      expected_training_jacobian = unname(box_cox_jacobian(box_cox_training, box_cox_fit$maximum))
    ),
    yeo_johnson = list(
      raw = c(yeo_johnson_training, yeo_johnson_holdout),
      alternate_holdout = yeo_johnson_alternate_holdout,
      expected_lambda = unname(yeo_johnson_fit$maximum),
      expected_profile_log_likelihood = unname(yeo_johnson_fit$objective),
      expected_training_transformed = unname(yeo_johnson_transform(yeo_johnson_training, yeo_johnson_fit$maximum)),
      expected_training_jacobian = unname(yeo_johnson_jacobian(yeo_johnson_training, yeo_johnson_fit$maximum))
    )
  ),
  manual = list(
    transform = "YeoJohnson",
    raw = manual_raw,
    training_steps = manual_training_steps,
    lambda = manual_lambda,
    phi = manual_phi,
    theta = manual_theta,
    sigma = manual_sigma,
    expected_full_transformed = unname(manual_transformed),
    expected_ar_residuals = unname(ar_residuals),
    expected_ma_residuals = unname(ma_residuals),
    expected_ar_jacobian = unname(ar_jacobian),
    expected_ma_jacobian = unname(ma_jacobian),
    expected_ar_log_likelihood = unname(ar_log_likelihood),
    expected_ma_log_likelihood = unname(ma_log_likelihood)
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
