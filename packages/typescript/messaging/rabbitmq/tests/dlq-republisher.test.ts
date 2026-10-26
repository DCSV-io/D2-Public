// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { afterEach, describe, expect, it, vi } from "vitest";
import {
  DlqOperation,
  DlqRepublisher,
  DlqTransportGeneration,
  waitForDlqCleanup,
  type DlqRepublishOutcome,
} from "../src/subscribing/dlq-republisher.js";
import { RabbitMqConnectionOptions } from "../src/connection/connection-options.js";
import { createCompletion } from "../src/connection/managed-connection.js";
import { DlqTransportFixture } from "./fixtures/dlq-transport-fixture.js";
import { RecordingLogger } from "./helpers.js";
import { dlqLifetimeContractFixture } from "./fixtures/dlq-lifetime-contract-fixture.js";

// These cases compile only against the internal transport surface
// (DlqTransportGeneration, DlqOperation and waitForDlqCleanup).
// An implementation that discards cleanup fails the late-acquisition assertions.
afterEach(() => vi.useRealTimers());

const request = {
  dlxName: "fixture.dlx",
  body: Buffer.from([1, 2]),
  headers: {},
};

function create(
  fixture: DlqTransportFixture,
  barrier = async (_signal: AbortSignal) => {},
) {
  return new DlqRepublisher(
    new RabbitMqConnectionOptions({
      connectionUri: "amqp://fixture",
      allowUnencryptedTransport: true,
    }),
    { republishMs: 100, cleanupMs: 10 },
    2,
    new RecordingLogger(),
    {
      connectionFactory: () => fixture.connection(),
      now: () => Date.now(),
      waitForReturns: barrier,
    },
  );
}

