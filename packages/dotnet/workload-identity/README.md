<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Spiffe

The single home for the SPIFFE workload-identity grammar used by D2 mutual TLS. Provides the `SpiffeWorkloadIdentity`
value object - a strong-typed representation of the subject-alternative-name a leaf certificate contains and a peer
validator checks: `spiffe://d2.internal/workload/<service>`.

This is a leaf-tier value object: parse, validate, and emit the SPIFFE SAN string. It has no X.509 handles, no
network code, and no framework references. The caller extracts the URI SAN from the certificate.

## Install

```bash
dotnet add package DcsvIo.D2.Spiffe
```

## Public API surface

| Type                                            | Role                                                                                                                                              |
| ----------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| `SpiffeWorkloadIdentity.Create(serviceId)`      | Validates a raw service identifier and returns a `D2Result<SpiffeWorkloadIdentity>`. Rejects null / empty / non-lowercase-DNS-label input.        |
| `SpiffeWorkloadIdentity.Parse(uri)`             | Validates a full SPIFFE URI from a certificate's SAN. Checks scheme, trust domain, path prefix, then delegates service-id validation to `Create`. |
| `SpiffeWorkloadIdentity.FromTrusted(serviceId)` | Skips validation for trusted store-side rehydration. Throws on null/empty. Never use with untrusted input.                                        |
| `SpiffeWorkloadIdentity.Uri`                    | The computed SPIFFE URI: `spiffe://d2.internal/workload/<ServiceId>`.                                                                             |
| `SpiffeWorkloadIdentity.ServiceId`              | The normalized lowercase service identifier (e.g. `orders`, `inventory`).                                                                         |

### Wire-format constants

```csharp
SpiffeWorkloadIdentity.SCHEME           // "spiffe"
SpiffeWorkloadIdentity.TRUST_DOMAIN     // "d2.internal"
SpiffeWorkloadIdentity.WORKLOAD_PATH_PREFIX  // "/workload/"
```

## Validation rules

`Create` and `Parse` enforce the same service-identifier rules:

- Non-null, non-empty, non-whitespace.
- Maximum length: 64 characters.
- Lowercase DNS-label-safe charset: `[a-z0-9-]` only.

`Parse` additionally asserts the full SPIFFE URI shape:

- Scheme is `spiffe`.
- Host is `d2.internal` (the D2 trust domain).
- Path begins with `/workload/`.

Both paths return a generic `D2Result.ValidationFailed` on rejection - a default-deny posture that does not reveal which check failed.

## Design notes

The SPIFFE format lives in this package and nowhere else. These consumers use it:

- The certificate / workload issuer (host issuance path) delegates to `Create` at issuance time, re-mapping the generic `ValidationFailed` to its own invalid-workload-identity error code.
- The shared mTLS peer validator in `DcsvIo.D2.AspNetCore` calls `Parse` to check a presented certificate's URI SAN.

This library has no framework dependency. It references only `DcsvIo.D2.Result` (for `D2Result<T>`) and
`DcsvIo.D2.Utilities` (for `ToNullIfEmpty()` / `ThrowIfFalsey()`), and has no AspNetCore, Kestrel, X.509, or
service-domain dependency.

A workload identity is not PII. It is a service label such as `orders` or `inventory`, which does not identify a person.
The `[RedactData]` attribute is deliberately absent from this type.

## Dependencies

- `DcsvIo.D2.Result` - `D2Result<T>` returned by smart constructors
- `DcsvIo.D2.Utilities` - `ToNullIfEmpty()` / `ThrowIfFalsey()` guards
