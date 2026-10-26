// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import type { D2Result } from "@dcsv-io/d2-result";

/**
 * Pluggable serialization seam for distributed caches. Local caches
 * store objects directly and do not need this. This package owns the
 * port only; a default JSON implementation is part of the
 * `@dcsv-io/d2-caching-distributed-redis` package surface. Provider-specific
 * implementations may substitute MessagePack, Protobuf and similar formats
 * for smaller size or better performance.
 *
 * Implementation failure codes: `COULD_NOT_BE_SERIALIZED` and
 * `COULD_NOT_BE_DESERIALIZED`. This package does not define them;
 * implementations reuse the existing catalogs.
 */
export interface ICacheSerializer {
  /**
   * Stable identifier for the serialization format (free string, e.g.
   * `"application/json"`). Lets DLQs and archive blobs that mix
   * serializers round-trip safely.
   */
  readonly contentType: string;

  /**
   * Serializes `value` to bytes.
   *
   * @returns `ok(bytes)`; failure with a serialize error code on
   *   serializer error.
   */
  serialize<T>(value: T): D2Result<Uint8Array>;

  /**
   * Deserializes `bytes` back to a value.
   *
   * @returns `ok(value)`; failure with a deserialize error code on
   *   deserializer error.
   */
  deserialize<T>(bytes: Uint8Array): D2Result<T>;
}
