// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import type { ICacheAtomic } from "./i-cache-atomic.js";
import type { ICacheBasic } from "./i-cache-basic.js";
import type { ICacheBroadcast } from "./i-cache-broadcast.js";

/**
 * Composed L1 (in-process) and L2 (remote) cache. Reads check L1 first,
 * fall through to L2 on miss, and populate L1 with whatever L2 returned.
 * Writes go to L2 first and L1 is written only if L2 succeeded, so
 * partial-write states are impossible and the result is binary: success,
 * or the L2 failure returned as is. Atomic primitives route through L2
 * (the source of truth) and invalidate or refresh L1 as a side effect.
 *
 * Composes {@link ICacheBasic}, {@link ICacheAtomic} and
 * {@link ICacheBroadcast}. **Does not compose {@link ICacheSet}**:
 * set primitives are cluster-only and tiered composition would
 * silently hide their cluster-wide nature. SADD/SCARD are available
 * on {@link IDistributedCache} only.
 *
 * **Marker vs {@link IDistributedCache}:** shared building blocks
 * (Basic, Atomic and Broadcast) are method-identical. The marker name
 * states behavioral intent: `ITieredCache` means reads
 * served from L1 are sub-microsecond, L1 stays cluster-coherent through
 * the broadcast backplane, and per-tier expirations are kept aligned
 * so L1 never outlives L2.
 *
 * Use this for read-heavy entity data where freshness within a few
 * seconds is acceptable. Prefer {@link IDistributedCache} when every
 * read must hit the canonical store.
 */
export interface ITieredCache
  extends ICacheBasic, ICacheAtomic, ICacheBroadcast {}
