using System;
using System.Threading;
using System.Threading.Tasks;
using JustSaying.Messaging.Middleware;

namespace JustSaying.FlexiblePublishers.Queued.Middleware
{
    public class QueuedMessagesMiddleware : MiddlewareBase<HandleMessageContext, bool>
    {
        private readonly IQueuedMessagePublisher _queuedMessagePublisher;

        public QueuedMessagesMiddleware(IQueuedMessagePublisher queuedMessagePublisher)
        {
            _queuedMessagePublisher = queuedMessagePublisher;
        }

        protected override async Task<bool> RunInnerAsync(
            HandleMessageContext context,
            Func<CancellationToken, Task<bool>> func,
            CancellationToken stoppingToken)
        {
            try
            {
                var result = await func(stoppingToken);
            
                if (result)
                {
                    await _queuedMessagePublisher.ProcessQueueAsync(
                        onlySendWhitelisted: false,
                        cancellationToken: stoppingToken);
                }
                else
                {
                    await _queuedMessagePublisher.ProcessQueueAsync(
                        onlySendWhitelisted: true,
                        cancellationToken: stoppingToken);
                }
            
                return result;
            }
            catch (Exception)
            {
                await _queuedMessagePublisher.ProcessQueueAsync(
                    onlySendWhitelisted: true,
                    cancellationToken: stoppingToken);
            
                throw;
            }
        }
    }
}