using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JustSaying.Messaging;
using JustSaying.Models;

namespace JustSaying.FlexiblePublishers.Queued
{
    public class QueuedMessagePublisher : IQueuedMessagePublisher
    {
        private readonly IMessagePublisher _messagePublisher;

        private readonly Queue<MessageContainer> _queuedMessages = new Queue<MessageContainer>();

        public QueuedMessagePublisher(IMessagePublisher messagePublisher)
        {
            _messagePublisher = messagePublisher;
        }

        public Task PublishAsync(Message message)
        {
            _queuedMessages.Enqueue(new MessageContainer
            {
                Message = message,
                IsWhitelisted = false
            });

            return Task.CompletedTask;
        }

        public Task PublishAsync(Message message, CancellationToken cancellationToken)
        {
            _queuedMessages.Enqueue(new MessageContainer
            {
                Message = message,
                IsWhitelisted = false
            });

            return Task.CompletedTask;
        }

        public Task PublishAsync(Message message, bool isWhitelisted)
        {
            _queuedMessages.Enqueue(new MessageContainer
            {
                Message = message,
                IsWhitelisted = isWhitelisted
            });

            return Task.CompletedTask;
        }

        public Task PublishAsync(Message message, bool isWhitelisted, CancellationToken cancellationToken)
        {
            _queuedMessages.Enqueue(new MessageContainer
            {
                Message = message,
                IsWhitelisted = isWhitelisted
            });

            return Task.CompletedTask;
        }

        public int QueuedItems => _queuedMessages.Count;

        public async Task ProcessQueueAsync(bool onlySendWhitelisted, CancellationToken cancellationToken)
        {
            var tracer = Guid.NewGuid();

            if (QueuedItems == 0)
            {
                //_logger.Debug("Trace {traceId}: No queued messages", tracer);

                return;
            }

            //_logger.Information(
            //    "Trace {traceId}: Sending {messageCount} queued messages",
            //    tracer,
            //    _queuedMessages.Count);

            while (QueuedItems > 0)
            {
                var container = _queuedMessages.Dequeue();

                if (!onlySendWhitelisted || container.IsWhitelisted)
                {
                    try
                    {
                        await _messagePublisher.PublishAsync(container.Message, cancellationToken);

                        //_logger.Information(
                        //    "Posted message to SQS - Queue: {queueUrl}, MessageBody: {messageBody}",
                        //    container.SendMessageRequest.QueueUrl,
                        //    container.SendMessageRequest.MessageBody);
                    }
                    catch (Exception ex)
                    {
                        //_logger.Error(
                        //    ex,
                        //    "Failed to post message to Queue: {queueUrl}, MessageBody: {messageBody}",
                        //    container.SendMessageRequest.QueueUrl,
                        //    container.SendMessageRequest.MessageBody);
                    }
                }
                else
                {
                    //_logger.Warning(
                    //    "Dropping message as it is not whitelisted, and only sending whitelisted messages - Queue: {queueUrl}, MessageBody: {messageBody}",
                    //    container.SendMessageRequest.QueueUrl,
                    //    container.SendMessageRequest.MessageBody);
                }
            }
          
            //_logger.Information("Trace {traceId}: Finished sending queued messages", tracer);
        }
    }
}