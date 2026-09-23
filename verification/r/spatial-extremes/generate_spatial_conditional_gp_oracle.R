# Generate the conditional Gaussian-process (simple kriging) oracle for RMC.BestFit (Phase 6, TR-054).
#
# The spatial GEV model's latent regression errors follow epsilon ~ MVN(0, sigma^2 R) with an
# exponential correlation R_ij = exp(-h_ij / range) on projected coordinates. At an ungauged location
# s* the conditional (simple-kriging) prediction given the sampled errors at the S sites is
#   E(eps* | eps) = k*' K^-1 eps,   Var(eps* | eps) = sigma^2 - k*' K^-1 k*,
# with K = sigma^2 R and k*_j = sigma^2 exp(-h(s*, s_j) / range). This script records, for several
# (sigma, range, eps) parameter sets and several target locations, the conditional mean and variance
# computed with dense linear algebra (solve), which RMC.BestFit's SpatialRegressionErrors.GetKrigingPrediction
# must reproduce to 1e-10, and the implied conditional GEV location under the log link. RMC.BestFit
# tests read only the committed JSON.

script_argument <- grep("^--file=", commandArgs(trailingOnly = FALSE), value = TRUE)
script_path <- normalizePath(sub("^--file=", "", script_argument[[1L]]), winslash = "/", mustWork = TRUE)
repository_root <- normalizePath(file.path(dirname(script_path), "..", "..", ".."), winslash = "/", mustWork = TRUE)
renv::load(file.path(repository_root, "verification", "r"), quiet = TRUE)

output_path <- file.path(repository_root, "verification", "data", "spatial-extremes", "spatial-conditional-gp-oracle.json")

# ---------------------------------------------------------------- network
coordinates <- matrix(c(0, 0, 12, 3, 25, 0, 8, 18, 30, 22), ncol = 2, byrow = TRUE)
sites <- nrow(coordinates)
distance <- as.matrix(dist(coordinates, method = "euclidean"))
targets <- matrix(c(10, 10, 0, 0, 40, 35, 18, 8, -5, 12), ncol = 2, byrow = TRUE)
location_intercept <- log(100)

# ---------------------------------------------------------------- parameter sets
parameter_sets <- list(
  list(name = "moderate", sigma = 0.06, range = 20, errors = c(0.05, -0.03, 0.02, -0.04, 0.01)),
  list(name = "short_range", sigma = 0.15, range = 6, errors = c(0.10, -0.12, 0.08, 0.03, -0.07)),
  list(name = "long_range", sigma = 0.30, range = 80, errors = c(-0.20, -0.15, 0.25, 0.10, 0.05))
)

conditional <- function(sigma, range, errors, target) {
  K <- sigma^2 * exp(-distance / range)
  diag(K) <- sigma^2
  h_star <- sqrt((coordinates[, 1] - target[1])^2 + (coordinates[, 2] - target[2])^2)
  k_star <- sigma^2 * exp(-h_star / range)
  alpha <- solve(K, errors)
  v <- solve(K, k_star)
  mean <- sum(k_star * alpha)
  variance <- sigma^2 - sum(k_star * v)
  list(mean = mean, variance = variance, distances = h_star)
}

cases <- list()
for (ps in parameter_sets) {
  for (t in seq_len(nrow(targets))) {
    target <- targets[t, ]
    cond <- conditional(ps$sigma, ps$range, ps$errors, target)
    cases[[length(cases) + 1L]] <- list(
      parameter_set = ps$name,
      sigma = ps$sigma,
      range = ps$range,
      errors = ps$errors,
      target = as.numeric(target),
      target_is_site = any(apply(coordinates, 1, function(s) all(abs(s - target) < 1e-12))),
      distances_to_sites = cond$distances,
      conditional_mean = cond$mean,
      conditional_variance = cond$variance,
      conditional_location_log_link = exp(location_intercept + cond$mean)
    )
  }
}

artifact <- list(
  metadata = list(
    generated = format(Sys.Date(), "%Y-%m-%d"),
    generator = "verification/r/spatial-extremes/generate_spatial_conditional_gp_oracle.R",
    generator_sha256 = digest::digest(file = script_path, algo = "sha256"),
    r_version = R.version.string,
    jsonlite = as.character(packageVersion("jsonlite")),
    digest = as.character(packageVersion("digest")),
    sites = sites,
    distance_metric = "Euclidean on projected coordinates (Numerics Tools.Distance)",
    correlation_function = "exponential rho(h) = exp(-h / range) with rho(0) = 1; covariance sigma^2 rho(h)",
    predictor = "simple kriging: mean k*' K^-1 eps, variance sigma^2 - k*' K^-1 k*; dense solve in R",
    location_link = "log link: conditional location exp(intercept + conditional mean) with intercept log(100)",
    tolerances = list(conditional_mean = 1e-10, conditional_variance = 1e-10)
  ),
  network = list(coordinates = lapply(seq_len(sites), function(i) as.numeric(coordinates[i, ])),
                 targets = lapply(seq_len(nrow(targets)), function(i) as.numeric(targets[i, ]))),
  location_intercept = location_intercept,
  cases = cases
)

dir.create(dirname(output_path), recursive = TRUE, showWarnings = FALSE)
jsonlite::write_json(artifact, output_path, auto_unbox = TRUE, pretty = TRUE, digits = NA)
message(sprintf("Wrote %s with %d cases", output_path, length(cases)))
