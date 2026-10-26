// -----------------------------------------------------------------------
// <copyright file="TypedHandlerDispatcher.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Subscribing;

using DcsvIo.D2.Handler;
using DcsvIo.D2.Messaging.RabbitMq.Encryption;
using DcsvIo.D2.Result;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Closed-over-generics <see cref="IHandlerDispatcher"/>. It is closed over the
/// concrete <typeparamref name="TSubscriber"/> handler and <typeparamref name="TMessage"/>
/// at compile time, so per-message dispatch uses no reflection.
/// </summary>
/// <typeparam name="TSubscriber">
/// The subscriber handler type, registered in DI as Transient. Constrained
/// to <see cref="BaseHandler{TSelf, TInput, TOutput}"/> so the dispatcher can call
/// <see cref="BaseHandler{TSelf, TInput, TOutput}.HandleAsync"/> directly.
/// </typeparam>
/// <typeparam name="TMessage">The message payload type.</typeparam>
/// <remarks>
/// This dispatcher does not populate the per-message <c>IRequestContext</c>
/// from the wire, and the wire has no envelope. The W3C <c>traceparent</c>
/// AMQP header carries cross-hop trace correlation. Any caller-identity,
/// organization or scope fields the handler needs come from the typed
/// message body itself.
/// </remarks>
/// <param name="catalog">The message descriptors this dispatcher decodes
/// against.</param>
internal sealed class TypedHandlerDispatcher<TSubscriber, TMessage>(MessagingCatalog catalog)
    : IHandlerDispatcher
    where TSubscriber : BaseHandler<TSubscriber, TMessage, Unit>
    where TMessage : class
{
    /// <inheritdoc />
    public async ValueTask<D2Result> DispatchAsync(
        IServiceProvider scope,
        ReadOnlyMemory<byte> body,
        CancellationToken ct)
    {
        TMessage message;
        try
        {
            // Resolve the wire descriptor and decode in one step. The throw
            // semantics match the publisher: a missing [MqPub] or an unknown
            // constant surfaces as InvalidOperationException, which the catch
            // below wraps as a decode failure (bound for the DLQ). The
            // descriptor tells the composer whether to AEAD-decrypt or treat
            // the body as plaintext.
            var descriptor = MessageWireResolver.Resolve(catalog, typeof(TMessage));
            message = EncryptedBodyComposer.Decompose<TMessage>(body.Span, descriptor, scope);
        }
        catch (Exception ex)
        {
            // Surface the failure as a typed boundary failure. Do not wrap it
            // in D2Result.UnhandledException, because decode failures get a
            // different DLQ cause.
            throw new MessageBodyDecodeException(
                "Failed to decode message body. See inner exception for cause.", ex);
        }

        var handler = scope.GetRequiredService<TSubscriber>();
        var result = await handler.HandleAsync(message, ct);
        return result;
    }
}
