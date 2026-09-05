# Security

Security fixes target the latest released version. Until 1.0, command-line and
generated-code behavior may change between minor releases.

Report suspected vulnerabilities using GitHub's **Security → Report a vulnerability**
on this repository. Do not put credentials, private datasets, or exploit details
in a public issue. Include a minimal synthetic input and the dottype version.

dottype reads files locally and does not upload data or collect telemetry.
Generated source contains source field names; those names may themselves be
sensitive, so review generated code before sharing it. Errors can include input
paths or field names. Review logs before posting them publicly.

JSON/CSV inference and compressed or piped Parquet use memory proportional to
decompressed data. Use OS-level memory, CPU, and filesystem limits when processing
untrusted input. Generated source is data-derived code and should be reviewed
before compiling. Keep dottype and the .NET runtime updated.
