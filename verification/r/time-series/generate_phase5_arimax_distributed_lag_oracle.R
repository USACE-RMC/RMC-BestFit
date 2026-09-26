# Generate the independent ARIMAX distributed-lag conditioning oracle for TR-066 (decision D6).
# Run from the repository root:
#   Rscript verification/r/time-series/generate_phase5_arimax_distributed_lag_oracle.R
#
# The approved rule conditions an ARIMAX model with covariates on K = max(q, p + b) leading
# model steps, so every evaluated step's own mean and every autoregressive-lag mean carry all b
# lagged covariate values. The existing alignment and MLE oracles use b = 0, where this rule and
# the former max(p, q, b) coincide; every case below has p > 0 and b > 0.
#
# This script implements the Box-Cox and Yeo-Johnson transforms at fixed exponents, differencing
# with later-value timestamps, date-keyed level-covariate lags, the conditioning order, the
# regression-with-ARMA-errors residual recursion, the per-observation transform Jacobian over the
# evaluated raw observations, the Gaussian conditional likelihood, and the conditional one-step
# predictions reconstructed on the raw scale directly in R. It does not call Numerics or
# RMC.BestFit. The recursion evaluates a mean only when all b covariate lags exist, and stops
# otherwise, so it never truncates a lag.

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
  "phase5-arimax-distributed-lag-oracle.json"
)

# Transforms and their log-derivatives, from the textbook definitions.
transform_values <- function(x, transform, lambda) {
  if (transform == "None") {
    return(x)
  }
  if (transform == "BoxCox") {
    stopifnot(all(x > 0), abs(lambda) > 1e-8)
    return((x^lambda - 1.0) / lambda)
  }
  if (transform == "YeoJohnson") {
    stopifnot(abs(lambda) > 1e-8, abs(lambda - 2.0) > 1e-8)
    result <- numeric(length(x))
    nonnegative <- x >= 0
    result[nonnegative] <- ((x[nonnegative] + 1.0)^lambda - 1.0) / lambda
    result[!nonnegative] <- -((1.0 - x[!nonnegative])^(2.0 - lambda) - 1.0) / (2.0 - lambda)
    return(result)
  }
  stop("Unknown transform: ", transform)
}

inverse_transform_values <- function(z, transform, lambda) {
  if (transform == "None") {
    return(z)
  }
  if (transform == "BoxCox") {
    stopifnot(all(lambda * z + 1.0 > 0))
    return((lambda * z + 1.0)^(1.0 / lambda))
  }
  if (transform == "YeoJohnson") {
    result <- numeric(length(z))
    nonnegative <- z >= 0
    result[nonnegative] <- (lambda * z[nonnegative] + 1.0)^(1.0 / lambda) - 1.0
    result[!nonnegative] <- 1.0 - (1.0 - (2.0 - lambda) * z[!nonnegative])^(1.0 / (2.0 - lambda))
    return(result)
  }
  stop("Unknown transform: ", transform)
}

