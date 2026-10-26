<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# @dcsv-io/d2-time

> **Audience**: backend Node/TypeScript service and BFF engineers who need the same deterministic clock seam and
> temporal storage types as `DcsvIo.D2.Time` (.NET).

Wraps the Temporal API. It provides the `IClock` interface, through which code receives the current time by
injection, and the Category 1 and Category 3 temporal storage types. Mirrors `DcsvIo.D2.Time` (.NET).

## Install

```bash
pnpm add @dcsv-io/d2-time
```

## Overview

All production code reads the current time through `IClock`, never through `Temporal.Now.instant()` directly.
Production binds `IClock` -> `SystemClock`. Tests construct `TestClock` directly and set its time with `advance`
and `setTo`.

The Temporal API (TC39 Stage 3) is the JavaScript equivalent of NodaTime. It provides types such as `Instant`
(an unambiguous UTC time), `PlainDateTime` (wall-clock time with no zone) and `Duration`. Node 22+ ships partial
native support. This package uses `temporal-polyfill` so that behavior is stable across runtimes.

## Public surface

| Type / function      | Purpose                                                                                                                                                                                                                                                                                          |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `IClock`             | Interface with the single method `getInstant(): Temporal.Instant`. Code receives it by injection to read the current time.                                                                                                                                                                       |
| `SystemClock`        | Production `IClock`. Delegates to `Temporal.Now.instant()`.                                                                                                                                                                                                                                      |
| `TestClock`          | Test-only `IClock` with a `now` getter, `advance(Duration)` and `setTo(Instant)`. JavaScript is single-threaded, so the class uses no locking.                                                                                                                                                   |
| `ZonedInstant`       | Category 1 class with the fields `{ instant, ianaIdentifier }`. The smart constructor `ZonedInstant.create(instant, iana)` returns `D2Result<ZonedInstant>`; it validates the IANA identifier and converts it to its canonical name.                                                             |
| `LocalAnchoredEvent` | Category 3 class with the fields `{ scheduledLocal, ianaIdentifier, nextFireUtc? }`. The smart constructor `LocalAnchoredEvent.create(scheduledLocal, iana, nextFireUtc?)` returns `D2Result<LocalAnchoredEvent>`. `computeNextFire()` applies the Temporal `disambiguation: "compatible"` rule. |

## Timestamp categories

Every timestamp MUST be assigned a category at design time. The category determines the storage shape, how to sort
and compare correctly, and how to handle DST.

| # | Category                                          | Storage                                                                                | Custom type                                                                |
| - | ------------------------------------------------- | -------------------------------------------------------------------------------------- | -------------------------------------------------------------------------- |
| 1 | Past instant (optionally with original context)   | `event_at TIMESTAMPTZ` + optional `event_at_zone TEXT NULL`                            | `ZonedInstant` (when context is needed); bare `Temporal.Instant` otherwise |
| 2 | Future fixed instant (JWT exp, session expiry)    | `expires_at TIMESTAMPTZ`                                                               | bare `Temporal.Instant`                                                    |
| 3 | Future local-anchored event (cron-like schedules) | `scheduled_local TIMESTAMP` + `scheduled_zone TEXT` + `next_fire_utc TIMESTAMPTZ NULL` | `LocalAnchoredEvent`                                                       |

To sort by absolute time, always use the UTC `Instant` field, which is zone-agnostic and unambiguous.

To handle DST ambiguity (Category 3 only), call `LocalAnchoredEvent.computeNextFire()`, which applies Temporal's
`disambiguation: "compatible"`. That matches .NET NodaTime's `Resolvers.LenientResolver`. Cross-language wire
parity is enforced by `contracts/temporal/temporal-adversarial.fixture.json`, which both the .NET test suite
(`CrossLanguageTemporalParityTests`) and the TypeScript test suite (`cross-language.test.ts`) load.

## Construction (smart-constructor pattern)

```ts
import { ZonedInstant, LocalAnchoredEvent } from "@dcsv-io/d2-time";
import { Temporal } from "temporal-polyfill";

const zoned = ZonedInstant.create(
  Temporal.Instant.fromEpochMilliseconds(1_700_000_000_000),
  "US/Pacific",
);
// zoned.success === true; zoned.data!.ianaIdentifier === "America/Los_Angeles"

const bad = ZonedInstant.create(Temporal.Now.instant(), "Invalid/Zone");
// bad.success === false; bad.inputErrors[0].errors[0].key === "common_time_INVALID_IANA_IDENTIFIER"

const evt = LocalAnchoredEvent.create(
  Temporal.PlainDateTime.from("2026-11-01T01:30:00"),
  "America/New_York",
);
const fire = evt.data!.computeNextFire();
// On US DST fall-back, fire.data === Instant 2026-11-01T05:30:00Z (earlier of the ambiguous pair).
```

`create` does not re-validate the `PlainDateTime`. Temporal's parser throws `RangeError` for invalid calendar dates (Feb
30, Feb 29 in a non-leap year, hour 24, and similar) at the `Temporal.PlainDateTime.from(...)` call site, BEFORE
`create` is reached. The package's tests document the throw behavior exhaustively. Temporal silently CLAMPS seconds = 60
to 59, unlike .NET NodaTime, which throws. Both reject leap seconds, but with different error modes. This is a
documented contract divergence.

## TestClock usage

```ts
import { TestClock } from "@dcsv-io/d2-time";
import { Temporal } from "temporal-polyfill";

const clock = new TestClock(Temporal.Instant.from("2026-01-15T12:00:00Z"));
const sut = new SomethingThatUsesClock(clock);

await sut.doThing();
clock.advance(Temporal.Duration.from({ minutes: 5 }));
await sut.doOtherThing();

clock.setTo(Temporal.Instant.from("2030-01-01T00:00:00Z"));
await sut.doLateThing();
```

Never register `TestClock` in production composition roots.

## Temporal-API note

Node 22+ has partial native Temporal support (behind a flag in some
releases). `temporal-polyfill@0.3.2` provides stable, complete coverage
across runtimes.

## Telemetry

No telemetry surface. The package emits no spans or metrics itself; consumers instrument with their own OTel setup.

## Configuration

No configuration. Consumers register `IClock -> SystemClock` in their own service composition root.

## Dependencies

- `temporal-polyfill` (npm)
- `@dcsv-io/d2-i18n-keys`: the generated TK constant catalog. The `create` factories use its `TK.common.errors.*` and
  `TK.common.time.*` keys.
- `@dcsv-io/d2-result`: `D2Result<T>`, `InputError` and the TK helpers returned by the `create` factories.
