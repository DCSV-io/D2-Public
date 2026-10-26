// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

/**
 * Re-export of the generated proto module surface. The generated TS lives under
 * `src/generated/` and is regenerated with `pnpm generate` (Buf + ts-proto).
 *
 * This barrel re-exports only the `d2/common/v2` namespace.
 */

// d2/common/v2: D2ResultProto + TKMessageProto + InputErrorProto (+ Fns helpers)
export {
  D2ResultProto,
  TKMessageProto,
  InputErrorProto,
} from "./generated/d2/common/v2/d2_result.js";
