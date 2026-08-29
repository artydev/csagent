# Task 2 — Implement 429 Retry/Backoff in LlmClient

**Status:** ⬜ Not started
**Depends on:** Task 1 (`RetryPolicy`)
**Unblocks:** Task 3

---

## Objective

Modify `LlmClient.CompleteChatAsync` (`src/Core/Llm/LlmClient.cs`) so that an HTTP
**429 Too Many Requests** response is retried automatically with exponential
backoff, honoring the server's `Retry-After` header when present.

## Current Behavior

```csharp
var res = await _http.PostAsync($"{_baseUrl}/chat/completions", req, ct);
var raw = await res.Content.ReadAsStringAsync(ct);

if (!res.IsSuccessStatusCode)
    throw new HttpRequestException($"API {(int)res.StatusCode}: {raw}");
```

Any non-2xx status throws immediately — no retry, no backoff.

## Target Behavior

1. Send the request.
2. If the response is **successful (2xx)** → parse and return as today.
3. If the response is **429**:
   - Read the `Retry-After` header if present (seconds, or an HTTP-date).
   - Compute the delay: `Retry-After` if present, otherwise exponential backoff
     from `RetryPolicy` (`BaseDelayMs * BackoffFactor^(attempt-1)`, capped at `MaxDelayMs`).
   - Wait for the delay (respecting the cancellation token).
   - Retry, up to `MaxAttempts` total attempts.
4. If the response is **any other non-2xx status** (401, 403, 500, etc.) → throw
   immediately as today (do **not** retry non-429 errors).
5. If all retry attempts are exhausted → throw with a clear message that includes
   the last status and body.

## Implementation Sketch

Add a `RetryPolicy` field to `LlmClient` and a constructor parameter:

```csharp
private readonly RetryPolicy _retry;

public LlmClient(string apiKey, string baseUrl, string model, RetryPolicy? retry = null)
{
    _model   = model;
    _baseUrl = baseUrl.TrimEnd('/');
    _retry   = (retry ?? RetryPolicy.Default).Validate();
    _http    = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    _http.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", apiKey);
}
```

Rewrite the request loop:

```csharp
public async Task<JsonNode> CompleteChatAsync(
    JsonArray messages,
    JsonArray? tools = null,
    CancellationToken ct = default)
{
    var body = new JsonObject
    {
        ["model"]       = _model,
        ["temperature"] = 0.1,
        ["messages"]    = messages.DeepClone()
    };
    if (tools is { Count: > 0 })
    {
        body["tools"]       = tools.DeepClone();
        body["tool_choice"] = "auto";
    }

    var payload = body.ToJsonString();

    for (int attempt = 1; attempt <= _retry.MaxAttempts; attempt++)
    {
        var req = new StringContent(payload, Encoding.UTF8, "application/json");
        var res = await _http.PostAsync($"{_baseUrl}/chat/completions", req, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);

        if (res.IsSuccessStatusCode)
            return JsonNode.Parse(raw) ?? throw new InvalidDataException("Empty API response");

        // Only retry 429 (rate limit). Other errors fail fast.
        if ((int)res.StatusCode != 429 || attempt == _retry.MaxAttempts)
            throw new HttpRequestException($"API {(int)res.StatusCode}: {raw}");

        var delay = ComputeDelay(res, attempt);
        await Task.Delay(delay, ct);
    }

    throw new HttpRequestException("API retry loop exhausted unexpectedly.");
}

private TimeSpan ComputeDelay(HttpResponseMessage res, int attempt)
{
    // Honor Retry-After if the server provides it (seconds or HTTP-date).
    if (res.Headers.RetryAfter is { } ra)
    {
        if (ra.Delta is { } delta)
            return Clamp(delta);
        if (ra.Date is { } date)
        {
            var diff = date - DateTimeOffset.UtcNow;
            if (diff > TimeSpan.Zero)
                return Clamp(diff);
        }
    }

    // Exponential backoff: BaseDelayMs * factor^(attempt-1), capped at MaxDelayMs.
    var backoff = _retry.BaseDelayMs * Math.Pow(_retry.BackoffFactor, attempt - 1);
    return Clamp(TimeSpan.FromMilliseconds(backoff));
}

private TimeSpan Clamp(TimeSpan t)
{
    var max = TimeSpan.FromMilliseconds(_retry.MaxDelayMs);
    return t > max ? max : t;
}
```

### Notes

- `HttpResponseMessage.Headers.RetryAfter` is a `RetryConditionHeaderValue` with
  `Delta` (TimeSpan?) and `Date` (DateTimeOffset?) — use `System.Net.Http.Headers`.
- The request body is rebuilt per attempt because `StringContent` is single-use.
- `Task.Delay(delay, ct)` lets cancellation abort a wait cleanly.
- Only 429 is retried; 401/403/500 etc. still throw immediately (matches current
  behavior and avoids hammering the API on auth errors).

## Acceptance Criteria

- [ ] 429 responses are retried up to `MaxAttempts` times.
- [ ] `Retry-After` header is honored when present.
- [ ] Exponential backoff with cap is applied when no `Retry-After` header.
- [ ] Non-429 errors throw immediately (no retry).
- [ ] Exhausted retries throw a clear `HttpRequestException` with the last status/body.
- [ ] Cancellation during a backoff wait aborts cleanly.
- [ ] `dotnet build` succeeds.

## Verification

```bash
dotnet build
```

Manual smoke test (see Task 6) will exercise the retry path against a stub or the
real endpoint.

## Definition of Done

- [ ] Code compiles.
- [ ] Retry/backoff logic implemented per the sketch.
- [ ] Behavior documented in the file's XML doc comments.
