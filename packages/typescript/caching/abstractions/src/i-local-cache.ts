// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import type { ICacheAtomic } from "./i-cache-atomic.js";
import type { ICacheBasic } from "./i-cache-basic.js";

/**
 * Per-process in-memory cache. Atomic primitives operate at process
 * scope and do not coordinate across instances. There is no
 * broadcast surface because nothing outside this process can observe
 * local cache state.
 *
 * The interface name documents the cache scope at the dependency site.
 * `ILocalCache` at an injection site shows, without looking
 * at registration, that the cache lives in process memory and ends
 * with the process.
 *
 * Composes {@link ICacheBasic} and {@link ICacheAtomic} only.
 */
export interface ILocalCache extends ICacheBasic, ICacheAtomic {}
