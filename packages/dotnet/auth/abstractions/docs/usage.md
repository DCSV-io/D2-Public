<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Auth.Abstractions - usage and edge cases

For a developer calling [`DcsvIo.D2.Auth.Abstractions`](../README.md): worked examples of the role authority order,
the session-liveness cache key and the forwarded-JWT holder, and how each type responds to input it refuses.

## Usage examples

```csharp
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Auth.Abstractions.Sessions;
using DcsvIo.D2.Result;

// Authority over another member: an Officer outranks an Agent, and no role outranks itself.
bool officerOverAgent = RoleAuthority.Outranks(Role.Officer, Role.Agent); // true
bool ownerOverOwner = RoleAuthority.Outranks(Role.Owner, Role.Owner);     // false

// The session-liveness key a revocation broadcasts, composed and read back.
Guid sessionId = Guid.NewGuid();
string key = SessionLivenessCacheKey.Compose(SessionLivenessCacheKey.DEFAULT_PREFIX, sessionId);
bool parsed = SessionLivenessCacheKey.TryParse(key, SessionLivenessCacheKey.DEFAULT_PREFIX, out Guid parsedId);
// parsed is true and parsedId equals sessionId.

// Holding an inbound bearer for an outbound hop. rawBearer is a JWT that already passed
// inbound validation, without its "Bearer " prefix.
D2Result<ForwardedJwt> created = ForwardedJwt.Create(rawBearer);
// created.Success is false for a null, empty or whitespace bearer.

IForwardedJwtAccessor accessor = new MutableForwardedJwtAccessor();
accessor.Capture(rawBearer);
string shown = accessor.Current?.ToString() ?? string.Empty; // "[REDACTED: ForwardedJwt]"
```

## Edge cases

- `RoleAuthority.RankOf` throws `ArgumentOutOfRangeException` for a value outside `Role`. `TryRankOf` returns `false`
  for that value, and `Outranks` returns `false` when it appears on either side.
- `ForwardedJwt.Create` refuses a null, empty or whitespace bearer with a `ValidationFailed` result and does not check
  the JWT format. It holds the bearer exactly as given, untrimmed.
- `ForwardedJwt.RevealForForwarding()` throws `InvalidOperationException` on a default `ForwardedJwt`; `HasValue`
  reports whether a wrapper holds a token.
- `MutableForwardedJwtAccessor.Capture` with a blank bearer stores nothing and leaves an earlier capture in place. A
  second valid capture in the same scope replaces the first, and `Clear()` removes it.
- `SessionLivenessCacheKey.Compose`, `HasPrefix` and `TryParse` throw `ArgumentNullException` for a null prefix and
  `ArgumentException` for an empty or whitespace one. `HasPrefix` and `TryParse` return `false` for a null key, and
  `TryParse` accepts only the 32-character `N` rendering, so a hyphenated or brace-wrapped id is refused.
