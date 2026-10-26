// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

// Compile-time type-witness proofs for the publish/encrypt fusion. Vitest does
// not run this file (it is `.compile.ts`, not `*.test.ts`); `tsc -p
// tsconfig.test.json` (the `type-check:test` gate) type-checks it. Each
// `@ts-expect-error` requires the expression that follows to be a type error.
// If the witness regresses (an unwired encrypted domain becomes publishable, or
// a mode-branded slot accepts the wrong composer), the error disappears and tsc
// fails on the unused directive. The `export`s satisfy `noUnusedLocals`. A
// directive suppresses only errors reported on the line directly beneath it, so
// every `@ts-expect-error` here is followed by a single physical line: wrapping
// a guarded expression can move the error below that line and leave the
// directive with no error to suppress. Where a guarded line would wrap under
// Prettier, an alias holds its type instead (`PkNothingWired`,
// `PkUnionUnwired`, `SealedEncryption`). An unguarded expression may span lines.

import type { IPayloadCrypto, IPayloadSealer } from "@dcsv-io/d2-encryption";

import type { ILogger } from "@dcsv-io/d2-logging";

import type { D2Connection } from "../src/connection/managed-connection.js";
import type {
  CatalogEncryption,
  DomainCryptoMap,
  DomainCryptoMapOf,
  PublishableKey,
  PublishableKeyOf,
} from "../src/publishing/domain-crypto-map.js";
import { createPublisher } from "../src/publishing/publisher.js";

// A synthetic catalog: one plaintext message and one message on the sealed
// framework fixture domain. The framework catalog contains no message, so the
// witness is proven against this catalog, which has the same shape as an
// application's own `as const` catalog.
interface FakeCatalog {
  readonly PlainMsg: { readonly encryption: "plaintext" };
  readonly SealedMsg: { readonly encryption: "payload-fixture-sealed" };
}

declare const sealer: IPayloadSealer;
declare const someCrypto: IPayloadCrypto;
declare const bareObject: { readonly nope: true };

type PkSealedWired = PublishableKeyOf<
  FakeCatalog,
  { "payload-fixture-sealed": IPayloadSealer }
>;
type PkNothingWired = PublishableKeyOf<FakeCatalog, Record<never, never>>;
type Wiring = Partial<DomainCryptoMap>;

// With the sealed composer wired, both messages are publishable.
export const _plainWired: PkSealedWired = "PlainMsg";
export const _sealedWired: PkSealedWired = "SealedMsg";

// With nothing wired, only the plaintext message is publishable.
export const _plainUnwired: PkNothingWired = "PlainMsg";
// @ts-expect-error - SealedMsg's domain is unwired, so it is not publishable.
export const _sealedUnwired: PkNothingWired = "SealedMsg";

// The wiring map is mode-branded: the sealed slot demands a sealer.
export const _goodMap: Wiring = { "payload-fixture-sealed": sealer };
// @ts-expect-error - a symmetric crypto is not a sealer; the sealed slot rejects it.
export const _cryptoMap: Wiring = { "payload-fixture-sealed": someCrypto };
// @ts-expect-error - a bare object is not a composer for the sealed slot.
export const _objectMap: Wiring = { "payload-fixture-sealed": bareObject };

// The catalog is a parameter, not a fixed reference to the framework one.
// Bound to FakeCatalog, PlainMsg and SealedMsg stay publishable exactly as
// above. Left at its default (the framework catalog, which contains no entry),
// nothing is publishable, which is the result for a host that bound no catalog
// of its own.
export const _boundPlain: PublishableKey<
  { "payload-fixture-sealed": IPayloadSealer },
  FakeCatalog
> = "PlainMsg";
// @ts-expect-error - the default catalog carries no entry, so no key is publishable.
export const _unboundPlain: PublishableKey<Record<never, never>> = "PlainMsg";

// A catalog entry whose `encryption` is a union and not one literal. A
// caller-supplied catalog can have this shape because the catalog is a
// parameter. Both arms must be covered for the key to be publishable.
interface FakeUnionCatalog {
  readonly WobblyMsg: {
    readonly encryption: "plaintext" | "payload-fixture-sealed";
  };
}

// With the sealed composer wired, both arms are covered and the key publishes.
export const _unionWired: PublishableKeyOf<
  FakeUnionCatalog,
  { "payload-fixture-sealed": IPayloadSealer }
