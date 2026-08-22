# Generate the geodesic (great-circle) distance oracle for RMC.BestFit (Phase 6, TR-060).
#
# RMC.BestFit's spatial GEV components accept a distance metric: Cartesian (planar Euclidean on projected
# coordinates, the historical behavior) or Geodesic, which interprets coordinates as (latitude, longitude)
# in decimal degrees and uses the haversine great-circle distance in kilometres on a sphere of mean radius
# 6371.0088 km. This script records, for a five-site latitude/longitude network and five target points,
# the pairwise distance matrix, the target-to-site distances, the exponential correlation matrix at a
# 150 km range, and the simple-kriging conditional mean and variance of a latent-error field at the
# targets (sigma 0.2, range 150 km), all computed with the haversine formula in plain R. RMC.BestFit tests
# read only the committed JSON.

script_argument <- grep("^--file=", commandArgs(trailingOnly = FALSE), value = TRUE)
script_path <- normalizePath(sub("^--file=", "", script_argument[[1L]]), winslash = "/", mustWork = TRUE)
repository_root <- normalizePath(file.path(dirname(script_path), "..", "..", ".."), winslash = "/", mustWork = TRUE)
renv::load(file.path(repository_root, "verification", "r"), quiet = TRUE)

output_path <- file.path(repository_root, "verification", "data", "spatial-extremes", "geodesic-distance-oracle.json")

earth_radius_km <- 6371.0088
to_radians <- function(degrees) degrees * pi / 180

haversine_km <- function(lat1, lon1, lat2, lon2) {
  phi1 <- to_radians(lat1); phi2 <- to_radians(lat2)
  dphi <- to_radians(lat2 - lat1); dlambda <- to_radians(lon2 - lon1)
  a <- sin(dphi / 2)^2 + cos(phi1) * cos(phi2) * sin(dlambda / 2)^2
  2 * earth_radius_km * asin(pmin(1, sqrt(a)))
}

# ---------------------------------------------------------------- network (latitude, longitude) and targets
sites <- matrix(c(
  38.90, -77.04,   # Washington, DC
  39.29, -76.61,   # Baltimore
  40.44, -79.99,   # Pittsburgh
  37.54, -77.44,   # Richmond
  41.88, -87.63    # Chicago
), ncol = 2, byrow = TRUE)
targets <- matrix(c(
  39.00, -78.00,
  38.90, -77.04,   # coincides with site 1
  40.00, -75.00,
  36.00, -80.00,
  42.50, -83.00
), ncol = 2, byrow = TRUE)
n <- nrow(sites)

distance_matrix <- matrix(0, n, n)
for (i in seq_len(n)) for (j in seq_len(n)) {
  distance_matrix[i, j] <- haversine_km(sites[i, 1], sites[i, 2], sites[j, 1], sites[j, 2])
}
target_distances <- t(sapply(seq_len(nrow(targets)), function(t) {
  haversine_km(targets[t, 1], targets[t, 2], sites[, 1], sites[, 2])
}))

range_km <- 150
sigma <- 0.2
errors <- c(0.05, -0.03, 0.02, -0.04, 0.01)
correlation <- exp(-distance_matrix / range_km)
diag(correlation) <- 1
K <- sigma^2 * correlation
kriging <- lapply(seq_len(nrow(targets)), function(t) {
  k_star <- sigma^2 * exp(-target_distances[t, ] / range_km)
  alpha <- solve(K, errors)
  v <- solve(K, k_star)
  list(target = as.numeric(targets[t, ]),
       conditional_mean = sum(k_star * alpha),
       conditional_variance = sigma^2 - sum(k_star * v))
})

# Reference checks: DC-Baltimore about 56 km; DC-Chicago about 957 km.
stopifnot(abs(distance_matrix[1, 2] - 56.0) < 2, abs(distance_matrix[1, 5] - 957) < 5)

artifact <- list(
  metadata = list(
    generated = format(Sys.Date(), "%Y-%m-%d"),
    generator = "verification/r/spatial-extremes/generate_geodesic_distance_oracle.R",
    generator_sha256 = digest::digest(file = script_path, algo = "sha256"),
    r_version = R.version.string,
    jsonlite = as.character(packageVersion("jsonlite")),
    digest = as.character(packageVersion("digest")),
    distance_metric = "haversine great-circle distance in kilometres; coordinates (latitude, longitude) in decimal degrees; mean Earth radius 6371.0088 km",
    correlation_function = "exponential rho(h) = exp(-h / range) with range 150 km",
    kriging = "simple kriging of a latent-error field with sigma 0.2 and the recorded errors; dense solve in R",
    tolerances = list(distance_km = 1e-9, conditional_mean = 1e-10, conditional_variance = 1e-10)
  ),
  earth_radius_km = earth_radius_km,
  sites = lapply(seq_len(n), function(i) as.numeric(sites[i, ])),
  targets = lapply(seq_len(nrow(targets)), function(i) as.numeric(targets[i, ])),
  distance_matrix_km = lapply(seq_len(n), function(i) as.numeric(distance_matrix[i, ])),
  target_distances_km = lapply(seq_len(nrow(targets)), function(i) as.numeric(target_distances[i, ])),
  range_km = range_km,
  correlation_matrix = lapply(seq_len(n), function(i) as.numeric(correlation[i, ])),
  error_scale = sigma,
  errors = errors,
  kriging = kriging
)

dir.create(dirname(output_path), recursive = TRUE, showWarnings = FALSE)
jsonlite::write_json(artifact, output_path, auto_unbox = TRUE, pretty = TRUE, digits = NA)
message(sprintf("Wrote %s; DC-Baltimore %.3f km, DC-Chicago %.3f km", output_path, distance_matrix[1, 2], distance_matrix[1, 5]))
