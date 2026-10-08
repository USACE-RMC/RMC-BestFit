# Generate independent R DIC and WAIC oracles for a deterministic Normal model.
# The C# verification project consumes only the committed JSON artifact.

script_path <- normalizePath(sys.frame(1)$ofile, winslash = "/", mustWork = TRUE)
repository_root <- normalizePath(
  file.path(dirname(script_path), "..", "..", ".."),
  winslash = "/",
  mustWork = TRUE
)
output_path <- file.path(
  repository_root,
  "verification",
  "data",
  "model-estimation",
  "model-comparison-oracle.json"
)

observations <- c(2.4, 2.9, 3.5, 4.1, 4.8)
draw_index <- seq_len(40L)
posterior_draws <- cbind(
  mu = 3.5 + 0.35 * sin(0.7 * draw_index) + 0.1 * cos(1.3 * draw_index),
  sigma = 0.85 + 0.075 * (1 + sin(0.5 * draw_index))
)

normal_log_likelihood <- function(parameters) {
  if (length(parameters) != 2L || parameters[[2L]] <= 0) {
    return(-Inf)
  }

  sum(stats::dnorm(
    observations,
    mean = parameters[[1L]],
    sd = parameters[[2L]],
    log = TRUE
  ))
}

total_log_likelihood <- apply(
  posterior_draws,
  1L,
  normal_log_likelihood
)
pointwise_log_likelihood <- t(vapply(
  seq_len(nrow(posterior_draws)),
  function(index) {
    stats::dnorm(
      observations,
      mean = posterior_draws[index, "mu"],
      sd = posterior_draws[index, "sigma"],
      log = TRUE
    )
  },
  numeric(length(observations))
))

# BayesianTools::DIC expects a BayesianOutput with parameter, posterior,
# likelihood, and prior columns. The fixed draws need not be sampled again.
information <- cbind(
  posterior = total_log_likelihood,
  likelihood = total_log_likelihood,
  prior = rep(0, length(total_log_likelihood))
)
bayesian_output <- BayesianTools::convertCoda(
  coda::mcmc(posterior_draws),
  info = information,
  likelihood = normal_log_likelihood
)
dic_result <- BayesianTools::DIC(bayesian_output)

# loo::waic accepts an S-by-N matrix: posterior draws by observations.
waic_result <- loo::waic(pointwise_log_likelihood)
log_mean_exp <- function(values) {
  maximum <- max(values)
  maximum + log(mean(exp(values - maximum)))
}
pointwise_lppd <- apply(
  pointwise_log_likelihood,
  2L,
  log_mean_exp
)
lppd <- sum(pointwise_lppd)

git_revision <- function(repository) {
  result <- system2(
    "git",
    c("-C", shQuote(repository), "rev-parse", "HEAD"),
    stdout = TRUE,
    stderr = TRUE
  )
  if (!identical(attr(result, "status"), NULL) || length(result) != 1L) {
    stop("Unable to determine Git revision for ", repository)
  }
  unname(result[[1L]])
}

artifact <- list(
  metadata = list(
    artifact_id = "ME-MODEL-COMPARISON-001",
    generated_utc = format(Sys.Date(), "%Y-%m-%d"),
    command = "scripts/generate-verification-data.ps1 -Family model-comparison",
    generator = "verification/r/model-comparison/generate_model_comparison_oracle.R",
    generator_sha256 = digest::digest(
      file = script_path,
      algo = "sha256",
      serialize = FALSE
    ),
    bestfit_source_commit = git_revision(repository_root),
    numerics_source_commit = git_revision(file.path(repository_root, "..", "Numerics")),
    r_version = R.version.string,
    bayesian_tools_version = as.character(utils::packageVersion("BayesianTools")),
    loo_version = as.character(utils::packageVersion("loo")),
    coda_version = as.character(utils::packageVersion("coda")),
    jsonlite_version = as.character(utils::packageVersion("jsonlite")),
    fixture = paste(
      "Five exact Normal observations and forty deterministic posterior draws",
      "for location and positive scale"
    ),
    absolute_tolerance = 1e-10,
    references = list(
      bayesian_tools = "https://CRAN.R-project.org/package=BayesianTools",
      loo_waic = "https://mc-stan.org/loo/reference/waic.html"
    )
  ),
  parameter_order = c("mu", "sigma"),
  observations = unname(observations),
  posterior_draws = unname(posterior_draws),
  pointwise_log_likelihood = unname(pointwise_log_likelihood),
  posterior_mean = unname(colMeans(posterior_draws)),
  dic = list(
    convention = "DIC = Dbar + pD = 2*Dbar - Dhat; pD = Dbar - Dhat",
    Dbar = unname(dic_result$Dbar),
    Dhat = unname(dic_result$Dhat),
    pD = unname(dic_result$pD),
    pV = unname(dic_result$pV),
    DIC = unname(dic_result$DIC)
  ),
  waic = list(
    convention = "WAIC = -2*elpd_waic; elpd_waic = lppd - p_waic",
    lppd = unname(lppd),
    elpd_waic = unname(waic_result$estimates["elpd_waic", "Estimate"]),
    p_waic = unname(waic_result$estimates["p_waic", "Estimate"]),
    waic = unname(waic_result$estimates["waic", "Estimate"]),
    standard_error = unname(waic_result$estimates["waic", "SE"]),
    pointwise = lapply(seq_along(observations), function(index) {
      list(
        index = index - 1L,
        lppd = unname(pointwise_lppd[[index]]),
        elpd_waic = unname(waic_result$pointwise[index, "elpd_waic"]),
        p_waic = unname(waic_result$pointwise[index, "p_waic"]),
        waic = unname(waic_result$pointwise[index, "waic"])
      )
    })
  ),
  status = "Independent R package oracle"
)

dir.create(dirname(output_path), recursive = TRUE, showWarnings = FALSE)
jsonlite::write_json(
  artifact,
  output_path,
  pretty = TRUE,
  auto_unbox = TRUE,
  digits = NA
)

cat("Wrote", output_path, "\n")
cat("BayesianTools DIC:", format(dic_result$DIC, digits = 17), "\n")
cat("loo WAIC:", format(waic_result$estimates["waic", "Estimate"], digits = 17), "\n")