> = "WobblyMsg";

type PkUnionUnwired = PublishableKeyOf<FakeUnionCatalog, Record<never, never>>;

// @ts-expect-error - one arm of the union is an unwired domain, so the key is not publishable.
export const _unionUnwired: PkUnionUnwired = "WobblyMsg";

// The encryption literal of a bound catalog's message. The catalog is a
// parameter here too, so an application can look up the domain of its own
// message.
type SealedEncryption = CatalogEncryption<"SealedMsg", FakeCatalog>;

export const _sealedEncryption: SealedEncryption = "payload-fixture-sealed";
// @ts-expect-error - SealedMsg publishes on the sealed domain, not in cleartext.
export const _wrongEncryption: SealedEncryption = "plaintext";

// Application-defined domains: the modes an application declares determine
// the composer that each of its domains' slots accepts, the same way the
// framework catalog's literal modes do for its own domains.
const appModes = {
  "app-orders-fixture": "sealed",
  "app-ledger-fixture": "symmetric",
} as const;

type AppWiring = Partial<DomainCryptoMapOf<typeof appModes>>;

export const _appSealed: AppWiring = { "app-orders-fixture": sealer };
// @ts-expect-error - the application sealed slot rejects a symmetric crypto.
export const _appSealedCrypto: AppWiring = { "app-orders-fixture": someCrypto };
export const _appSymmetric: AppWiring = { "app-ledger-fixture": someCrypto };
// @ts-expect-error - the application symmetric slot rejects a sealer.
export const _appSymmetricSealer: AppWiring = { "app-ledger-fixture": sealer };
// @ts-expect-error - a domain the application never declared has no slot.
export const _appUndeclared: AppWiring = { "app-other-fixture": sealer };
// The framework slots are unchanged beside the application ones.
export const _appKeepsFramework: AppWiring = {
  "payload-fixture-sealed": sealer,
};

// A declared mode for a framework domain keeps the framework catalog's slot.
type ShadowWiring = Partial<
  DomainCryptoMapOf<{ readonly "payload-fixture-sealed": "symmetric" }>
>;
// @ts-expect-error - the catalog's sealed slot still rejects a symmetric crypto.
export const _shadow: ShadowWiring = { "payload-fixture-sealed": someCrypto };

// With no application modes the map is the framework map.
type NoAppWiring = Partial<DomainCryptoMapOf>;
// @ts-expect-error - without declared modes an application domain has no slot.
export const _noAppOrders: NoAppWiring = { "app-orders-fixture": sealer };

// The createPublisher call site: the declared modes flow from `modes` into the
// `crypto` constraint, and a message on a wired application domain publishes.
const appCatalog = {
  OrderMsg: {
    constant: "OrderMsg",
    messageType: "D2.Test.OrderMsg",
    exchange: "d2.test.orders",
    exchangeType: "fanout",
    encryption: "app-orders-fixture",
  },
} as const;

declare const connection: D2Connection;
declare const logger: ILogger;

const appPublisher = createPublisher(connection, {
  crypto: { "app-orders-fixture": sealer },
  modes: appModes,
  logger,
  descriptors: appCatalog,
});

export const _appPublish = appPublisher.publish("OrderMsg", {});

export const _appWrongComposer = createPublisher(connection, {
  // @ts-expect-error - the declared sealed domain's slot rejects a symmetric crypto.
  crypto: { "app-orders-fixture": someCrypto },
  modes: appModes,
  logger,
  descriptors: appCatalog,
});

export const _appUndeclaredDomain = createPublisher(connection, {
  // @ts-expect-error - without `modes` the application domain has no slot.
  crypto: { "app-orders-fixture": sealer },
  logger,
  descriptors: appCatalog,
});

// Inline `modes` infer their literal modes without `as const`.
export const _appInlineModes = createPublisher(connection, {
  crypto: { "app-orders-fixture": sealer },
  modes: { "app-orders-fixture": "sealed" },
  logger,
  descriptors: appCatalog,
}).publish("OrderMsg", {});

export const _appInlineWrongComposer = createPublisher(connection, {
  // @ts-expect-error - the inline sealed mode still rejects a symmetric crypto.
  crypto: { "app-orders-fixture": someCrypto },
  modes: { "app-orders-fixture": "sealed" },
  logger,
  descriptors: appCatalog,
});
