<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.Messaging.RabbitMq

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.5.7 - 2026-10-25
### Changed

- Dependency update: `DcsvIo.D2.Handler` bumped to 0.2.0. That release is API-breaking; this package's own surface is
  unchanged.
- Dependency update: `DcsvIo.D2.Messaging.Abstractions` bumped to 0.2.21. This package's own surface is unchanged.
## 0.5.6 - 2026-10-21
### Changed

- `HandlerDispatcherFactory` documents the exceptions it throws and casts the created dispatcher directly; the
  `ChannelPoolOptions` doc no longer states a throughput figure.
- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.12. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.21. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Encryption` bumped to 0.2.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.32. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Headers.Amqp` bumped to 0.1.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.Abstractions` bumped to 0.2.20. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.10. This package's own surface is unchanged.
## 0.5.5 - 2026-10-20
### Changed

- The exception message that `SealedConsumerStartupCheck` throws for a sealed subscriber with no keyed
  `IPayloadOpener` now asks for an opener backed by a managed keyring in place of naming a specific registration method.
- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.11. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.20. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Encryption` bumped to 0.2.0. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.31. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Headers.Amqp` bumped to 0.1.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.Abstractions` bumped to 0.2.19. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Resilience` bumped to 0.1.17. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.9. This package's own surface is unchanged.
## 0.5.4 - 2026-10-19
### Changed

- Code comments, and the OpenTelemetry instrument descriptions copied from the contract spec, rewritten to the
  repository writing standard; no code or public API shape changed.
- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.10. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.19. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Encryption` bumped to 0.1.20. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.ErrorCodes.Category` bumped to 0.1.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.30. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Headers.Amqp` bumped to 0.1.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.Abstractions` bumped to 0.2.18. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Resilience` bumped to 0.1.16. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.8. This package's own surface is unchanged.
## 0.5.3 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.18. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Encryption` bumped to 0.1.19. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.ErrorCodes.Category` bumped to 0.1.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.29. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Headers.Amqp` bumped to 0.1.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.Abstractions` bumped to 0.2.17. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Resilience` bumped to 0.1.15. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.7. This package's own surface is unchanged.
## 0.5.2 - 2026-10-17
### Changed

- Punctuation in two `InvalidOperationException` messages of `EncryptedBodyComposer`: a dash is now a hyphen.
- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.17. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Encryption` bumped to 0.1.18. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.ErrorCodes.Category` bumped to 0.1.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.28. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Headers.Amqp` bumped to 0.1.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.Abstractions` bumped to 0.2.16. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Resilience` bumped to 0.1.14. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.6. This package's own surface is unchanged.
## 0.5.1 - 2026-10-03
### Changed

- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.16. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.27. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.Abstractions` bumped to 0.2.15. This package's own surface is unchanged.
## 0.5.0 - 2026-09-28
### API-breaking

- `AddD2MessagingRabbitMq` refuses, at composition, a `RabbitMqConnectionOptions.ConnectionUri` whose scheme is not
  `amqps` unless its host is loopback or `AllowUnencryptedTransport` is `true`. The check is an options validation with
  `ValidateOnStart`, so the host does not start, and resolving the connection or the consumer host throws
  `OptionsValidationException`; the dead-letter transport copies the validated options, so the rule covers it too. A
  host that connected with a plain `amqp://` address to another host now fails at startup: switch to `amqps`, or set
  `AllowUnencryptedTransport` where nothing can observe the network path. An address that is not an absolute URI is
  refused as well; an unset address still fails only when the connection starts.
- `RabbitMqConnectionOptions.ConnectionUri` is set-only. The broker address carries the credential, so it is held in a
  private field and read only through `RevealConnectionUri()`; the type, the field and the property are marked
  `[RedactData(Reason = RedactReason.SecretInformation)]`. A caller that read `ConnectionUri` calls
  `RevealConnectionUri()`. Configuration binding cannot fill a set-only property, so the address is set in the
  `configureConnection` delegate.
### Added

