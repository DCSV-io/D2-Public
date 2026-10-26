// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import type { ICacheAtomic } from "./i-cache-atomic.js";
import type { ICacheBasic } from "./i-cache-basic.js";
import type { ICacheBroadcast } from "./i-cache-broadcast.js";
import type { ICacheSet } from "./i-cache-set.js";

/**
 * Cluster-scoped cache backed by a remote store (e.g. Redis). Atomic
 * primitives coordinate cluster-wide. The {@link ICacheBroadcast}
 * surface publishes an invalidation after the write, and tiered caches
 * in other instances drop their L1 entries for the key on receipt.
 *
 * Composes {@link ICacheBasic}, {@link ICacheAtomic},
 * {@link ICacheBroadcast} and {@link ICacheSet}.
 *
 * **Marker vs {@link ITieredCache}:** shared building blocks
 * (Basic, Atomic and Broadcast) are method-identical. **`ICacheSet` is
 * only on `IDistributedCache`** (not full structural identity). The
 * marker name states behavioral intent at the dependency site:
 * `IDistributedCache` means the cache talks to the remote store on
 * every read (no L1), so at that site freshness matters more than
 * read speed (e.g. rate-limit counters, lock state, ephemeral
 * session data).
 */
export interface IDistributedCache
  extends ICacheBasic, ICacheAtomic, ICacheBroadcast, ICacheSet {}
