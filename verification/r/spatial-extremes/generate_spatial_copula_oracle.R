# Generate the spatial GEV Gaussian-copula likelihood oracle for RMC.BestFit (Phase 6, TR-048/TR-049/TR-057).
#
# The oracle defines, with mvtnorm, the observed-data log likelihood of a homogeneous spatial GEV
# model with Gaussian-copula dependence on a five-site projected network:
#   - missing_site_model: rows with patterned missing sites, where the copula density must be the
#     Gaussian copula over the observed-site correlation submatrix (TR-048). The artifact also
#     records the value obtained by substituting a zero latent score for each missing site and
#     evaluating the full-dimensional density, which is the behavior under review.
#   - complete_data_model: the complete rows only (marginal and copula conventions).
#   - location_error_model: the complete rows with latent location errors, recording the data log
#     likelihood without the Gaussian-process density and the process log density separately so the
#     data/prior decomposition and the posterior-kernel invariance can be verified (TR-049, TR-057).
# GEV uses the Numerics (Hosking) convention. RMC.BestFit tests read only the committed JSON.

script_argument <- grep("^--file=", commandArgs(trailingOnly = FALSE), value = TRUE)
script_path <- normalizePath(sub("^--file=", "", script_argument[[1L]]), winslash = "/", mustWork = TRUE)
repository_root <- normalizePath(file.path(dirname(script_path), "..", "..", ".."), winslash = "/", mustWork = TRUE)
renv::load(file.path(repository_root, "verification", "r"), quiet = TRUE)

output_path <- file.path(repository_root, "verification", "data", "spatial-extremes", "spatial-copula-likelihood-oracle.json")

# ---------------------------------------------------------------- network and parameters
coordinates <- matrix(c(0, 0, 12, 3, 25, 0, 8, 18, 30, 22), ncol = 2, byrow = TRUE)
sites <- nrow(coordinates)
copula_range <- 15
location_intercept <- log(100)
scale_intercept <- log(30)
shape <- -0.1
seed <- 20260821
rows <- 12L

distance <- as.matrix(dist(coordinates, method = "euclidean"))
correlation <- exp(-distance / copula_range)
diag(correlation) <- 1

# ---------------------------------------------------------------- GEV (Hosking convention, as Numerics)
gev_logpdf <- function(x, xi, alpha, kappa) {
  y <- (x - xi) / alpha
  if (abs(kappa) > 1e-12) y <- -log(1 - kappa * y) / kappa
  -(1 - kappa) * y - exp(-y) - log(alpha)
}
gev_cdf <- function(x, xi, alpha, kappa) {
  y <- (x - xi) / alpha
  if (abs(kappa) > 1e-12) y <- -log(1 - kappa * y) / kappa
  exp(-exp(-y))
}
gev_quantile <- function(p, xi, alpha, kappa) {
  if (abs(kappa) > 1e-12) xi + alpha * (1 - (-log(p))^kappa) / kappa else xi - alpha * log(-log(p))
}

# ---------------------------------------------------------------- data
set.seed(seed)
latent <- mvtnorm::rmvnorm(rows, mean = rep(0, sites), sigma = correlation)
uniforms <- pnorm(latent)
xi <- exp(location_intercept)
alpha <- exp(scale_intercept)
complete_data <- matrix(gev_quantile(as.vector(uniforms), xi, alpha, shape), nrow = rows, ncol = sites)
missing_pattern <- list(
  integer(0), integer(0), integer(0), integer(0),
  2L, c(1L, 5L), 3L, c(2L, 3L, 4L), 1:5,
  integer(0), integer(0), integer(0)
)
data <- complete_data
for (i in seq_len(rows)) {
  if (length(missing_pattern[[i]]) > 0) data[i, missing_pattern[[i]]] <- NA_real_
}

# ---------------------------------------------------------------- likelihood pieces
copula_log_density <- function(z, R) {
  if (length(z) < 2) return(0)
  mvtnorm::dmvnorm(z, mean = rep(0, length(z)), sigma = R, log = TRUE) - sum(dnorm(z, log = TRUE))
}

evaluate_rows <- function(values, site_xi, R) {
  rows_out <- list()
  total <- 0
  total_placeholder <- 0
  marginal_total <- 0
  for (i in seq_len(nrow(values))) {
    observed <- which(!is.na(values[i, ]))
    if (length(observed) == 0) {
      rows_out[[i]] <- list(
        row = i, observed_sites = integer(0), marginal_log_density = 0,
        copula_log_density_observed_subset = 0, copula_log_density_zero_placeholder = 0,
        row_log_likelihood = 0, row_log_likelihood_zero_placeholder = 0
      )
      next
    }
    marginal <- sum(vapply(observed, function(j) gev_logpdf(values[i, j], site_xi[j], alpha, shape), numeric(1)))
    z <- vapply(observed, function(j) qnorm(gev_cdf(values[i, j], site_xi[j], alpha, shape)), numeric(1))
    copula_subset <- copula_log_density(z, R[observed, observed, drop = FALSE])
    z_placeholder <- rep(0, ncol(values))
    z_placeholder[observed] <- z
    copula_placeholder <- mvtnorm::dmvnorm(z_placeholder, mean = rep(0, ncol(values)), sigma = R, log = TRUE) -
      sum(dnorm(z_placeholder, log = TRUE))
    rows_out[[i]] <- list(
      row = i,
      observed_sites = observed,
      marginal_log_density = marginal,
      copula_log_density_observed_subset = copula_subset,
      copula_log_density_zero_placeholder = copula_placeholder,
      row_log_likelihood = marginal + copula_subset,
      row_log_likelihood_zero_placeholder = marginal + copula_placeholder
    )
    total <- total + marginal + copula_subset
    total_placeholder <- total_placeholder + marginal + copula_placeholder
    marginal_total <- marginal_total + marginal
  }
  list(rows = rows_out, total_log_likelihood = total, total_log_likelihood_zero_placeholder = total_placeholder,
       marginal_only_total = marginal_total)
}

