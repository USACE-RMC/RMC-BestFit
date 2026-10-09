# RMC-BestFit Plugin Privacy Policy

This policy covers the RMC-BestFit plugin (`rmc-bestfit`), version 0.3.3,
from the USACE Risk Management Center for use with BestFit 2.0.1. The package
includes the `bestfit-frequency` skill. This policy describes its bundled
instructions, Python helpers, and session-local BestFit API. The plugin supports
statistical analysis, flood-frequency research, and plotting of supplied or
generated results.

## Information processed and purposes

The plugin processes files, study descriptions, observations, location and gage
identifiers, research queries, request bodies, and analysis settings selected for
your task. These inputs can contain personal information, including names,
contact details, or identifying file paths. Provide only information needed for
the analysis and remove unnecessary personal or confidential information.

The workflow uses these inputs to retrieve evidence, prepare and run analyses,
produce plots, and preserve reproducible results. Saved artifacts can include
original inputs, source responses, queries, requests, results, diagnostics,
runtime versions, timestamps, file paths, and logs.

## Where information goes

“Local” means the execution environment running your session. It may be your
computer or a cloud environment supplied by ChatGPT, Codex, Claude, or another
host. It does not mean that processing is always on your device or offline.
Your host processes prompts, selected files, tool inputs, and outputs according
to its own terms, privacy policy, and account settings.

The bundled workflow connects to a BestFit API bound to loopback in that same
environment. Its configuration includes no publisher-operated collection
endpoint, analytics service, or account service.

When research or downloads are requested, information goes to the selected
services. Examples include USGS, StreamStats, NOAA, and other sources selected
for the task. These services receive the relevant URL, query parameters or
request body, and ordinary connection information such as the requester’s IP
address. Setup can contact GitHub, NuGet, PyPI, and .NET distribution services
to obtain source code, packages, or runtimes. These providers and installed
runtimes have their own privacy and telemetry practices.

## Retention and your controls

The API keeps analysis resources in process memory until explicitly deleted
or the process stops. Saved workspace inputs, results, research receipts, and
logs remain until you remove them or the host’s workspace retention rules
remove them. Stopping the API does not delete saved artifacts. Host providers,
research services, and package providers control their own retention, including
any backups and service logs.

Choose the files and services used, review outgoing research requests, and use
an execution environment appropriate for your information. You can stop the
API you started and delete workspace artifacts through your host or filesystem
controls. Use the host’s account controls for chat history, uploaded files,
sharing, and retention; the plugin cannot delete provider-held records.

## Questions and support

For general questions about this policy, use the
[RMC-BestFit issue tracker](https://github.com/USACE-RMC/RMC-BestFit/issues).
Issues and attachments are public; do not include personal information,
credentials, or confidential study data. Information you voluntarily post is
available to GitHub, repository maintainers, and the public. For security
concerns, follow the repository’s
[security reporting instructions](https://github.com/USACE-RMC/RMC-BestFit/blob/main/SECURITY.md)
instead of posting a public issue.
