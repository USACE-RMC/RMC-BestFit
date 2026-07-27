# Generate independent R PSIS-LOO and Pareto-diagnostic oracles from the
# deterministic pointwise log-likelihood fixture. The C# verification project
# consumes only committed JSON artifacts.

script_path <- normalizePath(sys.frame(1)$ofile, winslash = "/", mustWork = TRUE)
repository_root <- normalizePath(
  file.path(dirname(script_path), "..", "..", ".."),
  winslash = "/",
  mustWork = TRUE
)
input_path <- file.path(
  repository_root,
  "verification",
  "data",
  "model-estimation",
  "model-comparison-oracle.json"
)
output_path <- file.path(
  repository_root,
  "verification",
  "data",
  "model-estimation",
  "psis-loo-oracle.json"
)

input <- jsonlite::read_json(input_path, simplifyVector = TRUE)
pointwise_log_likelihood <- as.matrix(input$pointwise_log_likelihood)
draw_count <- nrow(pointwise_log_likelihood)
observation_count <- ncol(pointwise_log_likelihood)
relative_efficiency <- rep(1, observation_count)

psis_result <- loo::psis(
  -pointwise_log_likelihood,
  r_eff = relative_efficiency,
  cores = 1
)
loo_result <- loo::loo(
  pointwise_log_likelihood,
  r_eff = relative_efficiency,
  save_psis = TRUE,
  cores = 1
)

normalized_log_weights <- weights(
  psis_result,
  log = TRUE,
  normalize = TRUE
)
unnormalized_log_weights <- weights(
  psis_result,
  log = TRUE,
  normalize = FALSE
)
pareto_k <- loo::pareto_k_values(loo_result)
influence_pareto_k <- loo::pareto_k_influence_values(loo_result)
psis_n_eff <- loo::psis_n_eff_values(loo_result)
diagnostic_threshold <- min(1 - 1 / log10(draw_count), 0.7)
problematic_ids <- as.integer(loo::pareto_k_ids(loo_result)) - 1L
pareto_table <- unclass(loo::pareto_k_table(loo_result))
mcse <- loo::mcse_loo(loo_result)

stopifnot(isTRUE(all.equal(
  unname(pareto_k),
  unname(influence_pareto_k),
  tolerance = 0
)))

# Deterministic standalone importance-ratio regimes exercise bounded, light,
# heavy, non-finite-mean, and degenerate Pareto tails. The permutation prevents
# an implementation from accidentally passing while losing original draw order.
tail_sample_count <- 256L
tail_index <- seq_len(tail_sample_count)
tail_probability <- (tail_index - 0.5) / tail_sample_count
tail_permutation <- (tail_index * 73L) %% 257L

make_tail_case <- function(name, true_shape) {
  excess <- posterior::qgeneralized_pareto(
    tail_probability,
    mu = 0,
    sigma = 1,
    k = true_shape
  )
  log_ratios <- log1p(excess)[tail_permutation]
  result <- suppressWarnings(loo::psis(log_ratios, r_eff = 1))
  estimated_k <- unname(result$diagnostics$pareto_k)
  list(
    name = name,
    true_tail_shape = true_shape,
    log_ratios = unname(log_ratios),
    tail_length = unname(attr(result, "tail_len")),
    pareto_k = if (is.finite(estimated_k)) estimated_k else NULL,
    pareto_k_label = if (is.finite(estimated_k)) "finite" else as.character(estimated_k),
    n_eff = unname(result$diagnostics$n_eff),
    normalized_log_weights = unname(as.numeric(weights(result, log = TRUE, normalize = TRUE))),
    unnormalized_log_weights = unname(as.numeric(weights(result, log = TRUE, normalize = FALSE)))
  )
}

tail_cases <- list(
  make_tail_case("bounded", -0.25),
  make_tail_case("light", 0),
  make_tail_case("moderate", 0.5),
  make_tail_case("high", 0.9),
  make_tail_case("non_finite_mean", 1.2)
)
degenerate_result <- suppressWarnings(loo::psis(rep(0, tail_sample_count), r_eff = 1))
tail_cases[[length(tail_cases) + 1L]] <- list(
  name = "degenerate",
  true_tail_shape = NULL,
  log_ratios = rep(0, tail_sample_count),
  tail_length = unname(attr(degenerate_result, "tail_len")),
  pareto_k = NULL,
  pareto_k_label = as.character(degenerate_result$diagnostics$pareto_k),
  n_eff = unname(degenerate_result$diagnostics$n_eff),
  normalized_log_weights = unname(as.numeric(weights(
    degenerate_result,
    log = TRUE,
    normalize = TRUE
  ))),
  unnormalized_log_weights = unname(as.numeric(weights(
    degenerate_result,
    log = TRUE,
    normalize = FALSE
  )))
)

