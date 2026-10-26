<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - @dcsv-io/d2-messaging-rabbitmq

All notable changes to this package are documented here. Versions follow the
per-package semver + build-free-diff convention.

## 0.7.7 - 2026-10-25
### Changed

- `package.json` has a `repository` field with the URL https://github.com/DCSV-io/D2-Public and this package's folder in it.
- Code comments in the publisher name the .NET file, `RabbitMqMessageBus.cs`, whose counting they match. No code changed.
- Dependency update: `@dcsv-io/d2-encryption` bumped to 0.1.11. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-encryption-abstractions` bumped to 0.1.11. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-headers-amqp` bumped to 0.1.9. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.17. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-messaging-abstractions` bumped to 0.2.5. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-request-context-abstractions` bumped to 0.2.11. This package's own surface is
  unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-telemetry` bumped to 0.1.17. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.11. This package's own surface is unchanged.
## 0.7.6 - 2026-10-21
### Changed

- The publisher no longer counts an unknown message constant or a body compose failure in `publish_failures`, counts
  each send attempt in `publishes` just before the send, and the descriptions and TSDoc of the counters state what
  TypeScript counts.
- Dependency update: `@dcsv-io/d2-encryption` bumped to 0.1.10. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-encryption-abstractions` bumped to 0.1.10. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.16. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-request-context-abstractions` bumped to 0.2.10. This package's own surface is
  unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.7. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-telemetry` bumped to 0.1.16. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.10. This package's own surface is unchanged.
## 0.7.5 - 2026-10-20
### Changed

- The error message that `assertOpenerMatchesDomain` throws for a sealed domain with no sealed opener now refers
  to the managed sealed opener.
- Dependency update: `@dcsv-io/d2-encryption` bumped to 0.1.9. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-encryption-abstractions` bumped to 0.1.9. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-headers-amqp` bumped to 0.1.8. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-messaging-abstractions` bumped to 0.2.4. This package's own surface is unchanged.
## 0.7.4 - 2026-10-19
### Changed

- Code comments, and the OpenTelemetry instrument descriptions copied from the contract spec, rewritten to the
  repository writing standard; no code or public API shape changed.
- Dependency update: `@dcsv-io/d2-encryption` bumped to 0.1.8. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-encryption-abstractions` bumped to 0.1.8. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-headers-amqp` bumped to 0.1.7. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.15. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-messaging-abstractions` bumped to 0.2.3. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-request-context-abstractions` bumped to 0.2.9. This package's own surface is
  unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-telemetry` bumped to 0.1.15. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.9. This package's own surface is unchanged.
## 0.7.3 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-encryption` bumped to 0.1.7. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-encryption-abstractions` bumped to 0.1.7. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-headers-amqp` bumped to 0.1.6. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.14. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-messaging-abstractions` bumped to 0.2.2. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-request-context-abstractions` bumped to 0.2.8. This package's own surface is
  unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.5. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-telemetry` bumped to 0.1.14. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.8. This package's own surface is unchanged.
## 0.7.2 - 2026-10-17

### Changed

- Punctuation in the `Error` messages of `composeBody` for an unknown encryption domain and for a domain with no
  wired composer, and in the description of the `d2.messaging.rabbitmq.ack_failures` counter: a dash is now a
  hyphen.
- Dependency update: `@dcsv-io/d2-encryption` bumped to 0.1.6. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-encryption-abstractions` bumped to 0.1.6. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-headers-amqp` bumped to 0.1.5. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.13. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-messaging-abstractions` bumped to 0.2.1. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-request-context-abstractions` bumped to 0.2.7. This package's own surface is
  unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-telemetry` bumped to 0.1.13. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.7. This package's own surface is unchanged.
## 0.7.1 - 2026-09-28
### Fixed

- A subscription no longer reads the broker address a second time for its
  dead-letter transport. The address is read and checked once per
  `RabbitMqConnectionOptions` instance, and the dead-letter transport connects to
  the address its connection was checked with. Before, options whose
  `revealConnectionUri()` returned a different value on a later call made `subscribe`
  throw after it had registered with the connection, and `connection.close()`
  then never completed.
- A throw inside `subscribe` after it registers with the connection and before
  its consumer is set up, such as from a `store` getter, no longer leaves
  `connection.close()` waiting forever.
