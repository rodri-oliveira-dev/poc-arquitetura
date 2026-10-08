# ADR Guard external consumer verification

This isolated test branch verifies the published `rodri-oliveira-dev/adr-guard@v1` GitHub Action for ADR Guard issue #49. It does not modify the consumer repository's `main` branch.

The valid workflow covers the default `docs/adr` directory, an explicit custom ADR directory, `contents: read` permissions, checkout without persisted credentials, and read-only `check` behavior.

The invalid workflow intentionally validates an ADR without a `Decision` section and **must fail** with diagnostic `ADR005` and a GitHub file annotation. A failing invalid-workflow run is the expected test outcome, not a regression.

The Action is consumed through the published moving major tag `@v1`, without an explicit runtime `version` input. Review the resulting Actions runs and capture their URLs in ADR Guard issue #49 before declaring the independent-consumer acceptance criterion complete.
