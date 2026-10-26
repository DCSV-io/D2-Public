// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import {
  type EncryptionDomainMode,
  EncryptionDomainModes,
} from "@dcsv-io/d2-encryption-abstractions";
import { falsey } from "@dcsv-io/d2-utilities";

const _PLAINTEXT = "plaintext";

const _catalog = EncryptionDomainModes as Readonly<
  Record<string, EncryptionDomainMode>
>;

/**
 * The encryption modes of an application's own domains, keyed by wire value:
 * the domains its catalog publishes on that the framework `EncryptionDomainModes`
 * catalog does not contain. Passed `as const` to `createPublisher({ modes })` so
 * each domain's literal mode picks the composer its `crypto` slot requires, and
 * to `assertOpenerMatchesDomain` on the consume side.
 *
 * The TypeScript counterpart of .NET `EncryptionDomainModeCatalog`. .NET
 * registers only sealed domains, each with its consumer service, because an
 * unregistered domain resolves to symmetric there and keyed dependency
 * injection resolves the sealer by consumer service. Here the host wires one
 * composer per domain, so a domain has its mode only, and a symmetric
 * application domain is declared too: a domain in neither this map nor the
 * framework catalog is refused.
 */
export type ApplicationDomainModes = Readonly<
  Record<string, EncryptionDomainMode>
>;

/**
 * Merges application domain modes over the framework `EncryptionDomainModes`
 * catalog. A domain the catalog already contains is accepted only with the
 * catalog's own mode. The result has no prototype, so an inherited member name
 * such as `constructor` resolves to no mode.
 *
 * @param modes The application's domain modes; omitted, the framework catalog alone.
 * @returns The frozen merged mode lookup, keyed by domain wire value.
 * @throws When a domain name is empty or whitespace, is the `plaintext`
 *   sentinel, has a mode other than `sealed` or `symmetric`, or re-maps a
 *   catalog domain to another mode.
 */
export function resolveDomainModes(
  modes?: ApplicationDomainModes,
): Readonly<Record<string, EncryptionDomainMode>> {
  const merged: Record<string, EncryptionDomainMode> = Object.create(
    null,
  ) as Record<string, EncryptionDomainMode>;
  Object.assign(merged, _catalog);

  for (const [domain, mode] of Object.entries(modes ?? {})) {
    if (falsey(domain)) {
      throw new Error(
        "An application encryption domain name is empty or whitespace.",
      );
    }

    if (domain === _PLAINTEXT) {
      throw new Error(
        `Encryption domain '${domain}' is the cleartext sentinel; it cannot ` +
          "be registered with an encryption mode.",
      );
    }

    if (mode !== "sealed" && mode !== "symmetric") {
      throw new Error(
        `Encryption domain '${domain}' has mode '${String(mode)}'; expected ` +
          "'sealed' or 'symmetric'.",
      );
    }

    const existing = Object.hasOwn(_catalog, domain)
      ? _catalog[domain]
      : undefined;

    if (existing !== undefined && existing !== mode) {
      throw new Error(
        `Encryption domain '${domain}' was already registered as ${existing}; ` +
          `cannot re-register as ${mode}.`,
      );
    }

    merged[domain] = mode;
  }

  return Object.freeze(merged);
}