matrix_rows <- function(value) {
  lapply(seq_len(nrow(value)), function(index) unname(value[index, ]))
}

named_estimates <- function(value) {
  lapply(rownames(value), function(name) {
    list(
      name = name,
      estimate = unname(value[name, "Estimate"]),
      standard_error = unname(value[name, "SE"])
    )
  })
}

artifact <- list(
  metadata = list(
    artifact_id = "ME-PSIS-LOO-001",
    generated_utc = format(Sys.Date(), "%Y-%m-%d"),
    command = "scripts/generate-verification-data.ps1 -Family model-comparison",
    generator = "verification/r/model-comparison/generate_psis_loo_oracle.R",
    generator_sha256 = digest::digest(
      file = script_path,
      algo = "sha256",
      serialize = FALSE
    ),
    input_artifact = "verification/data/model-estimation/model-comparison-oracle.json",
    input_artifact_sha256 = digest::digest(
      file = input_path,
      algo = "sha256",
      serialize = FALSE
    ),
    bestfit_source_commit = input$metadata$bestfit_source_commit,
    numerics_source_commit = input$metadata$numerics_source_commit,
    r_version = R.version.string,
    loo_version = as.character(utils::packageVersion("loo")),
    posterior_version = as.character(utils::packageVersion("posterior")),
    jsonlite_version = as.character(utils::packageVersion("jsonlite")),
    draw_count = draw_count,
    observation_count = observation_count,
    relative_efficiency = 1,
    tolerances = list(
      aggregate = 1e-10,
      pointwise = 1e-10,
      pareto_k = 1e-8,
      log_weights = 1e-8,
      n_eff = 1e-8
    ),
    references = list(
      loo = "https://mc-stan.org/loo/reference/loo.html",
      psis = "https://mc-stan.org/loo/reference/psis.html",
      diagnostics = "https://mc-stan.org/loo/reference/pareto-k-diagnostic.html"
    )
  ),
  psis = list(
    tail_length = unname(attr(psis_result, "tail_len")),
    pareto_k = unname(psis_result$diagnostics$pareto_k),
    n_eff = unname(psis_result$diagnostics$n_eff),
    normalized_log_weights = matrix_rows(normalized_log_weights),
    unnormalized_log_weights = matrix_rows(unnormalized_log_weights)
  ),
  psis_tail_cases = list(
    sample_count = tail_sample_count,
    permutation_multiplier = 73L,
    cases = tail_cases
  ),
  loo = list(
    estimates = named_estimates(loo_result$estimates),
    pointwise_columns = colnames(loo_result$pointwise),
    pointwise = matrix_rows(loo_result$pointwise),
    pareto_k = unname(pareto_k),
    influence_pareto_k = unname(influence_pareto_k),
    n_eff = unname(psis_n_eff),
    diagnostic_threshold = unname(diagnostic_threshold),
    problematic_zero_based_ids = unname(as.list(problematic_ids)),
    pareto_k_table_rows = rownames(pareto_table),
    pareto_k_table_columns = colnames(pareto_table),
    pareto_k_table = matrix_rows(pareto_table),
    mcse_elpd_loo = if (is.na(mcse)) NULL else unname(mcse)
  ),
  status = "Independent R loo 2.10.0 package oracle"
)

dir.create(dirname(output_path), recursive = TRUE, showWarnings = FALSE)
jsonlite::write_json(
  artifact,
  output_path,
  pretty = TRUE,
  auto_unbox = TRUE,
  digits = NA,
  na = "null",
  null = "null"
)

cat("Wrote", output_path, "\n")
cat("elpd_loo:", format(loo_result$estimates["elpd_loo", "Estimate"], digits = 17), "\n")
cat("looic:", format(loo_result$estimates["looic", "Estimate"], digits = 17), "\n")
cat("Pareto k:", paste(format(pareto_k, digits = 17), collapse = ", "), "\n")
