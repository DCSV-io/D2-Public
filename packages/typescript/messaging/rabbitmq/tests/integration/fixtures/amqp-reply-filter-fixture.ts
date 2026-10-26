// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import {
  createConnection,
  createServer,
  type Server,
  type Socket,
} from "node:net";
import { createCompletion } from "../../../src/connection/managed-connection.js";

export type SuppressedReply =
  | "channel-open"
  | "confirm-select"
  | "confirmation";
export type ReturnDelivery = "return-confirm-together" | "return-confirm-split";

/** Owns a test-only TCP proxy that withholds selected AMQP method replies. */
export class AmqpReplyFilterFixture {
  private readonly server: Server;
  private readonly sockets = new Set<Socket>();
  private readonly held: Array<{ socket: Socket; frame: Buffer }> = [];
  private readonly suppressed = createCompletion<void>();
  private readonly disconnected = createCompletion<void>();
  readonly replySuppressed = this.suppressed.promise;
  readonly clientDisconnected = this.disconnected.promise;
  private port = 0;
  private accepting = true;

  constructor(
    private readonly hostname: string,
    private readonly upstreamPort: number,
    private readonly reply: SuppressedReply | ReturnDelivery,
  ) {
    this.server = createServer((client) => this.forward(client));
  }

  get uri(): string {
    return `amqp://guest:guest@127.0.0.1:${this.port}`;
  }

  async start(): Promise<void> {
    await new Promise<void>((resolve, reject) => {
      this.server.once("error", reject);
      this.server.listen(0, "127.0.0.1", () => {
        this.server.off("error", reject);
        const address = this.server.address();
        if (address === null || typeof address === "string") {
          reject(new Error("Missing proxy address."));
          return;
        }
        this.port = address.port;
        resolve();
      });
    });
  }

  async close(): Promise<void> {
    this.accepting = false;
    this.held.length = 0;
    const closed = [...this.sockets].map(
      (socket) =>
        new Promise<void>((resolve) => {
          socket.once("close", () => resolve());
          socket.destroy();
        }),
    );
    if (this.server.listening)
      await new Promise<void>((resolve, reject) =>
        this.server.close((error) => (error ? reject(error) : resolve())),
      );
    await Promise.all(closed);
  }

  releaseHeld(): void {
    for (const held of this.held.splice(0))
      if (!held.socket.destroyed) held.socket.write(held.frame);
  }

  private forward(client: Socket): void {
    if (!this.accepting || this.sockets.size >= 32) {
      client.destroy();
      return;
    }
    const upstream = createConnection({
      host: this.hostname,
      port: this.upstreamPort,
    });
    this.sockets.add(client);
    this.sockets.add(upstream);
    client.setNoDelay(true);
    upstream.setNoDelay(true);
    const stop = (): void => {
      client.destroy();
      upstream.destroy();
    };
    client.on("error", stop);
    upstream.on("error", stop);
    client.once("close", () => {
      this.sockets.delete(client);
      upstream.destroy();
      this.disconnected.resolve();
    });
    upstream.once("close", () => {
      this.sockets.delete(upstream);
      client.destroy();
    });
    client.pipe(upstream);
    let buffered = Buffer.alloc(0);
    let returning: Buffer[] | undefined;
    let returnBytes = 0;
    const write = (frame: Buffer): void => {
      if (client.destroyed) return;
      if (!client.write(frame)) {
        upstream.pause();
        client.once("drain", () => upstream.resume());
      }
    };
    upstream.on("data", (chunk: Buffer) => {
      buffered = Buffer.concat([buffered, chunk]);
      while (buffered.length >= 7) {
        const size = buffered.readUInt32BE(3);
        if (size > 8 * 1024 * 1024) {
          stop();
          return;
        }
        const frameLength = 8 + size;
        if (buffered.length < frameLength) return;
        const frame = buffered.subarray(0, frameLength);
        buffered = buffered.subarray(frameLength);
        const method = frame[0] === 1 && size >= 4;
        const classId = method ? frame.readUInt16BE(7) : 0;
        const methodId = method ? frame.readUInt16BE(9) : 0;
        if (
          this.reply === "return-confirm-together" ||
          this.reply === "return-confirm-split"
        ) {
          if (classId === 60 && methodId === 50) {
            returning = [];
            returnBytes = 0;
          }
          if (returning !== undefined) {
            returnBytes += frame.length;
            if (returnBytes > 8 * 1024 * 1024) {
              stop();
              return;
            }
            if (classId === 60 && methodId === 80) {
              if (this.reply === "return-confirm-together")
                write(Buffer.concat([...returning, frame]));
              else {
                write(Buffer.concat(returning));
                this.held.push({ socket: client, frame: Buffer.from(frame) });
                this.suppressed.resolve();
              }
              returning = undefined;
            } else returning.push(Buffer.from(frame));
            continue;
          }
        }
        if (
          frame[0] === 1 &&
          size >= 4 &&
          this.matches(frame.readUInt16BE(7), frame.readUInt16BE(9))
        ) {
          if (this.held.length >= 32) {
            stop();
            return;
          }
          this.held.push({ socket: client, frame: Buffer.from(frame) });
          this.suppressed.resolve();
        } else write(frame);
      }
    });
  }

  private matches(classId: number, methodId: number): boolean {
    return (
      (this.reply === "channel-open" && classId === 20 && methodId === 11) ||
      (this.reply === "confirm-select" && classId === 85 && methodId === 11) ||
      (this.reply === "confirmation" &&
        classId === 60 &&
        (methodId === 80 || methodId === 120))
    );
  }
}
