# Microsoft Graph Resiliency Policy

This document defines retry ownership, throttling handling, transient-failure behavior, correlation, cancellation, and retry evidence for Microsoft Graph and temporary file-download hosts.

## Single retry owner

Exactly one layer must own automatic retry for each request category.

Approved model:

- Microsoft Graph requests: one configured Graph SDK retry handler or one application retry policy, never both independently.
- Temporary download-host requests: one application-owned download retry policy with no Graph authentication middleware.
- SQLite operations: database-specific bounded busy/lock handling, separate from HTTP retry.

If the Microsoft Graph SDK retry handler is enabled, application services must not wrap the same request in an additional transparent retry loop. Any higher-level recovery attempt must account for the SDK attempts and remain inside the total approved budget.

## Retry budgets

- Maximum transfer attempts per file: five, matching the binding contract.
- Maximum simultaneous downloads: three, fixed and not configurable.
- Authentication retries: no blind retry loop; use one controlled token renewal or interactive-authentication transition when appropriate.
- Source and destination validation failures: no automatic retry unless the exact failure is classified transient.
- Delta page requests: bounded per-request retries; checkpoint recovery handles process interruption separately.
- A retry budget is persisted or observably counted so restart cannot create an unbounded hidden loop.

## Retryable conditions

Retry only when safe and supported, including:

- `408 Request Timeout`;
- `429 Too Many Requests`;
- `500 Internal Server Error` when the operation is idempotent;
- `502 Bad Gateway`;
- `503 Service Unavailable`;
- `504 Gateway Timeout`;
- connection reset, DNS, TLS, or socket interruption classified as transient;
- expired or invalid short-lived download URL after obtaining a fresh URL; and
- incomplete temporary-host response where the partial file and range metadata remain safe.

Do not retry automatically:

- invalid employee input;
- unsupported source type;
- tenant mismatch;
- unauthorized operator;
- missing permanent consent;
- destination binding mismatch;
- path safety failure;
- unsupported package content;
- corrupted SQLite state;
- invalid schema version;
- insufficient storage reserve;
- deterministic collision or path failure requiring a final item result;
- checksum mismatch without a fresh metadata and content decision; or
- any Microsoft 365 write operation, which is prohibited.

## `Retry-After`

For `429` and any response containing a valid `Retry-After`:

1. stop immediate retries;
2. use the server-provided delay;
3. keep cancellation responsive during the delay;
4. retry only after the delay and inside the remaining budget;
5. record the sanitized status, delay, attempt number, request IDs, and outcome; and
6. avoid scheduling additional avoidable requests against the throttled category.

When no `Retry-After` is provided for a retryable condition, use bounded exponential backoff with jitter. Exact minimum, maximum, and jitter behavior must be centralized, deterministic under tests, and absent from the UI.

## HTTP 401 and 403

### 401 Unauthorized

- Do not treat 401 as ordinary throttling.
- Attempt the approved MSAL silent token path once when appropriate.
- If interaction is required, transition to `ReauthenticationRequired`.
- Resume only after tenant, operator allowlist, source, destination, and current operation state are revalidated.
- Never expose token contents or raw claims.

### 403 Forbidden

- Classify as missing access, consent, Conditional Access, tenant policy, or another authorization failure.
- Do not repeatedly retry 403 as a transient file error.
- Present a stable corrective-action error.
- Preserve accurate transfer state and safe partial files.

## HTTP 410

- For drive-item delta, use the reset and full-reenumeration process in `docs/GRAPH_DELTA_AND_RECONCILIATION_POLICY.md`.
- Do not classify a supported delta reset as database corruption.
- For other endpoints, treat 410 according to the current endpoint documentation and fail safely when no approved recovery exists.

## HTTP 503 and connection reuse

- Apply bounded backoff.
- Allow the HTTP stack to establish a fresh connection for the retry when the prior connection is suspect.
- Do not globally disable connection pooling, TLS validation, proxy handling, antivirus, firewall, or endpoint security.
- Preserve request idempotency and do not duplicate state transitions.

## Download-host retry rules

- Use a dedicated HTTP client without Graph bearer tokens, cookies, or Graph middleware.
- A temporary URL can expire; obtain a fresh URL through Graph instead of repeatedly retrying the expired URL.
- Range resume is accepted only with valid `206 Partial Content` and matching `Content-Range`.
- `200 OK` in response to a Range request requires restart from byte zero.
- `416 Range Not Satisfiable` requires revalidation of local partial size and current source metadata before restart or failure.
- Do not append to a partial file unless range and source identity are proven.
- Keep completed verified files immutable during retry of unrelated items.

## Cancellation

- Cancellation must interrupt retry delays promptly.
- Stop scheduling new requests.
- Cancel supported active requests.
- Preserve completed files and safe partial files.
- Persist exact item and run states.
- Do not convert cancellation into `Failed` unless an independent fatal state-persistence failure prevents safe cancellation.

## Correlation and protected diagnostics

For each Graph request, generate and record:

```text
ClientRequestId
MicrosoftRequestId
ResponseDateUtc
EndpointCategory
HttpMethod
SanitizedEndpointTemplate
HttpStatusCode
GraphErrorCode
AttemptNumber
RetryAfterSeconds
ElapsedMilliseconds
Outcome
```

Do not record:

```text
AccessToken
RefreshToken
AuthorizationHeader
Cookie
RawGraphResponse
TemporaryDownloadUrl
FullProtectedIdentifiers
EmployeeContent
```

The user-facing error contains only a stable reference code, plain-language explanation, and corrective action.

## Throttling-aware request behavior

- Use delta instead of repeated full polling after the initial inventory.
- Use `$select` to reduce response payload.
- Do not issue duplicate metadata requests when valid state already contains the required fields.
- Do not use JSON batching unless a separately reviewed implementation proves it preserves per-request throttling, retry, correlation, and evidence semantics.
- A fixed concurrency of three is a maximum, not a requirement to keep three requests active under throttling or resource pressure.

## Test requirements

Automated tests must cover:

- SDK retry enabled with no duplicate application retry;
- custom retry enabled with SDK automatic retry disabled or accounted for;
- `429` with integer `Retry-After`;
- retryable response without `Retry-After`;
- exponential backoff and jitter bounds;
- cancellation during delay;
- `401` silent renewal and interaction-required behavior;
- `403` no-retry authorization failure;
- `503` bounded retry;
- connection interruption;
- expired temporary download URL and fresh URL acquisition;
- Range `206`, ignored Range `200`, and invalid `Content-Range`;
- attempt budget persistence across restart;
- no Graph credentials on temporary-host requests;
- correlation-ID logging; and
- secret and URL redaction.

## Official references

- https://learn.microsoft.com/graph/throttling
- https://learn.microsoft.com/graph/best-practices-concept
- https://learn.microsoft.com/graph/api/driveitem-get-content?view=graph-rest-1.0
