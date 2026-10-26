// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

export {
  RedisDistributedCache,
  type RedisDistributedCacheDeps,
} from "./redis-distributed-cache.js";
export { RedisCacheInvalidationBackplane } from "./redis-cache-invalidation-backplane.js";
export { JsonCacheSerializer } from "./json-cache-serializer.js";
export {
  REDIS_CACHE_INSTRUMENTS,
  REDIS_CACHE_METER_NAME,
  REDIS_CACHE_METER_VERSION,
  type CacheInstrumentMeta,
} from "./redis-cache-telemetry.js";
export {
  REDIS_CACHE_DEFAULTS,
  createRedisCacheOptions,
  type RedisCacheOptions,
} from "./redis-cache-options.js";
export { connectRedis } from "./connect-redis.js";
// Public surface that pins the Lua script constants for dual-runtime
// ContractFixtures parity. It executes nothing: it exports constants only,
// byte-equivalent to .NET RedisLuaScripts.
export {
  CONSUME_TOKEN,
  INCREMENT_WITH_OPTIONAL_TTL,
  RELEASE_LOCK_IF_OWNER,
  REMOVE_IF_EQUAL,
  SET_ADD_WITH_OPTIONAL_TTL,
  SET_IF_EQUAL,
} from "./redis-lua-scripts.js";
