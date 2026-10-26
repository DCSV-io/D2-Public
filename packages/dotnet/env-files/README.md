<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.EnvFiles

Loads `.env*` files into the process environment from the nearest directory that contains one. Written for host-side
runs - a test suite or an IDE debug session - where a container runtime is not already injecting the variables.

## Install

```bash
dotnet add package DcsvIo.D2.EnvFiles
```

## Public API

| Member                          | Does                                                                                     |
| ------------------------------- | ---------------------------------------------------------------------------------------- |
| `D2Env.Load(params string[])`   | Discovers and loads the named files. Defaults to `.env`, `.env.local`, `.env.secrets`.   |

```csharp
// Default - load .env, .env.local, .env.secrets in that order from the
// nearest discovery directory:
D2Env.Load();

// Explicit override - load only what you want, in the order you want:
D2Env.Load(".env.test", ".env.local");
```

## Discovery

`Load` walks up from `AppContext.BaseDirectory`, at most 12 levels, and stops at the first directory containing at
least one of the named files. It then loads every named file from that directory and no other. Files from two
directories are never mixed, so a `.env.secrets` from a higher directory is never paired with a `.env.local` from a
lower one.

## Precedence

1. The process environment takes precedence over every file. A variable already set when `Load` runs - by a container
   runtime, an IDE, or the parent shell - is left unchanged.
2. Within one load, a later file in the list overrides an earlier one. Under the defaults, `.env.secrets` overrides
   `.env.local`, which overrides `.env`. That matches the ordering a Compose `--env-file` list gives, so a host-side run
   and a container run produce the same values for the same files.

## Idempotency

`Load` is safe to call more than once. The second call returns without re-walking the file system.

## Case sensitivity

Collision detection between an existing key and a file key uses the platform comparer: case-insensitive on Windows,
case-sensitive elsewhere. D2 writes env-var names in uppercase, so the difference appears only across operating systems.

## Dependencies

- `dotenv.net` - parses one file into key/value pairs. This package implements the discovery walk and the precedence
  rules.

## Configuration

None. The discovery depth and the default file list are compile-time constants.

## Telemetry

None. `Load` writes no log and emits no metric; it runs before a logging pipeline exists.
