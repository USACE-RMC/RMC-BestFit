# Generate the independent Phase 5 ARIMAX date-alignment oracle for TR-041.
#
# This script implements transform, differencing, raw/model index mapping,
# date-keyed level-covariate selection, ARMA residual recursion, Jacobian, and
# Gaussian conditional likelihood directly in R. It does not call Numerics or
# RMC.BestFit.

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
  "phase5-arimax-alignment-oracle.json"
)

box_cox_transform <- function(x, lambda) {
  if (abs(lambda) < 1e-8) log(x) else (x^lambda - 1.0) / lambda
}

difference_values <- function(x, order) {
  result <- x
  if (order > 0L) {
    for (iteration in seq_len(order)) {
      result <- result[-1L] - result[-length(result)]
    }
  }
  result
}

gaussian_log_density <- function(x, sigma) {
  -0.5 * log(2.0 * pi) - log(sigma) - 0.5 * (x / sigma)^2
}

evaluate_case <- function(order, raw, dates, covariate_values, training_steps,
                          lambda, intercept, beta, phi, theta, sigma) {
  transformed <- box_cox_transform(raw, lambda)
  differences <- difference_values(transformed, order)
  difference_dates <- dates[seq.int(order + 1L, length(dates))]
  training_count <- training_steps - order
  training_differences <- differences[seq_len(training_count)]
  training_dates <- difference_dates[seq_len(training_count)]
  raw_indices <- seq.int(order, training_steps - 1L)
  covariate_by_date <- stats::setNames(covariate_values, format(dates, "%Y-%m-%d"))
  matched_covariates <- unname(covariate_by_date[format(training_dates, "%Y-%m-%d")])

  means <- intercept + beta * matched_covariates
  residuals <- numeric(training_count)
  predictions <- numeric(training_count)
  max_order <- 1L
  for (position in seq_len(training_count)) {
    model_index <- position - 1L
    if (model_index < max_order) {
      predictions[[position]] <- training_differences[[position]]
    } else {
      ar_part <- phi * (training_differences[[position - 1L]] - means[[position - 1L]])
      ma_part <- theta * residuals[[position - 1L]]
      predictions[[position]] <- means[[position]] + ar_part + ma_part
    }
    residuals[[position]] <- training_differences[[position]] - predictions[[position]]
  }

  evaluation_positions <- seq.int(max_order + 1L, training_count)
  evaluation_raw_indices <- raw_indices[evaluation_positions]
  jacobian <- sum((lambda - 1.0) * log(raw[evaluation_raw_indices + 1L]))
  gaussian_terms <- gaussian_log_density(residuals[evaluation_positions], sigma)
  pointwise <- gaussian_terms + (lambda - 1.0) * log(raw[evaluation_raw_indices + 1L])

  list(
    differencing_order = order,
    training_difference_count = training_count,
    difference_dates = format(difference_dates, "%Y-%m-%d"),
    training_difference_values = unname(training_differences),
    model_to_raw_indices = unname(raw_indices),
    matched_level_covariates = unname(matched_covariates),
    predictions = unname(predictions),
    residuals = unname(residuals),
    evaluation_raw_indices = unname(evaluation_raw_indices),
    jacobian = unname(jacobian),
    pointwise_log_likelihood = unname(pointwise),
    log_likelihood = unname(sum(pointwise))
  )
}

git_revision <- function(path) {
  revision <- tryCatch(
    system2("git", c("-C", shQuote(path), "rev-parse", "HEAD"), stdout = TRUE),
    error = function(...) character()
  )
  if (length(revision) == 0L) NA_character_ else revision[[1L]]
}

raw <- c(1.2, 1.9, 3.1, 4.8, 7.4, 10.9, 15.7, 22.0, 30.2, 41.0)
dates <- seq(as.Date("2001-02-03"), by = "day", length.out = length(raw))
covariate_values <- c(2.0, -1.0, 0.5, 3.0, -2.0, 1.5, 4.0, -0.5, 8.0, -7.0)
training_steps <- 8L
lambda <- 0.4
intercept <- 0.25
beta <- 1.1
phi <- 0.3
theta <- -0.2
sigma <- 0.75

artifact <- list(
  metadata = list(
    artifact_id = "TS-PHASE5-ARIMAX-ALIGNMENT-001",
    finding = "TR-041",
    generator = "verification/r/time-series/generate_phase5_arimax_alignment_oracle.R",
    generator_sha256 = unname(digest::digest(file = script_path, algo = "sha256")),
    source_commit = git_revision(repository_root),
    r_version = R.version.string,
    jsonlite_version = as.character(utils::packageVersion("jsonlite")),
    digest_version = as.character(utils::packageVersion("digest")),
    generated_utc = format(Sys.time(), tz = "UTC", usetz = TRUE),
    tolerance_absolute = 1e-10,
    random_seed = NA_integer_
  ),
  fixture = list(
    dates = format(dates, "%Y-%m-%d"),
    raw = raw,
    alternate_holdout = c(3002.0, 0.041),
    covariate_values = covariate_values,
    training_steps = training_steps,
    transform = "BoxCox",
    lambda = lambda,
    ar_order = 1L,
    ma_order = 1L,
    covariate_lag_order = 0L,
    include_intercept = TRUE,
    intercept = intercept,
    beta = beta,
    phi = phi,
    theta = theta,
    sigma = sigma
  ),
  cases = lapply(0:2, function(order) {
    evaluate_case(
      order,
      raw,
      dates,
      covariate_values,
      training_steps,
      lambda,
      intercept,
      beta,
      phi,
      theta,
      sigma
    )
  })
)

dir.create(dirname(output_path), recursive = TRUE, showWarnings = FALSE)
jsonlite::write_json(
  artifact,
  output_path,
  auto_unbox = TRUE,
  pretty = TRUE,
  digits = NA,
  na = "null"
)

message(sprintf("Wrote %s", output_path))
