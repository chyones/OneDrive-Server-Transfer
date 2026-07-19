# Download and Integrity Policy

This document defines file-download, partial-file, source-hash, local-hash, timestamp, existing-file, and verification behavior for version 1.

## Download source

- Obtain file content through Microsoft Graph `v1.0` only.
- Use `/drives/{drive-id}/items/{item-id}/content` or a metadata request that returns `@microsoft.graph.downloadUrl`.
- Treat redirect targets and `@microsoft.graph.downloadUrl` values as short-lived, preauthenticated secrets.
- Use the URL immediately.
- Never persist, cache, log, report, emit in evidence, or place the URL in SQLite.
- Never send Graph bearer tokens, cookies, or Graph-specific headers to the temporary host.
- Use a separate unauthenticated HTTP client for temporary-host traffic.

## Streaming and bounded memory

- Stream network content directly into the approved `.partial` file.
- Do not buffer an entire file in memory.
- Keep hashing streaming and bounded.
- Keep at most three file downloads active.
- Do not expose concurrency as a user setting or configuration value.
- Keep cancellation responsive during network reads and hash computation.

## Partial-file rules

The temporary local path is the final mapped path plus `.partial` or another collision-safe application-owned suffix defined by implementation and tests.

- Create the partial file only inside the validated destination containment boundary.
- Persist source item ID, expected size, current partial length, source metadata used for resume, and attempt state in SQLite.
- Never treat a partial file as archived content.
- Never open or append through a reparse-point or hard-link redirection.
- On restart, validate destination binding, SQLite integrity, source identity, mapped path, partial ownership, and current source metadata before resume.
- A partial file without matching valid application state must not be adopted silently.

## Range resume

- Send `Range` to the actual temporary download URL, not to the Microsoft Graph `/content` endpoint.
- Resume only when the server returns `206 Partial Content`.
- Validate `Content-Range` start, end, and total against the local partial length and expected source size.
- Validate that source identity and relevant metadata still match the resume checkpoint.
- If the response is `200 OK`, truncate or replace the partial safely and restart from byte zero.
- If range metadata is absent, contradictory, or unsafe, restart from zero or fail the item according to the remaining retry budget.
- For `416 Range Not Satisfiable`, revalidate partial length and source metadata before deciding to restart or fail.
- Do not concatenate bytes from different source versions.

## Temporary URL expiration

- Do not assume a temporary URL remains valid for the duration of a large file download or across restart.
- When the URL expires, reauthenticate to Graph if required, re-read source metadata, obtain a fresh temporary URL, and continue only when safe.
- Removing a user's permission might not immediately invalidate an already issued temporary URL; therefore authorization must be revalidated before copy scheduling, resume, and protected recovery transitions.
- Clear all in-memory temporary URLs on cancel, sign-out, terminal failure, and application exit.

## Verification sequence

A file can transition to `Completed` only after all required steps succeed:

1. HTTP transfer finishes successfully.
2. Written length equals expected source size.
3. Source item identity is re-read and remains the same Drive Item ID.
4. Relevant source metadata is checked for change.
5. A supported Microsoft source hash is verified when available.
6. A streaming local SHA-256 is calculated and persisted.
7. The final file is committed atomically from the verified partial path.
8. Source timestamps are applied and verified where supported.
9. SQLite state is committed transactionally.

The implementation must define crash-safe ordering so a failure between file replacement and SQLite commit is recoverable without trusting unverified content.

## Microsoft source hashes

Supported source-hash fields are stored separately:

```text
SourceQuickXorHash
SourceSha1Hash
SourceCrc32Hash
SourceHashTypeUsed
SourceHashVerificationResult
```

Rules:

- `quickXorHash` is the preferred Microsoft source hash when available.
- `sha1Hash` and `crc32Hash` may be used only when Microsoft supplies them and the implementation supports them correctly.
- `sha256Hash` from Microsoft Graph is unsupported and must not be used.
- Do not compare Microsoft `quickXorHash`, SHA-1, or CRC32 directly to the local SHA-256.
- Absence of a supported source hash does not itself make the file fail.
- When no comparable Microsoft source hash exists, report metadata-and-size verification separately and do not claim source cryptographic verification.

## Local SHA-256

- Calculate SHA-256 over the completed local bytes using streaming reads.
- Store it in SQLite separately from Microsoft source hashes.
- Before trusting a recovered or existing `Completed` state, revalidate the local SHA-256.
- A mismatch means the local file cannot be validly skipped.
- Do not overwrite unrelated local content while repairing a mismatch.
- Reports may include the local SHA-256 only where approved by the report schema and protected-report policy.

## Existing completed files

Skip an existing local file only when all conditions hold:

- destination binding matches;
- persisted source Drive Item ID matches;
- persisted mapped path matches the approved mapping version;
- source metadata remains compatible with the completed state;
- expected size matches;
- the recorded local SHA-256 recomputes successfully; and
- no unsafe reparse point, hard link, or containment change exists.

Filename, timestamp, or size alone is insufficient proof.

## Atomic finalization

- Finalize the file using a same-volume atomic replacement or rename supported by Windows.
- Revalidate containment immediately before finalization.
- Never replace unrelated existing content.
- If final-path content exists without matching state, use the deterministic conflict policy and report it.
- Preserve the verified partial file when a recoverable finalization failure occurs.
- Do not mark `Completed` before durable state reflects the finalized file.

## Timestamp preservation

- Preserve source `createdDateTime` and `lastModifiedDateTime` where Windows and the source values allow it.
- Apply directory timestamps after child processing.
- Timestamp failure does not invalidate verified file bytes.
- Record each failure and use `CompletedWithWarnings` only when every supported item was copied or validly skipped and no content is missing.
- Do not fabricate or silently clamp invalid timestamps without a documented deterministic rule and warning.

## Source change during download

- Re-read source metadata after content transfer.
- If source identity, size, hash, or relevant change tag proves the content changed during transfer, do not finalize it as the scanned version.
- Preserve safe diagnostics and retry against the latest source state within the approved reconciliation and attempt budgets.
- Avoid duplicate local output when an item is renamed or moved.
- If the source cannot stabilize, return `Incomplete`.

## Unknown and package content

- Package items, including OneNote notebooks, are not downloaded as ordinary files in version 1.
- Unknown facets that might affect content semantics must not be guessed as files.
- Report unsupported or unknown content and produce `Incomplete` unless a fatal condition requires another terminal state.

## Acceptance tests

Automated tests must cover:

- streaming without full-file buffering;
- fixed concurrency maximum;
- partial creation and containment;
- safe `206` resume;
- Range ignored with `200` and restart from zero;
- invalid or mismatched `Content-Range`;
- `416` handling;
- expired URL and fresh URL acquisition;
- no authorization header or cookie on temporary-host request;
- source metadata change during download;
- quickXorHash success and mismatch;
- SHA-1 or CRC32 when present;
- Microsoft `sha256Hash` ignored;
- no source hash available;
- local SHA-256 persistence and recovered-state revalidation;
- corrupted local completed file;
- atomic finalization crash points;
- existing unrelated file conflict;
- timestamp success and warning behavior;
- package and unknown-facet handling; and
- no temporary URL persistence or logging.

## Official references

- https://learn.microsoft.com/graph/api/driveitem-get-content?view=graph-rest-1.0
- https://learn.microsoft.com/graph/api/resources/driveitem?view=graph-rest-1.0
- https://learn.microsoft.com/graph/api/resources/hashes?view=graph-rest-1.0