log_jacobian_terms <- function(x, transform, lambda) {
  if (transform == "None") {
    return(rep(0.0, length(x)))
  }
  if (transform == "BoxCox") {
    return((lambda - 1.0) * log(x))
  }
  if (transform == "YeoJohnson") {
    result <- numeric(length(x))
    nonnegative <- x >= 0
    result[nonnegative] <- (lambda - 1.0) * log(x[nonnegative] + 1.0)
    result[!nonnegative] <- (1.0 - lambda) * log(1.0 - x[!nonnegative])
    return(result)
  }
  stop("Unknown transform: ", transform)
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

# Rebuild transformed-scale conditional levels from model-scale one-step predictions: each level
# adds the predicted highest-order difference to the observed lower-order states at the preceding
# raw index, and the first d levels are the observed transformed levels. The caller applies the
# inverse transform once to the completed levels.
reconstruct_levels <- function(predictions, transformed, training_steps, order) {
  if (order == 0L) {
    return(predictions)
  }
  states <- list(transformed[seq_len(training_steps)])
  if (order > 1L) {
    for (level in 2L:order) {
      previous <- states[[level - 1L]]
      states[[level]] <- previous[-1L] - previous[-length(previous)]
    }
  }
  levels <- numeric(training_steps)
  levels[seq_len(order)] <- transformed[seq_len(order)]
  for (raw_index in order:(training_steps - 1L)) {
    state <- predictions[[raw_index - order + 1L]]
    for (level in (order - 1L):0L) {
      state <- state + states[[level + 1L]][[raw_index - 1L - level + 1L]]
    }
    levels[[raw_index + 1L]] <- state
  }
  levels
}

evaluate_case <- function(case) {
  p <- case$ar_order
  q <- case$ma_order
  b <- case$covariate_lag_order
  d <- case$differencing_order
  covariate_count <- length(case$covariates)
  stopifnot(covariate_count > 0L, p > 0L, b > 0L)
  conditioning_order <- max(q, p + b)

  dates <- seq(as.Date(case$response_start_date), by = "day", length.out = length(case$raw))
  transformed <- transform_values(case$raw, case$transform, case$lambda)
  differences <- difference_values(transformed, d)
  difference_dates <- dates[seq.int(d + 1L, length(dates))]
  training_count <- case$training_steps - d
  w <- differences[seq_len(training_count)]
  model_dates <- difference_dates[seq_len(training_count)]

  covariate_lookup <- lapply(case$covariates, function(covariate) {
    covariate_dates <- seq(as.Date(covariate$start_date), by = "day", length.out = length(covariate$values))
    stopifnot(!anyDuplicated(covariate_dates))
    stats::setNames(covariate$values, format(covariate_dates, "%Y-%m-%d"))
  })
  covariate_at <- function(index, date) {
    value <- covariate_lookup[[index]][format(date, "%Y-%m-%d")]
    if (is.na(value)) stop("Covariate ", index, " has no value on ", format(date, "%Y-%m-%d"), ".")
    unname(value)
  }

  # Model step k (zero-based) is dated by raw response index k + d; lag j uses model step k - j.
  mean_at <- function(k) {
    value <- case$intercept
    for (index in seq_len(covariate_count)) {
      for (lag in 0L:b) {
        if (k - lag < 0L) stop("The mean at model step ", k, " needs covariate lag ", lag, ".")
        value <- value + case$beta[[index]][[lag + 1L]] * covariate_at(index, model_dates[[k - lag + 1L]])
      }
    }
    value
  }

  residuals <- numeric(training_count)
  predictions <- numeric(training_count)
  for (position in seq_len(training_count)) {
    k <- position - 1L
    if (k < conditioning_order) {
      predictions[[position]] <- w[[position]]
      residuals[[position]] <- 0.0
      next
    }
    prediction <- mean_at(k)
    for (i in seq_len(p)) {
      prediction <- prediction + case$phi[[i]] * (w[[position - i]] - mean_at(k - i))
    }
    for (j in seq_len(q)) {
      prediction <- prediction + case$theta[[j]] * residuals[[position - j]]
    }
    predictions[[position]] <- prediction
    residuals[[position]] <- w[[position]] - prediction
  }

  evaluation_positions <- seq.int(conditioning_order + 1L, training_count)
  evaluation_raw_indices <- d + evaluation_positions - 1L
  jacobian_terms <- log_jacobian_terms(case$raw[evaluation_raw_indices + 1L], case$transform, case$lambda)
  pointwise <- gaussian_log_density(residuals[evaluation_positions], case$sigma) + jacobian_terms
  prediction_levels <- inverse_transform_values(
    reconstruct_levels(predictions, transformed, case$training_steps, d),
    case$transform,
    case$lambda
  )
  parameters <- c(case$intercept, unlist(case$beta), case$phi, case$theta, case$sigma)

  list(
    case_id = case$case_id,
    description = case$description,
    transform = case$transform,
    lambda = case$lambda,
    differencing_order = d,
    ar_order = p,
    ma_order = q,
    covariate_lag_order = b,
    covariate_count = covariate_count,
    include_intercept = TRUE,
    conditioning_order = conditioning_order,
    previous_conditioning_order = max(p, q, b),
    response_start_date = case$response_start_date,
    raw = case$raw,
    training_steps = case$training_steps,
    covariates = case$covariates,
    intercept = case$intercept,
    beta = lapply(case$beta, I),
    phi = I(case$phi),
    theta = I(case$theta),
    sigma = case$sigma,
    parameters = unname(parameters),
    training_difference_count = training_count,
    training_difference_values = unname(w),
    model_dates = format(model_dates, "%Y-%m-%d"),
    residuals = unname(residuals),
    predictions = unname(predictions),
    evaluated_count = length(evaluation_positions),
    evaluation_raw_indices = unname(evaluation_raw_indices),
    jacobian = unname(sum(jacobian_terms)),
    jacobian_terms = unname(jacobian_terms),
    pointwise_log_likelihood = unname(pointwise),
    log_likelihood = unname(sum(pointwise)),
    prediction_levels = unname(prediction_levels)
  )
}

git_revision <- function(path) {
  revision <- tryCatch(
    system2("git", c("-C", shQuote(path), "rev-parse", "HEAD"), stdout = TRUE),
    error = function(...) character()
  )
  if (length(revision) == 0L) NA_character_ else revision[[1L]]
}

level_raw <- c(12.4, 15.1, 13.8, 17.2, 16.5, 19.9, 18.3, 21.7, 20.6, 24.2, 23.1, 26.8)
signed_raw <- c(0.8, -0.3, 1.5, 0.4, -0.9, 1.1, 2.0, 0.2, -0.5, 1.7, 0.9, 1.3)
response_start_date <- "2003-04-05"

# Covariates start before the response so that positional pairing would select the wrong values.
first_covariate <- list(
  start_date = "2003-04-03",
  values = c(0.9, -1.2, 1.6, 0.3, -0.7, 2.1, 1.1, -0.4, 0.8, 1.9, -1.5, 0.6, 1.3, -0.2)
)
second_covariate <- list(
  start_date = "2003-04-04",
  values = c(-0.5, 1.1, 0.3, -1.4, 0.6, 1.0, -0.2, 0.8, -1.3, 0.5, 1.4, -0.7, 0.1)
)

case_definitions <- list(
  list(
    case_id = "ar-plus-lag-decides",
    description = "ARIMAX(1,0,0) with one covariate at lags 0-2; p + b = 3 exceeds q = 0.",
    transform = "None",
    lambda = 0.0,
    differencing_order = 0L,
    ar_order = 1L,
    ma_order = 0L,
    covariate_lag_order = 2L,
    response_start_date = response_start_date,
    raw = level_raw,
    training_steps = 10L,
    covariates = list(first_covariate),
    intercept = 18.0,
    beta = list(c(0.9, 0.35, -0.25)),
    phi = c(0.55),
    theta = numeric(0),
    sigma = 0.9
  ),
  list(
    case_id = "ma-order-decides",
    description = "ARIMAX(1,0,3) with one covariate at lags 0-1; q = 3 exceeds p + b = 2.",
    transform = "None",
    lambda = 0.0,
    differencing_order = 0L,
    ar_order = 1L,
    ma_order = 3L,
    covariate_lag_order = 1L,
    response_start_date = response_start_date,
    raw = level_raw,
    training_steps = 10L,
    covariates = list(first_covariate),
    intercept = 18.5,
    beta = list(c(0.7, -0.3)),
    phi = c(0.35),
    theta = c(0.25, -0.15, 0.1),
    sigma = 1.1
  ),
  list(
    case_id = "box-cox-differenced",
    description = "Box-Cox (lambda 0.35) ARIMAX(2,1,1) with one covariate at lags 0-1; p + b = 3 exceeds q = 1.",
    transform = "BoxCox",
    lambda = 0.35,
    differencing_order = 1L,
    ar_order = 2L,
    ma_order = 1L,
    covariate_lag_order = 1L,
    response_start_date = response_start_date,
    raw = level_raw,
    training_steps = 10L,
    covariates = list(first_covariate),
    intercept = 0.05,
    beta = list(c(0.04, -0.03)),
    phi = c(0.3, -0.15),
    theta = c(0.2),
    sigma = 0.5
  ),
  list(
    case_id = "yeo-johnson-two-covariates",
    description = "Yeo-Johnson (lambda 0.6) ARIMAX(1,0,1) with two covariates at lags 0-1 and negative responses; p + b = 2 exceeds q = 1.",
    transform = "YeoJohnson",
    lambda = 0.6,
    differencing_order = 0L,
    ar_order = 1L,
    ma_order = 1L,
    covariate_lag_order = 1L,
    response_start_date = response_start_date,
    raw = signed_raw,
    training_steps = 10L,
    covariates = list(first_covariate, second_covariate),
    intercept = 0.4,
    beta = list(c(0.3, -0.1), c(-0.2, 0.15)),
    phi = c(0.25),
    theta = c(-0.3),
    sigma = 0.6
  )
)

cases <- lapply(case_definitions, evaluate_case)
for (evaluated in cases) {
  stopifnot(evaluated$evaluated_count == evaluated$training_difference_count - evaluated$conditioning_order)
  stopifnot(all(evaluated$residuals[seq_len(evaluated$conditioning_order)] == 0.0))
}

artifact <- list(
  metadata = list(
    artifact_id = "TS-PHASE5-ARIMAX-DISTRIBUTED-LAG-001",
    finding = "TR-066",
    decision = "D6: condition ARIMAX models with covariates on max(q, p + b); approved 25 September 2026",
    generator = "verification/r/time-series/generate_phase5_arimax_distributed_lag_oracle.R",
    generator_sha256 = unname(digest::digest(file = script_path, algo = "sha256")),
    source_commit = git_revision(repository_root),
    r_version = R.version.string,
    jsonlite_version = as.character(utils::packageVersion("jsonlite")),
    digest_version = as.character(utils::packageVersion("digest")),
    generated_utc = format(Sys.time(), tz = "UTC", usetz = TRUE),
    tolerance_absolute = 1e-10,
    random_seed = NA_integer_,
    parameter_layout = "intercept, covariate coefficients (each covariate's lags 0..b), AR coefficients, MA coefficients, innovation scale"
  ),
  cases = cases
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
