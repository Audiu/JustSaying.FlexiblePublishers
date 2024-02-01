using JustSaying.FlexiblePublishers.Queued.Middleware;

// ReSharper disable once CheckNamespace
namespace JustSaying.Messaging.Middleware
{
    public static class MiddlewareExtensions
    {
        public static HandlerMiddlewareBuilder UseQueuedMessagesMiddleware(this HandlerMiddlewareBuilder builder)
        {
            builder.Use<QueuedMessagesMiddleware>();

            return builder;
        }
    }
}