describe("DLQ transport ownership", () => {
  it("Republish_ReturnLoggerThrows_PreservesUnroutableOutcome", async () => {
    const fixture = new DlqTransportFixture();
    fixture.completeAcquisition();
    const sent = createCompletion<void>();
    const confirmation = createCompletion<void>();
    fixture.channel.basicPublish.mockImplementation(() => {
      sent.resolve();
      return confirmation.promise;
    });
    const logger = new RecordingLogger();
    const error = vi.spyOn(logger, "error").mockImplementation(() => {
      throw new Error("Diagnostic fixture failed.");
    });
    const republisher = new DlqRepublisher(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      { republishMs: 1000, cleanupMs: 10 },
      1,
      logger,
      {
        connectionFactory: () => fixture.connection(),
        waitForReturns: async () => {},
      },
    );
    const result = republisher.republish(request);
    try {
      await sent.promise;
      expect(() => fixture.channel.emit("basic.return", {})).not.toThrow();
      confirmation.resolve();
      expect(await result).toBe("unroutable");
      expect(error).toHaveBeenCalledOnce();
    } finally {
      confirmation.resolve();
      await result;
      await republisher.close();
      await republisher.resourceCompletion;
    }
  });

  it.each(["work", "channel", "connection"] as const)(
    "Terminate_LoggerThrowsDuring_%s_CompletesRemainingCleanup",
    async (phase) => {
      const fixture = new DlqTransportFixture();
      const logger = new RecordingLogger();
      const error = vi.spyOn(logger, "error").mockImplementation(() => {
        throw new Error("Diagnostic fixture failed.");
      });
      const failure = new Error("Resource fixture failed.");
      const generation = new DlqTransportGeneration(
        1n,
        () => fixture.connection(),
        () => {},
        logger,
      );
      generation.connection = fixture.connection();
      fixture.completeAcquisition();
      generation.channel = await fixture.acquire();
      if (phase === "work") generation.work = Promise.reject(failure);
      if (phase === "channel") fixture.channel.close.mockRejectedValue(failure);
      if (phase === "connection") fixture.close.mockRejectedValue(failure);
      generation.terminate();
      await expect(generation.cleanup).resolves.toBeUndefined();
      expect(generation.resourceSettled).toBe(true);
      expect(fixture.channel.close).toHaveBeenCalledOnce();
      expect(fixture.close).toHaveBeenCalledOnce();
      expect(error).toHaveBeenCalledOnce();
    },
  );

  it("Terminate_SanitizationThrows_CompletesRemainingCleanup", async () => {
    const fixture = new DlqTransportFixture();
    const logger = new RecordingLogger();
    const failure = new Error("Private resource failure.");
    Object.defineProperty(failure, "name", {
      get() {
        throw new Error("Unreadable error name.");
      },
    });
    const generation = new DlqTransportGeneration(
      1n,
      () => fixture.connection(),
      () => {},
      logger,
    );
    generation.connection = fixture.connection();
    generation.work = Promise.reject(failure);
    generation.terminate();
    await expect(generation.cleanup).resolves.toBeUndefined();
    expect(generation.resourceSettled).toBe(true);
    expect(fixture.close).toHaveBeenCalledOnce();
    expect(logger.entries).toEqual([]);
  });

  it("Republish_CanceledMiddleWaiter_PreservesRemainingFifoOrder", async () => {
    const fixture = new DlqTransportFixture();
    fixture.completeAcquisition();
    const sent = createCompletion<void>();
    const confirmation = createCompletion<void>();
    fixture.channel.basicPublish.mockImplementationOnce(() => {
      sent.resolve();
      return confirmation.promise;
    });
    const republisher = new DlqRepublisher(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      { republishMs: 1000, cleanupMs: 10 },
      4,
      new RecordingLogger(),
      {
        connectionFactory: () => fixture.connection(),
        waitForReturns: async () => {},
      },
    );
    const first = republisher.republish({ ...request, body: Buffer.from([1]) });
    await sent.promise;
    const canceled = new AbortController();
    const second = republisher.republish(
      { ...request, body: Buffer.from([2]) },
      canceled.signal,
    );
    const third = republisher.republish({ ...request, body: Buffer.from([3]) });
    const fourth = republisher.republish({
      ...request,
      body: Buffer.from([4]),
    });
    try {
      canceled.abort();
      expect(await second).toBe("stopping");
      confirmation.resolve();
      expect(await Promise.all([first, third, fourth])).toEqual([
        "confirmed",
        "confirmed",
        "confirmed",
      ]);
      expect(fixture.channel.basicPublish).toHaveBeenCalledTimes(3);
      expect(fixture.channel.basicPublish).toHaveBeenNthCalledWith(
        1,
        expect.any(Object),
        Buffer.from([1]),
      );
      expect(fixture.channel.basicPublish).toHaveBeenNthCalledWith(
        2,
        expect.any(Object),
        Buffer.from([3]),
      );
      expect(fixture.channel.basicPublish).toHaveBeenNthCalledWith(
        3,
        expect.any(Object),
        Buffer.from([4]),
      );
    } finally {
      confirmation.resolve();
      await republisher.close();
    }
  });

  it("Close_DuringAdmissionHandoff_DoesNotAllocateGeneration", async () => {
    const fixture = new DlqTransportFixture();
    const republisher = create(fixture);
    const result = republisher.republish(request);
    const closed = republisher.close();
    expect(await result).toBe("stopping");
    await closed;
    expect(fixture.acquire).not.toHaveBeenCalled();
    expect(fixture.unsafeDestroy).not.toHaveBeenCalled();
  });

  // waitForDlqCleanup must propagate the rejection of the retained task and must
  // not leave its timer running.
  it("WaitForCleanup_ResourceRejects_PropagatesFailureAndClearsTimer", async () => {
    vi.useFakeTimers();
    const resources = createCompletion<void>();
    const failure = new Error("Cleanup failed.");
    const wait = waitForDlqCleanup(resources.promise, 100);
    resources.reject(failure);
    await expect(wait).rejects.toBe(failure);
    expect(vi.getTimerCount()).toBe(0);
  });

  // Cleanup must observe a rejected retained task even when no transport was allocated.
  it("Terminate_RejectedRetainedWork_ObservesFailureAndCompletesResources", async () => {
    const logger = new RecordingLogger();
    const factory = vi.fn();
    const generation = new DlqTransportGeneration(
      1n,
      factory,
      () => {},
      logger,
    );
    generation.work = Promise.reject(new Error("Retained task failed."));
    generation.terminate();
    await generation.resources.promise;
    expect(generation.resourceSettled).toBe(true);
    expect(factory).not.toHaveBeenCalled();
    expect(logger.logged("DLQ transport cleanup failed")).toBe(true);
  });

  it.each(dlqLifetimeContractFixture.outcomes)(
    "Complete_SharedScenario_PreservesExpectedOutcome($first)",
    async (row) => {
      vi.useFakeTimers();
      const operation = new DlqOperation(
        1n,
        100,
        () => Date.now(),
        () => {},
      );
      try {
        operation.complete(row.first);
        operation.complete(row.later);
        expect(await operation.completion.promise).toBe(row.expected);
      } finally {
        operation.dispose();
      }
    },
  );

  // Direct tests of the operation state use DlqOperation, which the internal
  // module exports.
  it.each<DlqRepublishOutcome>([
    "confirmed",
    "unroutable",
    "transport-failure",
    "deadline-exceeded",
    "stopping",
    "cleanup-pending",
  ])("Advance_TerminalOutcome_%s_RemainsTerminal", async (outcome) => {
    vi.useFakeTimers();
    const operation = new DlqOperation(
      1n,
      100,
      () => Date.now(),
      () => {},
    );
    try {
      operation.complete(outcome);
      operation.advance("acquisition");
      operation.advance("publication");
      operation.advance("return-drain");
      expect(operation.state).toEqual({ kind: "terminal", outcome });
      expect(await operation.completion.promise).toBe(outcome);
    } finally {
      operation.dispose();
    }
  });

  it("Republish_IdleTransportError_RetiresWithoutAnActiveOperation", async () => {
    const fixture = new DlqTransportFixture();
    fixture.completeAcquisition();
    const republisher = create(fixture);
    try {
      expect(await republisher.republish(request)).toBe("confirmed");
      fixture.emit("error", new Error("Idle transport failed."));
      expect(fixture.unsafeDestroy).toHaveBeenCalledOnce();
    } finally {
      await republisher.close();
    }
    expect(fixture.close).toHaveBeenCalledOnce();
  });
  it("Close_DuringConnectionAllocation_TerminatesReturnedTransport", async () => {
    const fixture = new DlqTransportFixture();
    let closed: Promise<void> | undefined;
    const republisher = new DlqRepublisher(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      { republishMs: 100, cleanupMs: 10 },
      1,
      new RecordingLogger(),
      {
        connectionFactory: () => {
          closed = republisher.close();
          return fixture.connection();
        },
      },
    );
    expect(await republisher.republish(request)).toBe("stopping");
    await closed;
    await republisher.resourceCompletion;
    expect(fixture.unsafeDestroy).toHaveBeenCalledOnce();
    expect(fixture.acquire).not.toHaveBeenCalled();
    expect(fixture.close).toHaveBeenCalledOnce();
  });

  it("Close_BeforeScheduledAllocation_DoesNotCreateTransport", async () => {
    const factory = vi.fn();
    const republisher = new DlqRepublisher(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      { republishMs: 100, cleanupMs: 10 },
      1,
      new RecordingLogger(),
      { connectionFactory: factory },
    );
    const result = republisher.republish(request);
    await Promise.resolve();
    const closed = republisher.close();
    expect(await result).toBe("stopping");
    await closed;
    expect(factory).not.toHaveBeenCalled();
  });

  it("Close_ConfirmSelectionPending_ObservesLateReplyWithoutPublication", async () => {
    const fixture = new DlqTransportFixture();
    fixture.completeAcquisition();
    const entered = createCompletion<void>();
    const reply = createCompletion<void>();
    fixture.channel.confirmSelect.mockImplementation(() => {
      entered.resolve();
      return reply.promise;
    });
    const republisher = create(fixture);
    const result = republisher.republish(request);
    await entered.promise;
    const closed = republisher.close();
    reply.resolve();
    expect(await result).toBe("stopping");
    await closed;
    expect(fixture.channel.basicPublish).not.toHaveBeenCalled();
    expect(fixture.channel.close).toHaveBeenCalledOnce();
  });

  it("Close_LateReturnDrainSuccess_DoesNotReplaceStoppingOutcome", async () => {
    const fixture = new DlqTransportFixture();
    fixture.completeAcquisition();
    const entered = createCompletion<void>();
    const release = createCompletion<void>();
    const republisher = create(fixture, () => {
      entered.resolve();
      return release.promise;
    });
    const result = republisher.republish(request);
    await entered.promise;
    const closed = republisher.close();
    release.resolve();
    expect(await result).toBe("stopping");
    await closed;
    expect(fixture.channel.listenerCount("basic.return")).toBe(0);
  });

  it("Republish_RetiredResourcesCompleted_AllocatesReplacementLazily", async () => {
    const first = new DlqTransportFixture();
    const second = new DlqTransportFixture();
    first.completeAcquisition();
    second.completeAcquisition();
    first.channel.basicPublish.mockRejectedValue(
      new Error("First transport failed."),
    );
    const factory = vi
      .fn()
      .mockReturnValueOnce(first.connection())
      .mockReturnValueOnce(second.connection());
    const republisher = new DlqRepublisher(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      { republishMs: 100, cleanupMs: 10 },
      1,
      new RecordingLogger(),
      { connectionFactory: factory, waitForReturns: async () => {} },
    );
    try {
      expect(await republisher.republish(request)).toBe("transport-failure");
      expect(factory).toHaveBeenCalledOnce();
      expect(first.close).toHaveBeenCalledOnce();
      expect(await republisher.republish(request)).toBe("confirmed");
      expect(factory).toHaveBeenCalledTimes(2);
    } finally {
      await republisher.close();
    }
  });

  it.each([0, -1, 1.5, Number.NaN, Number.POSITIVE_INFINITY])(
    "Create_InvalidPrefetch_%s_RejectsBeforeAllocation",
    (prefetch) => {
      expect(
        () =>
          new DlqRepublisher(
            new RabbitMqConnectionOptions({
              connectionUri: "amqp://fixture",
              allowUnencryptedTransport: true,
            }),
            { republishMs: 100, cleanupMs: 10 },
            prefetch,
            new RecordingLogger(),
          ),
      ).toThrow("Prefetch must be positive.");
    },
  );

  it.each([0, -1, 1.5, 2147483648, Number.NaN])(
    "Create_InvalidDuration_%s_RejectsBeforeAllocation",
    (duration) => {
      for (const options of [
        { republishMs: duration, cleanupMs: 10 },
        { republishMs: 100, cleanupMs: duration },
      ]) {
        expect(
          () =>
            new DlqRepublisher(
              new RabbitMqConnectionOptions({
                connectionUri: "amqp://fixture",
                allowUnencryptedTransport: true,
              }),
              options,
              1,
              new RecordingLogger(),
            ),
        ).toThrow("DLQ durations must be positive whole milliseconds.");
      }
    },
  );

  it("Create_MaximumDurationsAndPrefetch_AdmitsValidConfiguration", async () => {
    const republisher = new DlqRepublisher(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      { republishMs: 2147483647, cleanupMs: 2147483647 },
      65536,
      new RecordingLogger(),
    );
    await republisher.close();
    await republisher.resourceCompletion;
  });

  it("Republish_DefaultReturnBarrier_ObservesQueuedReturn", async () => {
    const fixture = new DlqTransportFixture();
    fixture.completeAcquisition();
    fixture.channel.basicPublish.mockImplementation(async () => {
      setImmediate(() => fixture.channel.emit("basic.return", {}));
    });
    const republisher = new DlqRepublisher(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      { republishMs: 100, cleanupMs: 10 },
      1,
      new RecordingLogger(),
      { connectionFactory: () => fixture.connection() },
    );
    try {
      expect(await republisher.republish(request)).toBe("unroutable");
    } finally {
      await republisher.close();
    }
  });

  it.each(["error", "close"])(
    "Republish_Channel_%s_RetiresTransport",
    async (event) => {
      const fixture = new DlqTransportFixture();
      fixture.completeAcquisition();
      const sent = createCompletion<void>();
      const confirmation = createCompletion<void>();
      fixture.channel.basicPublish.mockImplementation(() => {
        sent.resolve();
        return confirmation.promise;
      });
      const republisher = create(fixture);
      const result = republisher.republish(request);
      await sent.promise;
      try {
        fixture.channel.emit(event, new Error("Channel fixture event"));
        confirmation.resolve();
        expect(await result).toBe("transport-failure");
        expect(fixture.unsafeDestroy).toHaveBeenCalledOnce();
      } finally {
        confirmation.resolve();
        await republisher.close();
      }
    },
  );

  it("Republish_AdmissionAtCapacity_RejectsOnlyExcessOperation", async () => {
    const fixture = new DlqTransportFixture();
    fixture.completeAcquisition();
    const sent = createCompletion<void>();
    const confirmation = createCompletion<void>();
    fixture.channel.basicPublish.mockImplementationOnce(() => {
      sent.resolve();
      return confirmation.promise;
    });
    const republisher = create(fixture);
    const first = republisher.republish(request);
    await sent.promise;
    const second = republisher.republish(request);
    try {
      expect(await republisher.republish(request)).toBe("transport-failure");
      expect(fixture.unsafeDestroy).not.toHaveBeenCalled();
      confirmation.resolve();
      expect(await first).toBe("confirmed");
      expect(await second).toBe("confirmed");
      expect(fixture.channel.basicPublish).toHaveBeenCalledTimes(2);
    } finally {
      confirmation.resolve();
      await republisher.close();
    }
  });

  it("Republish_PreCanceled_DoesNotAllocateTransport", async () => {
    const fixture = new DlqTransportFixture();
    const cancellation = new AbortController();
    cancellation.abort();
    const republisher = create(fixture);
    try {
      expect(await republisher.republish(request, cancellation.signal)).toBe(
        "stopping",
      );
      expect(fixture.acquire).not.toHaveBeenCalled();
      expect(fixture.unsafeDestroy).not.toHaveBeenCalled();
    } finally {
      await republisher.close();
    }
  });

  it("Republish_ConnectionFactoryThrows_ReturnsTransportFailure", async () => {
    const republisher = new DlqRepublisher(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      { republishMs: 100, cleanupMs: 10 },
      1,
      new RecordingLogger(),
      {
        connectionFactory: () => {
          throw new Error("Connection fixture failed.");
        },
      },
    );
    try {
      expect(await republisher.republish(request)).toBe("transport-failure");
    } finally {
      await republisher.close();
      await republisher.resourceCompletion;
    }
  });

  it("Close_DisposalThrows_ObservesErrorsAndCompletesRemainingCleanup", async () => {
    const fixture = new DlqTransportFixture();
    fixture.completeAcquisition();
    const logger = new RecordingLogger();
    const republisher = new DlqRepublisher(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      { republishMs: 100, cleanupMs: 10 },
      1,
      logger,
      {
        connectionFactory: () => fixture.connection(),
        waitForReturns: async () => {},
      },
    );
    try {
      expect(await republisher.republish(request)).toBe("confirmed");
      fixture.channel.close.mockRejectedValue(
        new Error("Channel fixture failed."),
      );
      fixture.close.mockRejectedValue(new Error("Connection fixture failed."));
      await republisher.close();
      await republisher.resourceCompletion;
      expect(fixture.channel.close).toHaveBeenCalledOnce();
      expect(fixture.close).toHaveBeenCalledOnce();
      expect(logger.logged("DLQ transport cleanup failed")).toBe(true);
      expect(fixture.channel.listenerCount("error")).toBe(0);
      expect(fixture.channel.listenerCount("close")).toBe(0);
    } finally {
      await republisher.close();
    }
  });

  it("Republish_WaiterCancellation_DoesNotRetireActiveTransport", async () => {
    const fixture = new DlqTransportFixture();
    fixture.completeAcquisition();
    const confirmation = createCompletion<void>();
    const sent = createCompletion<void>();
    fixture.channel.basicPublish.mockImplementation(() => {
      sent.resolve();
      return confirmation.promise;
    });
    const republisher = create(fixture);
    const active = republisher.republish(request);
    await sent.promise;
    const cancellation = new AbortController();
    const waiting = republisher.republish(request, cancellation.signal);
    cancellation.abort();
    expect(await waiting).toBe("stopping");
    expect(fixture.unsafeDestroy).not.toHaveBeenCalled();
    confirmation.resolve();
    expect(await active).toBe("confirmed");
    await republisher.close();
  });

  it("Republish_SequentialConfirm_ReusesChannelAndPreservesMetadata", async () => {
    const fixture = new DlqTransportFixture();
    fixture.completeAcquisition();
    const republisher = create(fixture);
    const original = {
      ...request,
      messageId: "",
      contentType: "application/octet-stream",
      contentEncoding: "binary",
      correlationId: "fixture-correlation",
    };
    expect(await republisher.republish(original)).toBe("confirmed");
    expect(await republisher.republish(original)).toBe("confirmed");
    expect(fixture.acquire).toHaveBeenCalledOnce();
    expect(fixture.channel.confirmSelect).toHaveBeenCalledOnce();
    expect(fixture.channel.basicPublish).toHaveBeenLastCalledWith(
      {
        exchange: request.dlxName,
        routingKey: "",
        mandatory: true,
        durable: true,
        headers: {},
        messageId: "",
        contentType: original.contentType,
        contentEncoding: "binary",
        correlationId: "fixture-correlation",
      },
      request.body,
    );
    await republisher.close();
  });

  it("Close_DuringReturnDrain_CancelsBarrierWithoutConfirming", async () => {
    const fixture = new DlqTransportFixture();
    fixture.completeAcquisition();
    const entered = createCompletion<void>();
    const republisher = create(
      fixture,
      (signal) =>
        new Promise((_resolve, reject) => {
          entered.resolve();
          signal.addEventListener(
            "abort",
            () => reject(new Error("canceled")),
            { once: true },
          );
        }),
    );
    const result = republisher.republish(request);
    await entered.promise;
    const closed = republisher.close();
    expect(await result).toBe("stopping");
    await closed;
    await republisher.resourceCompletion;
    expect(fixture.unsafeDestroy).toHaveBeenCalledOnce();
    expect(fixture.channel.listenerCount("basic.return")).toBe(0);
  });

  it("Republish_LateAcquisition_DisposesWithoutPublishingOrReplacement", async () => {
    vi.useFakeTimers();
    const fixture = new DlqTransportFixture();
    const republisher = create(fixture);
    const result = republisher.republish(request);
    await fixture.acquired.promise;
    await vi.advanceTimersByTimeAsync(110);
    expect(await result).toBe("deadline-exceeded");
    expect(fixture.unsafeDestroy).toHaveBeenCalledOnce();
    expect(await republisher.republish(request)).toBe("cleanup-pending");
    expect(fixture.acquire).toHaveBeenCalledOnce();
    fixture.completeAcquisition();
    await republisher.close();
    await republisher.resourceCompletion;
    expect(fixture.channel.basicPublish).not.toHaveBeenCalled();
    expect(fixture.channel.close).toHaveBeenCalledOnce();
    expect(fixture.close).toHaveBeenCalledOnce();
  });

  it("Republish_ReturnDuringConfirmDrain_DoesNotConfirm", async () => {
    const fixture = new DlqTransportFixture();
    fixture.completeAcquisition();
    const barrier = createCompletion<void>();
    const entered = createCompletion<void>();
    const republisher = create(fixture, () => {
      entered.resolve();
      return barrier.promise;
    });
    const result = republisher.republish(request);
    await entered.promise;
    fixture.channel.emit("basic.return", {});
    barrier.resolve();
    expect(await result).toBe("unroutable");
    await republisher.close();
  });

  it("Republish_ConfirmAfterDeadlineWithoutTimerDelivery_DoesNotConfirm", async () => {
    vi.useFakeTimers();
    const fixture = new DlqTransportFixture();
    fixture.completeAcquisition();
    const republisher = create(fixture, async () => {
      vi.setSystemTime(Date.now() + 100);
    });
    expect(await republisher.republish(request)).toBe("deadline-exceeded");
    expect(fixture.unsafeDestroy).toHaveBeenCalledOnce();
    await republisher.close();
  });

  it("Close_RepeatedCall_ReturnsSameCompletionAndStopsAdmission", async () => {
    const fixture = new DlqTransportFixture();
    const republisher = create(fixture);
    const first = republisher.close();
    expect(republisher.close()).toBe(first);
    await first;
    expect(await republisher.republish(request)).toBe("stopping");
    expect(fixture.acquire).not.toHaveBeenCalled();
  });
});