- `RabbitMqConnectionOptions.AllowUnencryptedTransport` (default `false`) admits a non-`amqps` broker address to a host that is not loopback.
- `RabbitMqConnectionOptions.RevealConnectionUri()`, the only read of the broker address. A source scan test pins its
  production callers to `RabbitMqConnection` and `DlqRepublisher`, where a broker connection is built.
### Changed

- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.26. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.Abstractions` bumped to 0.2.14. This package's own surface is unchanged.
## 0.4.6 - 2026-09-27
### Changed

- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.15. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Encryption` bumped to 0.1.17. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.25. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.Abstractions` bumped to 0.2.13. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Resilience` bumped to 0.1.13. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.5. This package's own surface is unchanged.
## 0.4.5 - 2026-09-26
### Changed

- Dependency update: `DcsvIo.D2.Encryption` bumped to 0.1.16. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.Abstractions` bumped to 0.2.12. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Resilience` bumped to 0.1.12. This package's own surface is unchanged.
### Fixed

- `ConsumerHostedService` builds its shared shutdown task unstarted, publishes it under the shutdown gate and runs it
  after releasing the gate, and `DefaultTopologyDeclarer` does the same for its shared declaration. Both sequences
  started inside the gate. When nothing before the channels' broker calls had to wait, the cancel, close and dispose
  calls (or the channel request and declarations) therefore ran while the gate was held, and channel tracking was
  blocked until they finished.
## 0.4.4 - 2026-09-26
### Changed

- The architecture document states that an application builds and registers its own `MessagingCatalog`, and no longer
  mentions generated registry types that this package does not emit. Unlabeled fenced blocks in the package documents
  have a `text` tag. The `otel-messaging-tags-source-gen` and `error-codes-source-gen` READMEs describe the current
  contract. No code changed.
- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.14. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Encryption` bumped to 0.1.15. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.24. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.Abstractions` bumped to 0.2.11. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Resilience` bumped to 0.1.11. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.4. This package's own surface is unchanged.
## 0.4.3 - 2026-09-26
### Changed

- Dependency update: `DcsvIo.D2.Encryption` bumped to 0.1.14. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.Abstractions` bumped to 0.2.10. This package's own surface is unchanged.
## 0.4.2 - 2026-09-26
### Changed

- Dependency update: `DcsvIo.D2.Resilience` bumped to 0.1.10. This package's own surface is unchanged.
## 0.4.1 - 2026-09-26
### Changed

- The README, the publisher-and-consumer document and the `ConsumerHostedService` topology parameter doc state that
  consumer startup awaits the one shared topology declaration, which `DefaultTopologyDeclarer` sends once per instance.
  No behavior changed.
## 0.4.0 - 2026-09-14
### Added

- `RabbitMqSubscriberOptions` configures independent `NodaTime.Duration` deadlines for DLQ publication and retained
  cleanup through `AddD2MessagingRabbitMq`.
### Fixed

- Consumer startup completes readiness when shutdown cancels between startup publication and scheduling.
- DLQ publication uses a dedicated connection whose sockets terminate on its deadline or shutdown. A late acquisition is
  still cleaned up by the republisher, mandatory confirmations stay required, and settlement uses the original consumer
  channel.
- Logger failures during DLQ cancellation or disposal no longer interrupt retained resource cleanup. Settlement
  diagnostics cannot restart DLQ publication.
### Changed

- Generated LoggerMessage files omit trailing whitespace; log templates and behavior are unchanged.
## 0.3.13 - 2026-09-11
### Changed

- `SubscriberChannel`, `DefaultTopologyDeclarer`, `ConsumerHostedService`, `SealedConsumerStartupCheck` and the
  publisher-and-consumer document in this package's documentation folder state their facts subject-first. No behavior
  changed.
- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.13. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.23. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.Abstractions` bumped to 0.2.8. This package's own surface is unchanged.
### Fixed

- The README Architecture table's Telemetry row listed six instruments and omitted
  `d2.messaging.rabbitmq.publish_returns`. The row now lists every instrument the meter has and states no
  tally.
## 0.3.12 - 2026-09-10
### Changed

- The packed README contains no outbound link and refers to the architecture, wire-format and publisher-and-consumer
  documents by name instead, because the registry renderer cannot resolve a relative repository path.
- A log call in `RabbitMqConnection` is wrapped under the source line ceiling. No behavior changed.
- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.12. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Encryption` bumped to 0.1.12. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.22. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.Abstractions` bumped to 0.2.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Resilience` bumped to 0.1.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.3. This package's own surface is unchanged.
### Fixed

- `ConsumerHostedService.StartAsync` publishes the background start under the shutdown gate before the work can run,
  rather than launching it first. A `StopAsync` or `DisposeAsync` call that ran in that window read no handle, waited
  for nothing, and released the cancellation source the background start was about to read, which produced a fault
  caused by the lifecycle racing itself. A start that finds the shutdown already drained now begins no work at all.
## 0.3.11 - 2026-09-09
### Changed

- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.11. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.21. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.Abstractions` bumped to 0.2.6. This package's own surface is unchanged.
## 0.3.10 - 2026-09-08
### Changed

- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.10. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Encryption` bumped to 0.1.11. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.20. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.Abstractions` bumped to 0.2.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Resilience` bumped to 0.1.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.2. This package's own surface is unchanged.
## 0.3.9 - 2026-09-06
### Changed

- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Encryption` bumped to 0.1.10. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.19. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.Abstractions` bumped to 0.2.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Resilience` bumped to 0.1.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.0. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.1. This package's own surface is unchanged.
## 0.3.8 - 2026-09-04
### Changed

- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.18. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.Abstractions` bumped to 0.2.3. This package's own surface is unchanged.
## 0.3.7 - 2026-09-03
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.0. That release is API-breaking - `D2Env` moved to
  `DcsvIo.D2.EnvFiles`, which now carries the `dotenv.net` runtime package. This package's own surface is unchanged; a
  consumer that reached `D2Env` through this package's closure adds `DcsvIo.D2.EnvFiles` to get it back.
- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Encryption` bumped to 0.1.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.17. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.Abstractions` bumped to 0.2.2. This package's own surface is unchanged.
## 0.3.6 - 2026-09-03
### Changed

- Depends on `DcsvIo.D2.Caching.Abstractions` 0.5.0, which now takes `DcsvIo.D2.Utilities` for its `Falsey()` key check,
  so that package enters this one's dependency closure.
## 0.3.5 - 2026-09-03
### Changed

- Depends on the caching abstractions at the advanced version; no content change in this package.
## 0.3.4 - 2026-09-02
### Changed

- Depends on the caching packages at their 2026-09-02 patch (the token-bucket
  refill keeps its accrual remainder); no content change in this package.

## 0.3.3 - 2026-09-02
### Changed

- Depends on the caching packages at 0.4.0 (`ConsumeTokenAsync` added to the atomic contract); no content change in this package.
## 0.3.2 - 2026-09-02
### Changed

- Depends on `DcsvIo.D2.Caching.Abstractions` at the advanced version; no content change in this package.
## 0.3.1 - 2026-08-25
### Changed

- A source comment on the transport registration says "a host-declared message type"
  where it said "a product message type". No behavior change.
- Dependency update: DcsvIo.D2.Messaging.Abstractions bumped.
## 0.3.0 - 2026-08-24
### API-breaking

- The transport reads the registered `MessagingCatalog` instead of the static
  generated registry. `RabbitMqMessageBus`, `SealedConsumerStartupCheck`,
  `SubscriberChannel`, `HandlerDispatcherFactory`, `TypedHandlerDispatcher`,
  `ConsumerHostedService` and `DefaultTopologyDeclarer` each take it through
  their constructor.
- `MessageWireResolver`'s static per-type cache, its `RegisterForTesting` pin
  seam and its `ClearCache` reset are removed. A test that needs a fixture
  descriptor builds a `MessagingCatalog` and passes it through the ordinary
  seams. The static cache was keyed by CLR type alone, which two catalogs in one
  process cannot share.
