using System.Threading;
using System.Threading.Tasks;
using JustSaying.Messaging;
using JustSaying.Models;

namespace JustSaying.FlexiblePublishers.Queued
{
    public interface IQueuedMessagePublisher : IMessagePublisher
    {
        Task PublishAsync(Message message, PublishMetadata metadata, bool isWhitelisted, CancellationToken cancellationToken = default(CancellationToken));
        
        int QueuedItems { get; }

        Task ProcessQueueAsync(bool onlySendWhitelisted, CancellationToken cancellationToken);
    }
}
