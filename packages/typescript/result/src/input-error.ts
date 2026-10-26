// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import type { TKMessage } from "@dcsv-io/d2-i18n-abstractions";

/**
 * A field-level validation error: the offending field name plus one or more
 * translation messages describing what's wrong with that field. Wire format
 * matches .NET `DcsvIo.D2.Result.InputError` -
 * `{ field, errors: [{ key, params? }] }`.
 *
 * The JSON property names (`field`, `errors`) come from the spec-derived
 * `InputErrorWireShape` catalog (`./input-error.g.ts`);
 * `contracts/input-error/input-error.spec.json` drives BOTH the .NET
 * serializer AND this interface, so cross-language wire drift on the
 * property names is structurally impossible. The shape is a self-describing
 * object (NOT a tuple), so extending it with additional fields (e.g.
 * per-error `severity`) does not depend on positional indexing.
 */
export interface InputError {
  readonly field: string;
  readonly errors: readonly TKMessage[];
}

/**
 * Constructs an `InputError`.
 */
export function inputError(
  field: string,
  errors: readonly TKMessage[],
): InputError {
  return { field, errors };
}
