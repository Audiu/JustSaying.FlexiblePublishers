using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSaying.Messaging;
using JustSaying.Messaging.Interrogation;
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

        public Task PublishAsync(Message message, CancellationToken cancellationToken)
        {
            return PublishAsync(message, null, false, cancellationToken);
        }

        public Task PublishAsync(Message message, PublishMetadata metadata, CancellationToken cancellationToken)
        {
            return PublishAsync(message, metadata, false, cancellationToken);
        }
        
        public Task PublishAsync(Message message, bool isWhitelisted, CancellationToken cancellationToken = default(CancellationToken))
        {
            return PublishAsync(message, null, isWhitelisted, cancellationToken);
        }

        public Task PublishAsync(Message message, PublishMetadata metadata, bool isWhitelisted, CancellationToken cancellationToken)
        {
            if (message == null)
            {
                _logger.LogWarning("Received null message content, not queueing message");

                return Task.CompletedTask;
            }

            if (_queuedMessages.Any(x => x.Message.Equals(message)))
            {
                _logger.LogWarning($"Received duplicated message content, not queueing message - message content: {JsonConvert.SerializeObject(message)}");

                return Task.CompletedTask;
            }

            _queuedMessages.Enqueue(new MessageContainer(message, metadata, isWhitelisted));

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
                                if (container.Metadata != null)
                                {
                                    await _messagePublisher.PublishAsync(container.Message, container.Metadata, cancellationToken);
                                }
                                else
                                {
                                    await _messagePublisher.PublishAsync(container.Message, cancellationToken);
                                }

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

        public InterrogationResult Interrogate()
        {
            return _messagePublisher.Interrogate();
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            return _messagePublisher.StartAsync(stoppingToken);
        }
    }
}