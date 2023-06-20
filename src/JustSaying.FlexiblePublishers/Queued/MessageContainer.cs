using JustSaying.Messaging;
using JustSaying.Models;

namespace JustSaying.FlexiblePublishers.Queued
{
    public class MessageContainer
    {
        public Message Message { get; }
        public PublishMetadata Metadata { get; }
        public bool IsWhitelisted { get; }

        public MessageContainer(Message message, PublishMetadata metadata, bool isWhitelisted)
        {
            Message = message;
            Metadata = metadata;
            IsWhitelisted = isWhitelisted;
        }
    }
}