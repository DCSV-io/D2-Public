<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.PasswordHashing

Portable **Argon2id** password hashing that emits and verifies standard PHC strings (`$argon2id$v=19$...`). Use this
package whenever an application needs to store password secrets at rest without inventing a hash format.

## Install

```bash
dotnet add package DcsvIo.D2.PasswordHashing
```

## Types

| Type                        | Role                                       |
| --------------------------- | ------------------------------------------ |
| `IPhcPasswordHasher`        | Hash / Verify / NeedsRehash seam           |
| `Argon2idPhcPasswordHasher` | Konscious-backed Argon2id implementation   |
| `PhcPasswordHasherOptions`  | Memory / iterations / parallelism defaults |

## Usage

```csharp
var hasher = new Argon2idPhcPasswordHasher();

var hashResult = hasher.Hash(password);

if (hashResult.Failed)
    return hashResult.BubbleFail<string, MyOutput>();

var phc = hashResult.Data!;
var verify = hasher.Verify(password, phc);

if (verify is { Succeeded: true, Data: true } && hasher.NeedsRehash(phc))
{
    // rehash with current parameters after a successful verify
}
```

## Configuration defaults (`PhcPasswordHasherOptions`)

| Property              | Default | Role                      |
| --------------------- | ------- | ------------------------- |
| `MemoryKib`           | `65536` | Argon2id memory (KiB)     |
| `Iterations`          | `3`     | Time cost                 |
| `Parallelism`         | `1`     | Degree of parallelism     |
| `SaltLength`          | `16`    | Salt bytes                |
| `HashLength`          | `32`    | Derived key bytes         |
| `MAX_PASSWORD_LENGTH` | `128`   | Maximum accepted length   |

Zero-arg `new Argon2idPhcPasswordHasher()` uses these defaults.
The constructor copies and validates the options. Later changes to the supplied options do not change the
hasher's policy.

## Accepted range

Hash and Verify share one range. `PhcPasswordHasherOptions` publishes it as constants, and each of `MemoryKib`,
`Iterations`, `Parallelism`, `SaltLength` and `HashLength` has the matching `[Range]` annotation.

| Constant                           | Value     | Applies to                                                    |
| ---------------------------------- | --------- | ------------------------------------------------------------- |
| `MIN_ACCEPTED_MEMORY_KIB`          | `8`       | `MemoryKib`, and `m=` in a stored PHC string                  |
| `MAX_ACCEPTED_MEMORY_KIB`          | `1048576` | `MemoryKib`, and `m=` in a stored PHC string                  |
| `MIN_ACCEPTED_ITERATIONS`          | `1`       | `Iterations`, and `t=` in a stored PHC string                 |
| `MAX_ACCEPTED_ITERATIONS`          | `16`      | `Iterations`, and `t=` in a stored PHC string                 |
| `MIN_ACCEPTED_PARALLELISM`         | `1`       | `Parallelism`, and `p=` in a stored PHC string                |
| `MAX_ACCEPTED_PARALLELISM`         | `16`      | `Parallelism`, and `p=` in a stored PHC string                |
| `MIN_ACCEPTED_SALT_LENGTH`         | `8`       | `SaltLength`, and the salt segment of a stored PHC string     |
| `MAX_ACCEPTED_SALT_LENGTH`         | `64`      | `SaltLength`, and the salt segment of a stored PHC string     |
| `MIN_ACCEPTED_HASH_LENGTH`         | `16`      | `HashLength`, and the digest segment of a stored PHC string   |
| `MAX_ACCEPTED_HASH_LENGTH`         | `64`      | `HashLength`, and the digest segment of a stored PHC string   |
| `MIN_ACCEPTED_MEMORY_KIB_PER_LANE` | `8`       | `MemoryKib` divided by `Parallelism`, both emitted and stored |

## Dependencies

| PackageId                                | Role                    |
| ---------------------------------------- | ----------------------- |
| `DcsvIo.D2.Result`                       | `D2Result` control flow |
| `DcsvIo.D2.Utilities`                    | shared helpers          |
| `Konscious.Security.Cryptography.Argon2` | Argon2id engine         |

## Telemetry

This package does **not** emit meters or spans - pure hash/verify helpers. Hosts own observability around call sites.

## Notes

- Empty and oversized passwords (maximum length 128) return `ValidationFailed`.
- Verify uses fixed-time comparison of derived digests.
- `NeedsRehash` returns `true` when a valid stored hash has a lower memory, iteration, parallelism, salt-length, or
  digest-length value than the captured policy. Hashes with shorter accepted salts or digests still verify.
- Malformed PHC strings return `Ok(false)` rather than throwing.
- A stored PHC string whose parameters fall outside the accepted range above verifies `Ok(false)`, and
  `NeedsRehash` returns `true` for it. That covers a row holding `m=0`, an absurd memory cost, a salt or digest
  segment too short to be one, and a truncated string with no digest at all.
- `new Argon2idPhcPasswordHasher(options)` throws `ArgumentOutOfRangeException` for options outside the same
  range, so a host cannot configure a hasher whose output its own Verify rejects.
- Verify performs one derivation on every unusable stored string - absent, unparseable, or out of range - under the
  configured cost parameters, so an unknown account costs what a known one whose row carries those same parameters
  costs. A row written before a cost increase verifies under its own lower stored parameters until `NeedsRehash` has
  been acted on for it, and during that window a wrong password against it costs measurably less than an unknown
  account.
- This package has **no** host-application types - only portable PHC helpers.