- `createConnection` refuses every object not created by
  `new RabbitMqConnectionOptions(...)` with a `TypeError`, as documented. Before,
  a plain object with its own `revealConnectionUri` function was accepted and
  connected.
## 0.7.0 - 2026-09-28
### API-breaking

- `RabbitMqConnectionOptions` is a class, not an interface. The broker address
  contains the credential, so an instance holds it in a private field and
  `revealConnectionUri()` is the only read; `connectionUri` is no longer a
  property of the options. `JSON.stringify`, `structuredClone`, `util.inspect`,
  object spread and a structured log of an instance render no address. Create the
  options with `new RabbitMqConnectionOptions({ connectionUri, ... })`: an object
  literal passed to `createConnection` no longer compiles, and at run time it
  throws a `TypeError` whose message never contains the address. The instance is
  frozen. This is the TypeScript counterpart of the .NET
  `RabbitMqConnectionOptions.RevealConnectionUri()`.
### Added

- `RabbitMqConnectionOptionsInit`, the settings `new RabbitMqConnectionOptions(...)`
  copies: `connectionUri`, `allowUnencryptedTransport`, `clientProvidedName`,
  `retryLowMs`, `retryHighMs` and `heartbeatSeconds`, with the meanings they had on
  the options.
- `RabbitMqConnectionOptions.revealConnectionUri()`, the only read of the broker
  address.
### Changed

- `createConnection` and the dead-letter transport read the address once, check
  its transport, and connect to that same address.
## 0.6.0 - 2026-09-28
### API-breaking

- `createConnection` refuses a `RabbitMqConnectionOptions.connectionUri` whose scheme is
  not `amqps` unless its host is loopback (`localhost`, 127.0.0.0/8 written as four
  decimal octets, or `[::1]`) or `allowUnencryptedTransport` is `true`. It throws
  `UnencryptedTransportError` before any connection attempt, and the error message
  never contains the address. The dedicated dead-letter transport checks its copy of
  the options when the subscriber builds it, so the rule covers it too. A caller that
  connected with a plain `amqp://` address to another host now fails: switch to
  `amqps`, or set `allowUnencryptedTransport` where nothing can observe the network
  path. An address that does not parse as a URL is refused as well, even with the
  flag. This is the TypeScript counterpart of the .NET `RabbitMqConnectionOptions`
  validation.
### Added

- `RabbitMqConnectionOptions.allowUnencryptedTransport` (default `false`; only `true`
  admits) admits a non-`amqps` broker address to a host that is not loopback. It is
  never passed to the vendor client.
- `UnencryptedTransportError`, the error a refused broker address throws.
### Changed

- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.12. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-telemetry` bumped to 0.1.12. This package's own surface is unchanged.
## 0.5.0 - 2026-09-26
### Fixed

- `assertOpenerMatchesDomain` throws for a domain other than `plaintext` that
  resolves to no mode in the framework catalog or `modes`. It passed such a domain
  unchecked, so a host that omitted or misspelled `modes` for an application sealed
  domain passed wire-up with a plaintext opener, while the publisher refuses the same
  domain.
- `assertOpenerMatchesDomain` throws for a symmetric domain wired with an opener that
  is not a `CryptoBodyOpener`. Such a consumer dead-lettered every genuine frame and
  read a cleartext body on the encrypted domain as genuine.
## 0.4.0 - 2026-09-26
### Added

- `CreatePublisherOptions.modes` declares an application's own encrypted domains as
  `sealed` or `symmetric`. Each declared mode fixes the composer that domain's
  `crypto` slot accepts, and the publisher resolves the domain's mode from the same
  map, so a message on an application domain such as
  `audit` now publishes. A domain <!-- public-clone-path:exempt-line released 0.4.0 entry, kept as it shipped -->
  in neither `modes` nor the framework `EncryptionDomainModes` catalog is still
  refused. This is the TypeScript counterpart of .NET `EncryptionDomainModeCatalog`.
- `resolveDomainModes(modes)` merges application domain modes over the framework
  catalog. It throws on an empty domain name, a mode for `plaintext`, a mode other
  than `sealed` or `symmetric`, or a framework domain declared with a mode other than
  its own. `createPublisher` calls it before it opens the confirm-publisher.
- `ApplicationDomainModes`, `DomainCryptoMapOf<TModes>` and `ComposerForMode<M>`
  types, and `CryptoBodyOpener.mode`.
- `assertOpenerMatchesDomain(domain, opener, modes)` takes the application domain
  modes as an optional third argument.
- `publishVia` takes the resolved domain modes as an optional last argument.
### Fixed

- `assertOpenerMatchesDomain` refuses a sealed domain wired with a symmetric
  `CryptoBodyOpener` for that domain, and a symmetric domain wired with a
  `CryptoBodyOpener` of the other mode or for another domain. Each wiring made every
  delivery dead-letter.
## 0.3.1 - 2026-09-15
### Fixed

- `pack:patched` runs `tar` from the archive's or the target's directory with a
  relative archive path, so GNU tar on Windows no longer reads the drive letter as
  a remote host and the pack helper works with whichever `tar` is first on PATH.
## 0.3.0 - 2026-09-14
### API-breaking

- `createConnection` now returns a managed `D2Connection`. Subscriber and publisher
  registration require that handle; the raw vendor `Connection` re-export is removed.
  Awaiting its close stops registered D2 resources before the shared transport.
- `createConnection(options, logger)` requires an explicit `ILogger`. Transport errors
  receive sanitized observation from construction through late close events. Logger
  failures are caught without changing reconnect or close results.
### Added

- Subscriber-specific elapsed publication and cleanup durations with matching .NET
  defaults and validation.
- A guarded `pack:patched` entry point, frozen standalone producer and verified
  bundled rabbitmq-client distribution with its original MIT license.
### Fixed

- Dead-letter work now has one deadline covering admission through confirmation and
  return processing. Timeout terminates dedicated transport and retains late cleanup
  without destroying the shared connection or allocating replacements during cleanup.
- Dead-letter bodies and AMQP metadata are privately captured before callbacks.
  The additive client patch preserves original bytes independently of legacy decoding;
  republishing preserves the original message-id property, not its processing fallback.
- Diagnostic failures during mandatory-return observation or transport cleanup no
  longer interrupt the return outcome or remaining resource cleanup.
## 0.2.4 - 2026-09-11
### Changed

- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.11. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.3. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-telemetry` bumped to 0.1.11. This package's own surface is unchanged.
## 0.2.3 - 2026-09-10
### Changed

- Keyboard-ASCII punctuation normalization in test names and comments. No shipped file changed.
- Test fixtures no longer contain a product service name in their error codes, translation keys and call-path ids;
  they use a neutral placeholder of the same shape. Test text only: no shipped file changed.
- Dependency update: `@dcsv-io/d2-encryption` bumped to 0.1.5. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-encryption-abstractions` bumped to 0.1.5. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.10. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-request-context-abstractions` bumped to 0.2.6. This package's own surface is
  unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-telemetry` bumped to 0.1.10. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.6. This package's own surface is unchanged.
## 0.2.2 - 2026-09-09
### Changed

- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.9. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.1. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-telemetry` bumped to 0.1.9. This package's own surface is unchanged.
## 0.2.1 - 2026-09-06
### Changed

- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.8. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.0. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-telemetry` bumped to 0.1.8. This package's own surface is unchanged.
## 0.2.0 - 2026-08-24
### API-breaking

- `D2Publisher`, `CreatePublisherOptions`, `createPublisher` and
  `PublishableKey` take the catalog as a type parameter, defaulting to the
  catalog this package's `@dcsv-io/d2-messaging-abstractions` dependency ships.
  `CreatePublisherOptions.descriptors` binds it: pass an `as const` catalog and
  its literal `encryption` values drive which keys `publish` accepts. A host
  that binds none can publish nothing, because the framework catalog contains no
  entry.
- `CatalogEncryption` takes the catalog the same way: `CatalogEncryption<K>`
  becomes `CatalogEncryption<K, TCatalog>`, with `K` constrained to
  `keyof TCatalog` and `TCatalog` defaulting to the framework catalog. Bound to
  its default the constraint is `never`, so an application refers to one of its own
  messages by passing its own catalog as the second argument.

### Changed

- Dependency update: `@dcsv-io/d2-messaging-abstractions` 0.1.4 -> 0.2.0, which
  empties the framework catalog.

### Fixed

- `PublishableKeyOf` no longer distributes over a catalog entry whose
  `encryption` is a union. Such an entry is publishable only when every arm is
  `plaintext` or a wired domain; previously one satisfied arm admitted the key.

## 0.1.9 - 2026-08-05
### Changed

- Dependency update: @dcsv-io/d2-request-context-abstractions bumped.

## 0.1.8 - 2026-08-05
### Changed

- Dependency update: @dcsv-io/d2-request-context-abstractions bumped.
## 0.1.7 - 2026-08-04
### Changed

- Dependency update: @dcsv-io/d2-logging bumped.
- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-telemetry bumped.
## 0.1.6 - 2026-08-03
### Changed

- Dependency update: @dcsv-io/d2-encryption bumped.
- Dependency update: @dcsv-io/d2-encryption-abstractions bumped.
- Dependency update: @dcsv-io/d2-headers-amqp bumped.
- Dependency update: @dcsv-io/d2-logging bumped.
- Dependency update: @dcsv-io/d2-messaging-abstractions bumped.
- Dependency update: @dcsv-io/d2-request-context-abstractions bumped.
- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-telemetry bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
### Fixed

- Release fingerprint recomputed: `vitest.config.ts` is no longer hashed as shipped package content. It is test-runner configuration and is not published, so a coverage-threshold change was bumping this package's version and every package depending on it. No shipped file changed.
## 0.1.5 - 2026-07-26
### Changed

- Dependency update: @dcsv-io/d2-encryption bumped.
- Dependency update: @dcsv-io/d2-encryption-abstractions bumped.
- Dependency update: @dcsv-io/d2-headers-amqp bumped.
- Dependency update: @dcsv-io/d2-logging bumped.
- Dependency update: @dcsv-io/d2-messaging-abstractions bumped.
- Dependency update: @dcsv-io/d2-request-context-abstractions bumped.
- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-telemetry bumped.
## 0.1.4 - 2026-07-22
### Changed

- Dependency update: @dcsv-io/d2-logging bumped.
- Dependency update: @dcsv-io/d2-request-context-abstractions bumped.
- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-telemetry bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
## 0.1.3 - 2026-07-20
### Changed

- Dependency update: @dcsv-io/d2-request-context-abstractions bumped.
## 0.1.2 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-encryption bumped.
- Dependency update: @dcsv-io/d2-encryption-abstractions bumped.
- Dependency update: @dcsv-io/d2-headers-amqp bumped.
- Dependency update: @dcsv-io/d2-logging bumped.
- Dependency update: @dcsv-io/d2-messaging-abstractions bumped.
- Dependency update: @dcsv-io/d2-request-context-abstractions bumped.
- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-telemetry bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-encryption bumped.
- Dependency update: @dcsv-io/d2-encryption-abstractions bumped.
- Dependency update: @dcsv-io/d2-headers-amqp bumped.
- Dependency update: @dcsv-io/d2-logging bumped.
- Dependency update: @dcsv-io/d2-messaging-abstractions bumped.
- Dependency update: @dcsv-io/d2-request-context-abstractions bumped.
- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-telemetry bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
## 0.1.0

Initial release: the service-agnostic RabbitMQ consumer runtime - the
TypeScript twin of the .NET `DcsvIo.D2.Messaging.RabbitMq` consumer path.

- `subscribe` / `createConnection` public surface (consumer-only; no publisher).
- Topology declaration matching the .NET contract exactly (primary + `{q}.dlx`
  + `{q}.dlq` + optional retry tiers), byte-identical `DlqNaming`.
- Manual-ack consume with the DLQ republish-with-`DlqFailureMetadata`-then-ack
  failure path (+ NACK-no-requeue fallback).
- The precise idempotency contract over an `IMessageIdempotencyStore`
  seam (+ in-memory 24h-TTL impl), as listed under "What the runtime does" in the README.
- Consume-side context establishment: traceparent-parented `Consumer`-kind span
  + `x-d2-context` (base64url-of-JSON) decoded and applied onto a fresh
  per-message context (identity + `RequestOrigin` never taken from the wire).
- Injectable body-decompose seam (plaintext now; encrypted body with no
  registered opener -> DLQ with `DECRYPT_FAILURE`, never a silent mis-parse).
- Reconnect (rabbitmq-client auto-recovery + aux-topology re-declaration).
