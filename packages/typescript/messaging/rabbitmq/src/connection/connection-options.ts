// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import type { ILogger } from "@dcsv-io/d2-logging";
import {
  createManagedConnection,
  type D2Connection,
} from "./managed-connection.js";

/**
 * The settings a {@link RabbitMqConnectionOptions} is created from. This object
 * belongs to the caller; the options instance copies what it needs and keeps no
 * reference to it.
 */
export interface RabbitMqConnectionOptionsInit {
  /**
   * AMQP connection URI: `amqps://...`, or `amqp://...` to a loopback host
   * (`localhost`, 127.0.0.0/8, `[::1]`) or with `allowUnencryptedTransport`
   * set. SECRET (embeds the broker password). The `amqps:` scheme enables TLS
   * automatically. The options instance reads it back only through
   * {@link RabbitMqConnectionOptions.revealConnectionUri}.
   */
  readonly connectionUri: string;
  /**
   * Whether a broker address whose scheme is not `amqps` may use a host that
   * is not loopback. Defaults to `false`, and only `true` admits. Set it only
   * where nothing can observe the network path to the broker, such as a local
   * container network: the credential and every message body cross that path
   * unencrypted.
   */
  readonly allowUnencryptedTransport?: boolean;
  /** Client name shown in the RabbitMQ management UI (e.g. the service id). */
  readonly clientProvidedName?: string;
  /** Reconnect backoff floor in milliseconds (rabbitmq-client `retryLow`). */
  readonly retryLowMs?: number;
  /** Reconnect backoff ceiling in milliseconds (rabbitmq-client `retryHigh`). */
  readonly retryHighMs?: number;
  /** Heartbeat interval in seconds (0 disables). */
  readonly heartbeatSeconds?: number;
}

/** Assigned in the class body, where the private field can be referenced. */
let holdsPrivateAddress: (value: object) => boolean;

/**
 * Reports whether a value was created by `new RabbitMqConnectionOptions(...)` or
 * a subclass constructor. An object that only copies the class's methods or
 * prototype has no private address field and is not one.
 *
 * @param value The value to check.
 */
export function isRabbitMqConnectionOptions(
  value: unknown,
): value is RabbitMqConnectionOptions {
  return (
    typeof value === "object" && value !== null && holdsPrivateAddress(value)
  );
}

/**
 * Connection configuration for the RabbitMQ consumer runtime. Mirrors the
 * .NET `RabbitMqConnectionOptions` surface: a single AMQP URI (host / port /
 * vhost / credentials / TLS embedded) plus the transport choice, a
 * client-provided name and reconnect backoff.
 *
 * The URI contains the broker credential, so the instance keeps it in a private
 * field and {@link RabbitMqConnectionOptions.revealConnectionUri} is the only
 * read. `JSON.stringify`, `structuredClone`, `util.inspect`, object spread and a
 * structured log read the public fields, and none of them renders the address.
 * Route any diagnostic about the address through {@link redactAmqpUri}. The
 * instance is frozen, so the settings a connection was checked against cannot
 * change afterwards.
 *
 * A connection refuses an address whose scheme is not `amqps` unless its host is
 * loopback or {@link RabbitMqConnectionOptions.allowUnencryptedTransport} is
 * `true`, and throws `UnencryptedTransportError` before any connection
 * attempt. The address is read and checked once per instance, and a
 * subscription's dead-letter transport connects to that checked address.
 */
export class RabbitMqConnectionOptions {
  readonly #connectionUri: string;

  static {
    holdsPrivateAddress = (value) => #connectionUri in value;
  }

  /** See {@link RabbitMqConnectionOptionsInit.allowUnencryptedTransport}. */
  readonly allowUnencryptedTransport?: boolean;
  /** See {@link RabbitMqConnectionOptionsInit.clientProvidedName}. */
  readonly clientProvidedName?: string;
  /** See {@link RabbitMqConnectionOptionsInit.retryLowMs}. */
  readonly retryLowMs?: number;
  /** See {@link RabbitMqConnectionOptionsInit.retryHighMs}. */
  readonly retryHighMs?: number;
  /** See {@link RabbitMqConnectionOptionsInit.heartbeatSeconds}. */
  readonly heartbeatSeconds?: number;

  /**
   * Copies the settings and freezes the instance.
   *
   * @param init The settings, including the secret connection URI.
   */
  constructor(init: RabbitMqConnectionOptionsInit) {
    this.#connectionUri = init.connectionUri;
    this.allowUnencryptedTransport = init.allowUnencryptedTransport;
    this.clientProvidedName = init.clientProvidedName;
    this.retryLowMs = init.retryLowMs;
    this.retryHighMs = init.retryHighMs;
    this.heartbeatSeconds = init.heartbeatSeconds;
    Object.freeze(this);
  }

  /**
   * Returns the connection URI with the broker credential in it. This is the
   * only read of the address, for building a connection to the broker.
   */
  revealConnectionUri(): string {
    return this.#connectionUri;
  }
}

/**
 * Strips userinfo (credentials) from an AMQP URI so it can appear in logs
 * without leaking the broker password. Returns a `<scheme>://<host-and-path>`
 * form; a URI that cannot be parsed collapses to its scheme only, so a
 * malformed value cannot leak an embedded secret either.
 *
 * @param uri The AMQP URI (never logged whole).
 */
export function redactAmqpUri(uri: string): string {
  try {
    const parsed = new URL(uri);
    parsed.username = "";
    parsed.password = "";
    return parsed.toString();
  } catch {
    const schemeEnd = uri.indexOf("://");
    return schemeEnd > 0
      ? `${uri.slice(0, schemeEnd)}://[redacted]`
      : "[redacted]";
  }
}

/**
 * Constructs a long-lived, auto-reconnecting {@link D2Connection} from the D2
 * options. The connection immediately begins connecting and recovers from
 * dropped connections on its own (exponential backoff between
 * `retryLowMs`..`retryHighMs`); consumers registered via `subscribe` re-declare
 * their topology on every reconnect.
 *
 * @param options The connection configuration, created with
 * `new RabbitMqConnectionOptions(...)`.
 * @param logger Receives sanitized transport errors throughout the connection lifetime.
 * @throws `TypeError` when `options` is not a {@link RabbitMqConnectionOptions}
 * instance, such as a plain object literal.
 * @throws `UnencryptedTransportError` before any connection attempt when the
 * address's scheme is not `amqps`, its host is not loopback, and
 * `allowUnencryptedTransport` is not `true`.
 */
export function createConnection(
  options: RabbitMqConnectionOptions,
  logger: ILogger,
): D2Connection {
  return createManagedConnection(options, logger);
}
