<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Usernames

Portable **friendly username** generator that samples adjective + noun + numeric suffix
into a lowercase canonical form and a PascalCase display form. Use this package whenever
an application needs human-readable handles without inventing word lists or RNG rules.

## Install

```bash
dotnet add package DcsvIo.D2.Usernames
```

## Types

| Type                               | Role                                                               |
| ---------------------------------- | ------------------------------------------------------------------ |
| `FriendlyUsername`                 | Result record (`Username` lowercase, `DisplayUsername` PascalCase) |
| `FriendlyUsernameGenerator`        | Pure `GenerateFriendlyUsername` entry point                        |
| `FriendlyUsernameGeneratorOptions` | Optional list / suffix / word-length overrides                     |
| `DefaultUsernameAdjectives`        | Built-in adjective list                                            |
| `DefaultUsernameNouns`             | Built-in noun list                                                 |

## Usage

```csharp
var result = FriendlyUsernameGenerator.GenerateFriendlyUsername();

if (result.Failed)
    return result.BubbleFail<FriendlyUsername, MyOutput>();

var friendly = result.Data!;
// friendly.Username        -> "ableactive42"  (store / unique key)
// friendly.DisplayUsername -> "AbleActive42"  (optional UI form)
```

Hosts that require uniqueness (e.g. insert with unique constraint) should **retry**
generation - this package never talks to a database.

## Configuration defaults (`FriendlyUsernameGeneratorOptions`)

| Property        | Default       | Role                             |
| --------------- | ------------- | -------------------------------- |
| `Adjectives`    | built-in list | Override adjective list          |
| `Nouns`         | built-in list | Override noun list               |
| `MinSuffix`     | `1`           | Inclusive minimum numeric suffix |
| `MaxSuffix`     | `999`         | Inclusive maximum numeric suffix |
| `MaxWordLength` | `12`          | Max length of each override word |

Zero-arg `GenerateFriendlyUsername()` uses the built-in lists and defaults.

## Dependencies

| PackageId             | Role                      |
| --------------------- | ------------------------- |
| `DcsvIo.D2.Result`    | `D2Result` control flow   |
| `DcsvIo.D2.Utilities` | `Falsey` / shared helpers |

## Telemetry

This package does **not** emit meters or spans - pure generation helpers. Hosts are responsible for
observability around call sites (retry counts, clash exhaustion, etc.).

## Notes

- Uses `RandomNumberGenerator.GetInt32` (cryptographic RNG), not `System.Random`.
- Empty override lists, invalid suffix ranges, or words over `MaxWordLength` return
  `ValidationFailed` (no throw on invalid config).
- Built-in lists are hand-authored constants; the default suffix range is 1-999.
- This package has **no** types from a host application - only portable generation helpers. It does not check a
  generated handle for uniqueness or retry on a clash.
