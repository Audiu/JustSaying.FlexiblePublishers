using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JustSaying.Messaging;
using JustSaying.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace JustSaying.FlexiblePublishers.Queued
{
    public class QueuedMessagePublisher : IQueuedMessagePublisher
    {
        private readonly ILogger<IQueuedMessagePublisher> _logger;
        private readonly IMessagePublisher _messagePublisher;

        private readonly Queue<MessageContainer> _queuedMessages = new Queue<MessageContainer>();

        public QueuedMessagePublisher(ILoggerFactory loggerFactory, IMessagePublisher messagePublisher)
        {
            _messagePublisher = messagePublisher;
            _logger = loggerFactory.CreateLogger<QueuedMessagePublisher>();
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

            using (_logger.BeginScope(new Dictionary<string, object> {{"tracer", tracer.ToString()}}))
            {

                if (QueuedItems == 0)
                {
                    _logger.LogDebug("No queued messages");

                    return;
                }

                _logger.LogInformation($"Sending {_queuedMessages.Count} queued messages"); 

                while (QueuedItems > 0)
                {
                    var container = _queuedMessages.Dequeue();

                    var messageType = container.Message.GetType().Name;

                    using (_logger.BeginScope(new Dictionary<string, object>
                    {
                        { "isWhitelisted", container.IsWhitelisted },
                        { "messageType", messageType },
                        { "messageBody", JsonConvert.SerializeObject(container.Message) }
                    }))
                    {

                        if (!onlySendWhitelisted || container.IsWhitelisted)
                        {
                            try
                            {
                                await _messagePublisher.PublishAsync(container.Message, cancellationToken);

                                _logger.LogInformation($"Published message successfully - MessageType: {messageType}");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Failed to post message - MessageType: {messageType}");
                            }
                        }
                        else
                        {
                            _logger.LogWarning(
                                $"Dropping message as it is not whitelisted, and only sending whitelisted messages - MessageType: {messageType}");
                        }
                    }

                }

                _logger.LogInformation("Finished sending queued messages");
            }
        }
    }
}