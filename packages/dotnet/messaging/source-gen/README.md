<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Messaging.SourceGen

> Parent: [`packages/dotnet/`](../../README.md)

**Input contracts:** [`contracts/mq-messages/`](../../../../contracts/mq-messages/README.md) +
[`contracts/mq-subscriptions/`](../../../../contracts/mq-subscriptions/README.md)

Roslyn incremental source generator that emits the messaging registry types by reading spec files from `contracts/`.
It dispatches on the consuming assembly name: `DcsvIo.D2.Messaging.Abstractions` receives `MqMessages`,
`MqMessagesRegistry`, `MqSubscriptions` and `MqSubscriptionsRegistry` in the `DcsvIo.D2.Messaging` namespace. Another
assembly receives the same types only when its project sets the MSBuild properties `D2MqExtensionNamespace` and
`D2MqExtensionClassPrefix` and declares each as a `CompilerVisibleProperty` item. `D2MqExtensionNamespace` sets the
namespace, and `D2MqExtensionClassPrefix` sets the identifier placed before each type name. Those types cover the union
of every spec file the assembly lists in AdditionalFiles and are written to the prefixed `MqMessages` and
`MqSubscriptions` names followed by `.g.cs`. The property `D2MqExtraEncryptionDomainsType` names a type by its metadata
name whose string constants a message entry may also use as its encryption domain, and it reaches the generator
only when the project also declares it as a `CompilerVisibleProperty` item; an undeclared property or a name the
compilation does not resolve adds nothing. The public assembly ignores these properties, and an assembly that sets
neither `D2MqExtensionNamespace` nor `D2MqExtensionClassPrefix` receives none of these types.

The generator reads:

- `mq-messages.spec.json` - every message type the platform publishes (constant name, runtime descriptor, encryption
  requirements)
- `mq-subscriptions.spec.json` - every subscription declaration (consumer name, message types, retry policy, prefetch,
  idempotency)

The spec files are the single source of truth. Adding a message or a subscription means adding one entry to a JSON file.
On the next build the generator emits the constant, the descriptor and the registry entry, and consuming code compiles
against typed identifiers (`MqMessages.UserCreated` instead of `"user.created"`).

Convention: spec-driven Roslyn `IIncrementalGenerator` (netstandard2.0 analyzer; spec via `<AdditionalFiles>`).

---

## Build-time diagnostics

| ID        | Severity | Trigger                                                                                                                                                                                                                        |
| --------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `D2MQ001` | Error    | Spec file is malformed JSON or violates the schema                                                                                                                                                                             |
| `D2MQ002` | Error    | Spec entry missing a required field (e.g. messages-entry missing `constant`)                                                                                                                                                   |
| `D2MQ003` | Error    | Two messages or subscriptions declare the same constant name                                                                                                                                                                   |
| `D2MQ004` | Error    | Encryption domain referenced by a messages-entry is declared by none of the encryption-domain catalogs the consuming assembly references (detects a mismatch between encryption keyring registration and message declarations) |
| `D2MQ005` | Error    | `plaintext`-encryption messages-entry missing the required `encryptionReason` justification                                                                                                                                    |
| `D2MQ006` | Error    | Subscription `pattern` value isn't in `{CompetingConsumer, FanoutExclusiveAutoDelete, DurableShared}`                                                                                                                          |
| `D2MQ007` | Error    | Messages-entry `messageType` value isn't in the recognized set                                                                                                                                                                 |
| `D2MQ008` | Error    | Subscription `exchangeType` value isn't in `{fanout, topic, direct}`                                                                                                                                                           |
| `D2MQ009` | Error    | No `mq-messages.spec.json` found in `AdditionalFiles`                                                                                                                                                                          |
| `D2MQ010` | Error    | No `mq-subscriptions.spec.json` found in `AdditionalFiles`                                                                                                                                                                     |
| `D2MQ011` | Error    | Messages-entry `constant` violates C#-identifier rules (must be `^[A-Z][A-Za-z0-9]*$`)                                                                                                                                         |
| `D2MQ012` | Error    | Subscription `tieredRetry.tiers[*]` value isn't a valid `HH:MM:SS` `TimeSpan` literal                                                                                                                                          |
| `D2MQ013` | Error    | The same `constant` is declared in two spec files of one catalog                                                                                                                                                               |
| `D2MQ014` | Error    | `D2MqExtensionNamespace` is not a dot-separated sequence of C# identifiers                                                                                                                                                     |
| `D2MQ015` | Error    | `D2MqExtensionNamespace` is set and `D2MqExtensionClassPrefix` is absent or not a C# identifier                                                                                                                                |
| `D2MQ016` | Warning  | `D2MqExtensionClassPrefix` is set and `D2MqExtensionNamespace` is not; nothing is emitted                                                                                                                                      |

---

## Spec format - messages

```json
{
  "messages": [
    {
      "constant": "UserCreated",
      "messageType": "Event",
      "encryption": "payload-fixture-sealed",
      "defaultRoutingKey": "user.created"
    },
    {
      "constant": "PaymentReceiptIssued",
      "messageType": "Event",
      "encryption": "plaintext",
      "encryptionReason": "Receipt is downstream-published to billing-public exchange; payment details intentionally exposed to external auditors per SOC 2 retention policy.",
      "defaultRoutingKey": "billing.payment.receipt"
    }
  ]
}
```

