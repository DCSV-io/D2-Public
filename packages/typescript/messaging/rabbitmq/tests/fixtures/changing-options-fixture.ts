// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { RabbitMqConnectionOptions } from "../../src/connection/connection-options.js";

/** Connection options paired with the count of their reveal calls. */
export interface ChangingOptionsFixture {
  readonly options: RabbitMqConnectionOptions;
  readonly reads: () => number;
}

/**
 * Creates connection options whose reveal method returns `first` on its first
 * call and `later` on every call after it, counting the calls.
 *
 * @param first The address the first reveal returns.
 * @param later The address every later reveal returns.
 */
export function createChangingOptionsFixture(
  first: string,
  later: string,
): ChangingOptionsFixture {
  let reads = 0;
  class ChangingRevealFixture extends RabbitMqConnectionOptions {
    override revealConnectionUri(): string {
      reads++;
      return reads === 1 ? first : later;
    }
  }
  return {
    options: new ChangingRevealFixture({ connectionUri: first }),
    reads: () => reads,
  };
}
