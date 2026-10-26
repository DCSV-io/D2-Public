// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import type { IPayloadCrypto, IPayloadSealer } from "@dcsv-io/d2-encryption";
import type {
  EncryptionDomain,
  EncryptionDomainMode,
  EncryptionDomainModes,
} from "@dcsv-io/d2-encryption-abstractions";
import type { MqMessagesCatalog } from "@dcsv-io/d2-messaging-abstractions";

import type { ApplicationDomainModes } from "../encryption-domain-modes.js";

/**
 * The compile-time type-witness that fuses publishing and encryption (the TS
 * twin of the .NET spec-driven composer and DI). Publishing a message to an
 * encrypted domain that was not wired is a compile error, and there is no
 * raw-bytes publish surface: the composer for a domain is the only path to the
 * socket for that domain. Runtime default-deny (see `publisher.ts`) is the
 * second check for dynamic and fixture paths that bypass the static catalog.
 */

/**
 * Domains with payload encryption: every domain except the `plaintext`
 * sentinel (which ships cleartext and needs no composer).
 */
export type EncryptedDomain = Exclude<EncryptionDomain, "plaintext">;

/**
 * The composer a domain requires, branded by its encryption mode: a `sealed`
 * domain accepts only an {@link IPayloadSealer}; a `symmetric` domain only an
 * {@link IPayloadCrypto}. Driven by the literal `EncryptionDomainModes` types so
 * a mode change reshapes the required composer automatically.
 */
export type ComposerFor<D extends EncryptedDomain> =
  (typeof EncryptionDomainModes)[D] extends "sealed"
    ? IPayloadSealer
    : IPayloadCrypto;

/**
 * The full wiring map: every encrypted domain to its mode-branded composer.
 * A host passes a `Partial` of this (it wires only the domains it publishes to);
 * `createPublisher` captures the wired subset's literal keys.
 */
export type DomainCryptoMap = {
  [D in EncryptedDomain]: ComposerFor<D>;
};

/**
 * The composer an encryption mode requires: `sealed` accepts only an
 * {@link IPayloadSealer}, `symmetric` only an {@link IPayloadCrypto}.
 */
export type ComposerForMode<M extends EncryptionDomainMode> = M extends "sealed"
  ? IPayloadSealer
  : IPayloadCrypto;

/**
 * The full wiring map given an application's own domain modes `TModes`: every
 * framework encrypted domain as in {@link DomainCryptoMap}, plus each application
 * domain to the composer its declared mode requires. A key of `TModes` that the
 * framework catalog already contains keeps the catalog's slot, and `plaintext`
 * never gets one. With no application modes this is {@link DomainCryptoMap}.
 */
export type DomainCryptoMapOf<
  TModes extends ApplicationDomainModes = Record<never, never>,
> = DomainCryptoMap & {
  [D in Exclude<keyof TModes & string, EncryptionDomain>]: ComposerForMode<
    TModes[D]
  >;
};

/**
 * The literal `encryption` value the message `K` of the catalog `TCatalog`
 * publishes under. `TCatalog` defaults to the framework catalog this package
 * ships against, which has no message entries. Binding `TCatalog` to another
 * catalog, such as the `as const` mirror given as
 * `CreatePublisherOptions.descriptors`, selects a message of that catalog.
 */
export type CatalogEncryption<
  K extends keyof TCatalog,
  TCatalog = typeof MqMessagesCatalog,
> = TCatalog[K] extends { readonly encryption: infer E } ? E : never;

/**
 * The message constants of an arbitrary catalog `TCatalog` publishable given the
 * wired composer map `TWired`: a message is publishable iff its `encryption` is
 * `plaintext` (always cleartext) or a key of `TWired` (a composer is wired for
 * it). The inner test is non-distributive, so an entry whose `encryption` is a
 * union publishes only when every arm is covered, so a supplied catalog
 * need not contain one literal per entry. Generic over the catalog so the
 * compile-time type-witness is provable against a synthetic catalog (the
 * production catalog contains no sealed messages).
 */
export type PublishableKeyOf<TCatalog, TWired> = {
  [K in keyof TCatalog]: TCatalog[K] extends { readonly encryption: infer E }
    ? [E] extends [keyof TWired | "plaintext"]
      ? K
      : never
    : never;
}[keyof TCatalog];

/**
 * The publishable message constants of the catalog `TCatalog` given the wired
 * composer map `TWired`. Publishing any other message is a compile error: there
 * is no overload that skips composition.
 *
 * `TCatalog` defaults to the framework catalog this package ships against. An
 * application binds its own catalog (the `as const` mirror its catalog package
 * exports) by passing it as `CreatePublisherOptions.descriptors`, which is the
 * same seam the runtime descriptor lookup reads.
 */
export type PublishableKey<
  TWired,
  TCatalog = typeof MqMessagesCatalog,
> = PublishableKeyOf<TCatalog, TWired>;