- **`constant`**: PascalCase C# identifier; becomes `MqMessages.UserCreated` etc.
- **`messageType`**: one of the recognized message-type tokens (validated by `D2MQ007`).
- **`encryption`**: lowercase wire value of an `EncryptionDomain` constant, or the `plaintext` sentinel. The accepted
  set is the union of the encryption-domain catalogs the consuming assembly references, so it differs by target: an
  assembly referencing only the framework catalog accepts what
  [`contracts/encryption-domains/encryption-domains.spec.json`](../../../../contracts/encryption-domains/encryption-domains.spec.json)
  declares - `plaintext` and the `payload-fixture-sealed` fixture domain - while an application's own catalog assembly
  also accepts the domains its own encryption-domain catalog declares. A value outside that union surfaces as `D2MQ004`.
- **`encryptionReason`**: required if and only if `encryption == "plaintext"`. Records why the payload is intentionally
  unencrypted (audit trail).
- **`defaultRoutingKey`**: optional default routing key for publishers.

## Spec format - subscriptions

```json
{
  "subscriptions": [
    {
      "constant": "OrdersConsumer",
      "messageTypes": ["UserCreated", "RoleAssigned"],
      "pattern": "CompetingConsumer",
      "exchangeType": "topic",
      "routingKeyBinding": "orders.*",
      "prefetch": 50,
      "idempotency": true,
      "tieredRetry": {
        "tiers": ["00:00:05", "00:01:00", "00:15:00"],
        "maxAttempts": 5
      }
    }
  ]
}
```

---

## Emitted output

The generator emits `MqMessages.g.cs` and `MqSubscriptions.g.cs` into the consuming assembly from the spec files that
assembly lists. For `DcsvIo.D2.Messaging.Abstractions` those are the framework spec halves:

```csharp
// MqMessages.g.cs
public static partial class MqMessages
{
    public const string UserCreated = "UserCreated";
    public const string PaymentReceiptIssued = "PaymentReceiptIssued";
    // ...
}

public static partial class MqMessagesRegistry
{
    public static readonly IReadOnlyDictionary<string, MqMessageDescriptor> ByConstant = ...;
}

// MqSubscriptions.g.cs
public static partial class MqSubscriptions { /* constants */ }
public static partial class MqSubscriptionsRegistry { /* dict */ }
```

`MqMessagesRegistry.ByConstant[name]` returns the full descriptor (`messageType`, `encryptionReason`,
`defaultRoutingKey`, etc.). `MessagingCatalog.Default` wraps this dictionary, and `DcsvIo.D2.Messaging.RabbitMq` resolves
the descriptor of a message type through the registered `MessagingCatalog` at publish time to look up encryption and
routing settings, so no parallel hand-written table exists.

One generator emits both files. Publishers use the `MqMessages*` constants and subscribers use the `MqSubscriptions*`
constants. Both derive from the same shared encryption-domain validation pass, and one generator re-emits both files
together after a spec edit.

---

## Spec evolution rules

The spec files are the contract. Adding, renaming or removing a field requires coordination with deployed consumers:

- **Adding a message**: add a `messages[]` entry, mark the new class
  `[MqPub(MqMessages.X)]`, rebuild, and codegen emits the constant. Consumers
  pick up the new constant on their next build.
- **Renaming a constant**: edit the spec, update every `[MqPub("...")]` /
  `[MqSub("...")]` reference. `MessagingCatalog.RequireDescriptor` and `SubscriberRegistrar` throw
  `InvalidOperationException` for a reference to a constant that is not in the catalog, and CI detects the stale
  reference.
- **Changing the wire shape of an existing message**: the message class itself
  is the source of truth. JSON serialization tolerates additive changes (new
  optional fields). Removing or renaming a field is a breaking change - bump
  to a new constant + new class until every consumer has migrated.
- **Encryption-domain change**: edit the spec. The publisher picks up the new
  descriptor on the next build; rolling-upgrade safety needs both keyrings
  registered during the cutover window.

---

## Reference

- [`contracts/mq-messages/mq-messages.spec.json`](../../../../contracts/mq-messages/mq-messages.spec.json) - message
  catalog
- [`contracts/mq-subscriptions/mq-subscriptions.spec.json`](../../../../contracts/mq-subscriptions/mq-subscriptions.spec.json) -
  subscription catalog
- [`DcsvIo.D2.Messaging.Abstractions`](../abstractions/README.md) - emission target + transport-agnostic contract
- [`DcsvIo.D2.Messaging.RabbitMq`](../rabbitmq/README.md) - primary consumer (publish + consume) + canonical runtime /
  wire-format / topology reference
- [`DcsvIo.D2.Auth.Scopes.SourceGen`](../../auth/scopes-source-gen/README.md) - sibling source generator whose pattern
  this one follows (same incremental-generator + diagnostic-split pattern)