### Added

- `AddD2MessagingRabbitMq` registers `MessagingCatalog.Default` with TryAdd, so a
  host that registered its own catalog keeps it.
## 0.2.1 - 2026-08-22
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Messaging.Abstractions bumped.
## 0.2.0 - 2026-08-20
### Added

- `MessagingErrorCodes` and `MessagingTransportFailures`, generated from the new
  `contracts/messaging-error-codes/messaging-error-codes.spec.json` catalog. It declares
  `LIB_MESSAGING_PUBLISH_UNROUTABLE`, the failure a publish that reached no queue returns.
- `d2.messaging.rabbitmq.publish_returns` counts publish attempts the broker returned.
  `d2.messaging.rabbitmq.publish_failures` and `d2.messaging.rabbitmq.dlq_republish_failures`
  now carry an `outcome` tag separating an unroutable failure from a transport one.
### Changed

- A publish that reaches no queue now fails instead of succeeding. Publishes go out with
  the AMQP `mandatory` flag, so the broker returns a message it cannot route rather than
  dropping it and confirming it. A returned attempt is treated as transient and retried on the
  existing backoff, which makes the cross-host bind race self-healing: `bind-ok` can be
  returned before the exchange index the router reads has been written, and the next attempt
  runs after it has been written. When every attempt is returned, `PublishAsync` returns
  `LIB_MESSAGING_PUBLISH_UNROUTABLE` - a 503 distinct from the `GEN_SERVICE_UNAVAILABLE` an
  unreachable broker gives - and logs the exchange and routing key at error level.
  A service that publishes to an exchange nothing is bound to will now see that publish
  fail. Bind a queue to the exchange, even one nothing drains, or stop publishing to it.
- The consumer-side dead-letter republish goes out `mandatory` on a channel with confirms and confirmation tracking, so
  the original delivery is acked only after the broker has taken the copy. A dead-letter exchange with nothing bound to
  it previously accepted the republish without routing it to any queue, and the ack then destroyed the last copy of the
  message.
## 0.1.21 - 2026-08-20
### Fixed

- Topology is declared once per host instead of twice. `TopologyHostedService` and
  `ConsumerHostedService` both drive `ITopologyDeclarer` on every host start, and the
  two declarations sent two `queue.bind` commands for the same binding. RabbitMQ
  returns `bind-ok` for the second bind from its route table as soon as the first bind's route
  record is committed, while the first bind's exchange-index update runs after that
  commit - so `bind-ok` could return while the exchange still routed nothing to the
  queue. A message published in that window, after the consumer had reported ready,
  was confirmed by the broker and dropped as unroutable, and nothing logged it.
  `DefaultTopologyDeclarer` now declares once and lets the second caller await the
  first declaration.
## 0.1.20 - 2026-08-19
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Encryption bumped.
- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Messaging.Abstractions bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.19 - 2026-08-19
### Changed

- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.
## 0.1.18 - 2026-08-18
### Fixed

- Disposing the consumer hosted service without stopping it no longer leaks every
  subscriber channel it opened. Disposal canceled its cancellation source and returned;
  the AMQP channel and the semaphore behind each subscriber channel were released only
  by the stop path, so a host disposed without a stop leaked all of them.
- Disposal now waits for the background start before it releases the channels, so a
  start still opening channels can no longer hand one to a host that has finished
  disposing. Such a channel was referenced by nobody and could never be closed.
- Disposal now releases its cancellation source even when the source was already
  canceled. The short-circuit that kept a second disposal quiet also skipped the
  release after a normal stop, which is the ordinary lifecycle.
- Stopping and disposing now share one shutdown sequence behind a gate, so the two
  can no longer walk the channel list at the same time, and whichever arrives second
  waits for the first rather than repeating the work.

## 0.1.17 - 2026-08-17
### Added

