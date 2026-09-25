# Docker Skills Source

The Docker-related agent skills below use `docker/skills` as their pinned provenance baseline:

- `docker-build-strategies`
- `docker-compose-patterns`
- `docker-destructive-guardrails`

Source repository: https://github.com/docker/skills
Source revision: `ddbf34bfd8be2fed3fe69dddd6c7590b42d45320`
License: Apache License 2.0
Vendored license: `.agents/skills/licenses/docker-skills-APACHE-2.0.txt`

The initial import came from that exact revision. The copies in this repository include review-driven local hardening and documentation corrections on top of the pinned baseline, so they are intentionally not byte-for-byte identical to upstream. Keep those local deltas minimal and reconcile them whenever the pinned revision advances.

Repository-specific rules remain authoritative. For Docker work in this repository, use `docker-compose-container-baseline` for the local .NET/container policy and use the vendored Docker skills as supplemental upstream guidance. When instructions conflict, `AGENTS.md` and `docker-compose-container-baseline` prevail.

Updates are manual: review upstream changes, reconcile the documented local hardening, and advance the pinned source revision intentionally.
