# Graph Delta and Reconciliation Policy

This document defines the required Microsoft Graph drive-item delta behavior for scan, copy reconciliation, restart, and recovery. It operationalizes `IMPLEMENTATION_CONTRACT.md` without turning the product into synchronization or mirroring.

## Core rules

- Use Microsoft Graph `v1.0` drive-item delta.
- Initial `Scan` starts from the employee business OneDrive root and completes all pages before the scan can succeed.
- A scan must not download employee file content.
- Follow `@odata.nextLink` until Microsoft returns `@odata.deltaLink`.
- Treat both returned URLs as opaque values.
- Never parse, decode, normalize, shorten, edit, rebuild, or concatenate a delta token.
- Persist the active next link or final delta link transactionally in SQLite for the bound source drive.
- Never use a mutable path as the primary identity. Track source items by Drive Item ID and parent Drive Item ID.
- The same item may appear more than once in a delta sequence. Use the last occurrence received before the completed checkpoint.

## Minimum delta item data

Request only fields required by the contract and local state. The final `$select` must be documented and tested. It should include, where supported:

```text
id
name
parentReference
size
createdDateTime
lastModifiedDateTime
eTag
cTag
file
folder
package
deleted
remoteItem
@microsoft.graph.downloadUrl only when intentionally obtaining a fresh download URL
```

Rules:

- Do not require fields Microsoft documents as conditionally absent.
- Do not rely on `parentReference.path` being present in delta responses.
- Do not require `cTag` for folders.
- Preserve unknown properties and enum values safely without treating them as copied content.
- External shortcuts or items that resolve to another drive remain unsupported and are not traversed.

## Initial scan sequence

1. Validate authenticated operator, tenant, employee source, Drive ID, destination, destination binding, lock availability, write access, and storage reserve.
2. Create a new scan identity in SQLite.
3. Call the drive-root delta endpoint without a prior token.
4. Process each page transactionally in bounded batches.
5. Upsert items by Drive Item ID.
6. Classify each item as supported file, supported folder, unsupported package, deleted-source record, external shortcut, or unknown.
7. Apply deterministic path mapping without creating employee file content.
8. Persist each returned next link before requesting the next page.
9. On final delta link, persist the completed checkpoint and calculated scan summary atomically.
10. Mark scan successful only when the complete initial sequence, destination checks, and summary validation succeed.

A partial enumeration must not enable `Start Copy`.

## Paging and crash recovery

- The application must be able to restart from the latest safely persisted next link.
- Page application and checkpoint advancement must be atomic or recoverably ordered so a crash cannot silently skip a page.
- Replaying the last successfully applied page must be idempotent.
- Duplicate item occurrences must not create duplicate local mappings.
- A stale `InProgress` scan becomes interrupted and requires integrity and binding validation before recovery.
- Do not store raw page bodies after successful transactional application.

## Deleted source items

Microsoft Graph represents source deletions with the `deleted` facet.

For this archival-copy product:

- remove the deleted item from the current-source inventory view;
- retain historical local archive content already copied;
- never automatically delete local content because the source was deleted;
- record the source deletion, retained local path, source item ID, and reconciliation run;
- prevent a deleted source item from being counted as currently copied content;
- handle later Drive Item ID reuse or conflicting new content through normal binding and path rules; and
- show the condition in protected reports without exposing protected identifiers unnecessarily.

## Rename and move handling

- Preserve item identity through Drive Item ID.
- Recalculate the deterministic mapped path from the latest hierarchy.
- Update the path mapping transactionally.
- Do not create unexplained duplicates.
- Never overwrite unrelated local content.
- If safe local relocation of already verified archived content is not possible, retain the verified content, record the conflict, and produce the required warning or `Incomplete` state according to content impact.
- A folder rename may not cause descendants to appear individually in delta. Rebuild descendant effective paths from stored parent relationships.

## Delta checkpoint invalidation and HTTP 410

Microsoft can invalidate an old delta token and return `HTTP 410 Gone` with a `Location` header that starts a fresh enumeration.

Required behavior:

1. Verify the response is a supported Graph delta-reset response.
2. Preserve the prior checkpoint and run diagnostics.
3. Persist state `DeltaCheckpointResetRequired`.
4. Follow the returned fresh enumeration location as an opaque URL.
5. Build a new complete source inventory without deleting verified local archive content.
6. Compare the new inventory to the prior current-source state by Drive Item ID.
7. Apply additions, updates, moves, renames, and source deletions transactionally.
8. Persist the new delta link only after the fresh enumeration completes.
9. Continue safe reconciliation or return `Incomplete` when the source cannot be stabilized.

A 410 response is not evidence of SQLite corruption. Do not reset the database, destination binding, local hashes, or archive content.

## Reconciliation after copy

- Run reconciliation from the scan's completed delta checkpoint.
- Apply at most three bounded reconciliation passes, as required by the contract.
- Each pass must reach a new delta checkpoint before it is considered complete.
- Changes to a file already downloaded require metadata revalidation and, when content identity changed, a safe re-download decision.
- Newly discovered supported files must be copied when safe.
- Unsupported content must be reported and makes the run `Incomplete`.
- If changes continue after three passes, preserve safe completed work and return `Incomplete` with source-instability evidence.
- Never present an unstable source snapshot as a complete archive.

## Approved delta states

```text
NoCheckpoint
InitialEnumerationInProgress
InitialEnumerationComplete
DeltaCheckpointValid
DeltaCheckpointResetRequired
FullReenumerationInProgress
ReconciliationInProgress
SourceStable
SourceUnstable
DeltaFailed
```

State transitions must be persisted and tested. `SourceStable` requires a completed delta sequence with no unresolved content-affecting changes for the final accepted pass.

## Error classification

| Condition | Classification | Required behavior |
|---|---|---|
| `401` | Authentication | Controlled reauthentication path; do not treat as a file failure |
| `403` | Authorization or tenant policy | Fail scan or reconciliation with corrective action |
| `404` for source drive | Source invalid or access lost | Revalidate source and authorization; do not silently create a new binding |
| `410` with supported delta reset | Checkpoint reset | Fresh enumeration and reconciliation |
| `429` | Throttling | Use the single retry owner and `Retry-After` |
| `5xx` | Transient or service failure | Bounded retry according to resilience policy |
| Malformed or missing paging link | Protocol failure | Fail safely with protected diagnostics |
| Unknown facet affecting content | Unsupported/unknown | Report and do not claim complete copy |

## Acceptance tests

Automated tests must cover:

- multiple next-link pages;
- empty pages before final delta link;
- exact opaque-link preservation;
- crash after page application but before next request;
- crash before page transaction commit;
- duplicate item occurrences with last occurrence winning;
- missing parent path;
- folder rename without descendant delta entries;
- item move;
- source deletion with retained local content;
- external shortcut rejection;
- package classification;
- unknown facet handling;
- `410 Gone` full re-enumeration;
- fresh-inventory comparison;
- three reconciliation passes;
- stable source result;
- unstable source `Incomplete` result;
- no local deletion caused by source deletion; and
- no checkpoint advancement past unapplied data.

Production acceptance must execute a controlled rename, move, deletion, file change, interruption, resume, and unstable-source case against the test employee OneDrive.

## Official reference

- https://learn.microsoft.com/graph/api/driveitem-delta?view=graph-rest-1.0
