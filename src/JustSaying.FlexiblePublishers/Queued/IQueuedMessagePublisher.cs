using System.Threading;
using System.Threading.Tasks;
using JustSaying.Messaging;
using JustSaying.Models;

namespace JustSaying.FlexiblePublishers.Queued
{
    public interface IQueuedMessagePublisher : IMessagePublisher
    {
        Task PublishAsync(Message message, bool isWhitelisted);

        Task PublishAsync(Message message, bool isWhitelisted, CancellationToken cancellationToken);

        int QueuedItems { get; }

        Task ProcessQueueAsync(bool onlySendWhitelisted, CancellationToken cancellationToken);
    }
}
