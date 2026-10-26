// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import {
  isRabbitMqConnectionOptions,
  type RabbitMqConnectionOptions,
} from "./connection-options.js";

/** The URL protocol of an AMQP connection over TLS. */
const ENCRYPTED_PROTOCOL = "amqps:";

/** A decimal IPv4 octet with no leading zero, which no resolver reads as octal. */
const CANONICAL_OCTET = /^(?:0|[1-9]\d{0,2})$/;

/**
 * Thrown before any connection attempt when a broker address would carry the
 * broker credential and every message body unencrypted to a host that is not
 * loopback. The twin of the .NET `RabbitMqConnectionOptions` validation that
 * `AddD2MessagingRabbitMq` runs at composition. The message never contains the
 * address.
 */
export class UnencryptedTransportError extends Error {
  constructor() {
    super(
      "The RabbitMqConnectionOptions connection URI must use the amqps scheme unless its " +
        "host is loopback or RabbitMqConnectionOptions.allowUnencryptedTransport is true.",
    );
    this.name = "UnencryptedTransportError";
  }
}

/**
 * The address each options instance was admitted with. Held weakly, so an entry
 * lives no longer than the instance whose private field holds the same address.
 */
const admittedAddresses = new WeakMap<RabbitMqConnectionOptions, string>();

/**
 * Returns the broker address when it may be used: its scheme is `amqps`, its
 * host is loopback, or `allowUnencryptedTransport` is exactly `true`. An address
 * that does not parse as a URL is refused, because nothing shows its transport
 * is encrypted. The address is read once per options instance: a later call
 * with the same instance returns the address admitted the first time, so a
 * subscription's dead-letter transport connects to the address its connection
 * was checked against.
 *
 * @param options The connection configuration a connection is built from.
 * @returns The connection URI, credential included.
 * @throws `TypeError` when `options` is not a {@link RabbitMqConnectionOptions}
 * instance; the message never contains the address.
 * @throws {@link UnencryptedTransportError} when the transport is not admitted.
 */
export function admitConnectionUri(options: RabbitMqConnectionOptions): string {
  // Only an instance the constructor created holds the private address field; a
  // plain object is refused rather than read, even one with a reveal method.
  if (!isRabbitMqConnectionOptions(options))
    throw new TypeError(
      "Connection options must be created with new RabbitMqConnectionOptions(...).",
    );
  const admitted = admittedAddresses.get(options);
  if (admitted !== undefined) return admitted;
  const connectionUri = options.revealConnectionUri();
  if (!isTransportAdmitted(connectionUri, options.allowUnencryptedTransport))
    throw new UnencryptedTransportError();
  admittedAddresses.set(options, connectionUri);
  return connectionUri;
}

function isTransportAdmitted(
  connectionUri: string,
  allowUnencryptedTransport?: boolean,
): boolean {
  let url: URL;
  try {
    url = new URL(connectionUri);
  } catch {
    return false;
  }
  return (
    url.protocol === ENCRYPTED_PROTOCOL ||
    isLoopbackHost(url.hostname) ||
    allowUnencryptedTransport === true
  );
}

/**
 * Reports whether a URL host refers to the local machine: `localhost` in any case,
 * an IPv4 address in 127.0.0.0/8 written as four canonical decimal octets, or
 * the IPv6 address `::1` (which the URL parser serializes as `[::1]` whatever
 * form it was written in). Any other spelling is treated as remote.
 *
 * @param hostname The `hostname` of a parsed URL.
 */
function isLoopbackHost(hostname: string): boolean {
  const host = hostname.toLowerCase();
  if (host === "localhost" || host === "[::1]") return true;
  const octets = host.split(".");
  return (
    octets.length === 4 &&
    octets[0] === "127" &&
    octets.every((octet) => CANONICAL_OCTET.test(octet) && Number(octet) <= 255)
  );
}
