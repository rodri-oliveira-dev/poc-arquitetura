# ADR Guard external consumer evidence

This branch is an isolated pre-release consumer test for ADR Guard issue #49.

The valid workflow exercises:
- the default `docs/adr` path;
- a custom ADR directory;
- repository permissions limited to `contents: read`;
- checkout without persisted credentials;
- read-only `check` behavior;
- an immutable Action source SHA plus explicit runtime version.

The invalid workflow intentionally validates an ADR without a `Decision` section and is expected to fail with `ADR005`.

This is **pre-release evidence only**. The workflow currently pins ADR Guard source commit `f2eb5a749977d925b6cf7602c24340e9e79c58ef` and runtime image `0.1.12`. After ADR Guard publishes a real production Action tag, this branch must be updated to `uses: rodri-oliveira-dev/adr-guard@v1` with no explicit `version` input, rerun, and only then can the external-adoption acceptance criteria be considered complete.
