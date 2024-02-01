using System;
using System.Threading;
using System.Threading.Tasks;
using JustSaying.Fluent;
using JustSaying.Messaging.Middleware;

namespace JustSaying.FlexiblePublishers.Queued.Middleware
{
    public class QueuedMessagesMiddleware : MiddlewareBase<HandleMessageContext, bool>
    {
        private readonly IServiceResolver _resolver;

        public QueuedMessagesMiddleware(IServiceResolver resolver)
        {
            _resolver = resolver;
        }

        protected override async Task<bool> RunInnerAsync(
            HandleMessageContext context,
            Func<CancellationToken, Task<bool>> func,
            CancellationToken stoppingToken)
        {
            var queuedMessagePublisher = _resolver.ResolveService<IQueuedMessagePublisher>();

            try
            {
                var result = await func(stoppingToken);

                if (result)
                {
                    await queuedMessagePublisher.ProcessQueueAsync(
                        onlySendWhitelisted: false,
                        cancellationToken: stoppingToken);
                }
                else
                {
                    await queuedMessagePublisher.ProcessQueueAsync(
                        onlySendWhitelisted: true,
                        cancellationToken: stoppingToken);
                }

                return result;
            }
            catch (Exception)
            {
                await queuedMessagePublisher.ProcessQueueAsync(
                    onlySendWhitelisted: true,
                    cancellationToken: stoppingToken);

                throw;
            }
        }
    }
}
