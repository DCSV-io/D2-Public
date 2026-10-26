// -----------------------------------------------------------------------
// <copyright file="OutboxMessageRowMapper.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Outbox.EntityFrameworkCore;

/// <summary>
/// Pure mapper between <see cref="OutboxMessageRow"/> and the
/// <see cref="OutboxMessage"/> state family.
/// </summary>
/// <remarks>
/// <b>The write path is <c>ProjectOnto</c> and not the conventional
/// <c>ToRow</c>.</b> A row mapper's <c>ToRow</c> returns a fresh instance, which
/// suits code that owns the row. Every write here changes a row that was
/// already loaded and is already tracked by the change tracker, and the
/// concurrency token belongs to that tracked instance, so returning a new row
/// and assigning it would either detach the token or add a second entity for
/// the same key. <c>ToNewRow</c> is the other half of the conventional pair and
/// keeps its name, because nothing tracks a message that has never been
/// persisted.
/// </remarks>
public static class OutboxMessageRowMapper
{
    /// <param name="row">Flat persistence row.</param>
    extension(OutboxMessageRow row)
    {
        /// <summary>Rehydrates the message this row records.</summary>
        /// <returns>The sealed state matching the row's status.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the row's status identifies a state whose own columns are
        /// absent, which no writer here can produce.
        /// </exception>
        public OutboxMessage ToDomain() => row.Status switch
        {
            OutboxMessageStatus.Staged => new StagedOutboxMessage
            {
                Id = new OutboxMessageId(row.Id),
                Kind = row.Kind,
                Payload = row.Payload,
                StagedAt = row.StagedAt,
                Attempts = row.Attempts,
                NextAttemptAt = row.NextAttemptAt,
                LastFailureCode = row.LastFailureCode,
            },
            OutboxMessageStatus.Published => new PublishedOutboxMessage
            {
                Id = new OutboxMessageId(row.Id),
                Kind = row.Kind,
                Payload = row.Payload,
                StagedAt = row.StagedAt,
                Attempts = row.Attempts,
                PublishedAt = row.PublishedAt
                    ?? throw new InvalidOperationException(
                        "Published outbox row missing PublishedAt."),
            },
            OutboxMessageStatus.Abandoned => new AbandonedOutboxMessage
            {
                Id = new OutboxMessageId(row.Id),
                Kind = row.Kind,
                Payload = row.Payload,
                StagedAt = row.StagedAt,
                Attempts = row.Attempts,
                AbandonedAt = row.AbandonedAt
                    ?? throw new InvalidOperationException(
                        "Abandoned outbox row missing AbandonedAt."),
                LastFailureCode = row.LastFailureCode
                    ?? throw new InvalidOperationException(
                        "Abandoned outbox row missing LastFailureCode."),
            },
            _ => throw new InvalidOperationException(
                $"Outbox row carries an unhandled status: {row.Status}."),
        };
    }

    /// <param name="message">The message whose state is being persisted.</param>
    extension(OutboxMessage message)
    {
        /// <summary>
        /// Writes this state onto a tracked row in place, nulling the columns
        /// the new state does not use so no leftover from a previous state
        /// survives.
        /// </summary>
        /// <param name="row">The tracked row to mutate.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="row"/> is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the message is a state this mapper does not handle.
        /// </exception>
        public void ProjectOnto(OutboxMessageRow row)
        {
            ArgumentNullException.ThrowIfNull(row);

            row.Id = message.Id.Value;
            row.Kind = message.Kind;
            row.Payload = message.Payload;
            row.StagedAt = message.StagedAt;
            row.Attempts = message.Attempts;
            row.Status = message.Status;

            switch (message)
            {
                case StagedOutboxMessage staged:
                    row.NextAttemptAt = staged.NextAttemptAt;
                    row.PublishedAt = null;
                    row.AbandonedAt = null;
                    row.LastFailureCode = staged.LastFailureCode;
                    break;

                case PublishedOutboxMessage published:
                    row.NextAttemptAt = null;
                    row.PublishedAt = published.PublishedAt;
                    row.AbandonedAt = null;
                    row.LastFailureCode = null;
                    break;

                case AbandonedOutboxMessage abandoned:
                    row.NextAttemptAt = null;
                    row.PublishedAt = null;
                    row.AbandonedAt = abandoned.AbandonedAt;
                    row.LastFailureCode = abandoned.LastFailureCode;
                    break;

                default:
                    throw new InvalidOperationException(
                        "ProjectOnto reached an unhandled outbox message leaf: "
                        + message.GetType().Name);
            }
        }

        /// <summary>
        /// Builds a fresh row for a message that has never been persisted.
        /// </summary>
        /// <returns>A row holding this message's state.</returns>
        public OutboxMessageRow ToNewRow()
        {
            var row = new OutboxMessageRow
            {
                Id = message.Id.Value,
                Kind = message.Kind,
                Payload = message.Payload,
                StagedAt = message.StagedAt,
                Status = message.Status,
                Attempts = message.Attempts,
            };

            message.ProjectOnto(row);
            return row;
        }
    }
}