site_xi <- rep(xi, sites)
missing_eval <- evaluate_rows(data, site_xi, correlation)
complete_rows <- which(vapply(seq_len(rows), function(i) all(!is.na(data[i, ])), logical(1)))
complete_eval <- evaluate_rows(data[complete_rows, , drop = FALSE], site_xi, correlation)

# Location-error model on the complete rows: latent errors enter the log-link location.
errors <- c(0.05, -0.03, 0.02, -0.04, 0.01)
error_scale <- 0.06
error_range <- 20
error_correlation <- exp(-distance / error_range)
diag(error_correlation) <- 1
error_covariance <- error_scale^2 * error_correlation
site_xi_errors <- exp(location_intercept + errors)
error_eval <- evaluate_rows(data[complete_rows, , drop = FALSE], site_xi_errors, correlation)
process_log_density <- mvtnorm::dmvnorm(errors, mean = rep(0, sites), sigma = error_covariance, log = TRUE)

to_json_matrix <- function(m) lapply(seq_len(nrow(m)), function(i) as.list(m[i, ]))
artifact <- list(
  metadata = list(
    generated = format(Sys.Date()),
    generator = "verification/r/spatial-extremes/generate_spatial_copula_oracle.R",
    generator_sha256 = digest::digest(file = script_path, algo = "sha256"),
    r_version = R.version.string,
    mvtnorm = as.character(packageVersion("mvtnorm")),
    jsonlite = as.character(packageVersion("jsonlite")),
    digest = as.character(packageVersion("digest")),
    seed = seed,
    rows = rows,
    sites = sites,
    distance_metric = "Euclidean on projected coordinates (Numerics Tools.Distance)",
    correlation_function = "exponential rho(h) = exp(-h / range) with rho(0) = 1",
    gev_convention = "Hosking: y = -log(1 - kappa (x - xi) / alpha) / kappa, log f = -(1 - kappa) y - exp(-y) - log(alpha)",
    links = "log link for location and scale, identity for shape",
    copula_density = "log phi_R(z) - sum log phi(z_j) with z_j = PhiInverse(F_GEV(y_j)) over the observed sites",
    tolerances = list(total_log_likelihood = 1e-8, row_log_likelihood = 1e-10)
  ),
  network = list(coordinates = to_json_matrix(coordinates), distance_matrix = to_json_matrix(distance)),
  missing_site_model = list(
    bestfit_parameter_names = c("copula range", "location intercept", "scale intercept", "shape"),
    bestfit_parameters = c(copula_range, location_intercept, scale_intercept, shape),
    site_location = site_xi,
    site_scale = rep(alpha, sites),
    site_shape = rep(shape, sites),
    correlation_matrix = to_json_matrix(correlation),
    data = to_json_matrix(data),
    missing_sites_by_row = missing_pattern,
    rows = missing_eval$rows,
    total_log_likelihood = missing_eval$total_log_likelihood,
    total_log_likelihood_zero_placeholder = missing_eval$total_log_likelihood_zero_placeholder,
    marginal_only_total = missing_eval$marginal_only_total
  ),
  complete_data_model = list(
    bestfit_parameters = c(copula_range, location_intercept, scale_intercept, shape),
    source_rows = complete_rows,
    data = to_json_matrix(data[complete_rows, , drop = FALSE]),
    rows = complete_eval$rows,
    total_log_likelihood = complete_eval$total_log_likelihood,
    marginal_only_total = complete_eval$marginal_only_total
  ),
  location_error_model = list(
    bestfit_parameter_names = c("copula range", "location intercept", "scale intercept", "shape",
                                "error scale", "error range", paste0("epsilon_", seq_len(sites))),
    bestfit_parameters = c(copula_range, location_intercept, scale_intercept, shape, error_scale, error_range, errors),
    data = to_json_matrix(data[complete_rows, , drop = FALSE]),
    errors = errors,
    error_scale = error_scale,
    error_range = error_range,
    error_covariance = to_json_matrix(error_covariance),
    site_location_with_errors = site_xi_errors,
    rows = error_eval$rows,
    data_log_likelihood_without_process_density = error_eval$total_log_likelihood,
    process_log_density = process_log_density,
    data_plus_process = error_eval$total_log_likelihood + process_log_density
  )
)

dir.create(dirname(output_path), recursive = TRUE, showWarnings = FALSE)
jsonlite::write_json(artifact, output_path, auto_unbox = TRUE, pretty = TRUE, digits = NA, na = "null")
message(sprintf("Wrote %s", output_path))
message(sprintf("missing-site total %.12f (placeholder %.12f); complete %.12f; error model data %.12f process %.12f",
                missing_eval$total_log_likelihood, missing_eval$total_log_likelihood_zero_placeholder,
                complete_eval$total_log_likelihood, error_eval$total_log_likelihood, process_log_density))