- The connection now subscribes to the RabbitMQ client's own recovery and shutdown
  callbacks and logs them: connection shutdown by a peer or by the library, recovery
  succeeded, recovery failed, a callback that raised, a consumer being re-attached, a
  queue renamed during recovery, and the broker blocking or unblocking the connection.
  Automatic recovery and topology recovery were already enabled, so the client could
  re-open the connection, re-declare topology and re-attach consumers with nothing in
  the log saying it had happened - a publish issued inside that window reaches no queue
  and a consumer stops receiving, and both were silent.
- A subscriber channel closed by the broker or by the client library is now logged. Its
  only previous symptom was that deliveries stopped.
- A shutdown whose initiator is the application - this process closing its own
  connection or channel - is not logged, so the entries above cover only the shutdowns that nobody
  in the application requested.

## 0.1.16 - 2026-08-17
### Changed

- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.
## 0.1.15 - 2026-08-17
### Changed

- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.
- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- No source in this package changed; the release fingerprint includes the declared version of each direct consumable
  dependency, so the bumps move it.
- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Messaging.Abstractions bumped.
## 0.1.14 - 2026-08-12
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Messaging.Abstractions bumped.
## 0.1.13 - 2026-08-09
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Encryption bumped.
- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Messaging.Abstractions bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.12 - 2026-08-06
### Changed

- Dependency update cascade: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Messaging.Abstractions bumped.
## 0.1.11 - 2026-08-05
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Messaging.Abstractions bumped.
## 0.1.10 - 2026-08-04
### Changed

- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.
- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Encryption bumped.
- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.I18n.Keys bumped.
- Dependency update: DcsvIo.D2.Messaging.Abstractions bumped.
- Dependency update: DcsvIo.D2.Resilience bumped.
- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.9 - 2026-08-03
### Changed

- Dependency update: DcsvIo.D2.Resilience bumped.
## 0.1.8 - 2026-07-28
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Messaging.Abstractions bumped.
## 0.1.7 - 2026-07-26
### Changed

- Keyboard-ASCII punctuation normalization in generated xmldoc comments (em dash, section sign and arrow glyphs replaced
  by `-`, `section` and `->`). Comment text only: no type, member, constant value or wire format changed.
- Subscriber log message text uses `-` instead of an em dash in four messages, including the DLQ warning that the
  `x-d2-failure-reason` header will not survive a re-route. Rendered text only: EventIds, log levels, placeholders and
  routing behavior are unchanged.
- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.
- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Encryption bumped.
- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Headers.Amqp bumped.
- Dependency update: DcsvIo.D2.I18n.Abstractions bumped.
- Dependency update: DcsvIo.D2.I18n.Keys bumped.
- Dependency update: DcsvIo.D2.Messaging.Abstractions bumped.
- Dependency update: DcsvIo.D2.Resilience bumped.
- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.6 - 2026-07-25
### Changed

- Dependency / fingerprint refresh after DcsvIo.D2.Handler null-input ValidationFailed pipeline (no public API change in
  this package).
- Dependency update: DcsvIo.D2.Messaging.Abstractions bumped.
## 0.1.5 - 2026-07-23
### Changed

- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Messaging.Abstractions bumped.
## 0.1.4 - 2026-07-22
### Changed

- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.
- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Encryption bumped.
- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Messaging.Abstractions bumped.
- Dependency update: DcsvIo.D2.Resilience bumped.
- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.3 - 2026-07-20
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Messaging.Abstractions bumped.
## 0.1.2 - 2026-07-17
### Changed

- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.
- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Encryption bumped.
- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Headers.Amqp bumped.
- Dependency update: DcsvIo.D2.I18n.Abstractions bumped.
- Dependency update: DcsvIo.D2.I18n.Keys bumped.
- Dependency update: DcsvIo.D2.Messaging.Abstractions bumped.
- Dependency update: DcsvIo.D2.Resilience bumped.
- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: DcsvIo.D2.Encryption bumped.
- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Messaging.Abstractions bumped.
