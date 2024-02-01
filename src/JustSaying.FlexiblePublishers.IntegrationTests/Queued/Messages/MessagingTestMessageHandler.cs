using System;
using System.Threading.Tasks;
using JustSaying.FlexiblePublishers.Queued;
using JustSaying.Messaging;
using JustSaying.Messaging.MessageHandling;

namespace JustSaying.FlexiblePublishers.IntegrationTests.Queued.Messages;

public class MessagingTestMessageHandler : IHandlerAsync<MessagingTestMessage>
{
    private readonly IQueuedMessagePublisher _messagePublisher;

    public static string LastUniqueId { get; private set; }
    public static string RelayUniqueId { get; private set; }
    public static string RelayWhitelistUniqueId { get; private set; }

    public MessagingTestMessageHandler(IQueuedMessagePublisher messagePublisher)
    {
        _messagePublisher = messagePublisher;
    }

    public static void ResetIds()
    {
        LastUniqueId = null;
        RelayUniqueId = null;
        RelayWhitelistUniqueId = null;
    }

    public async Task<bool> Handle(MessagingTestMessage message)
    {
        LastUniqueId = message.UniqueId;

        RelayUniqueId = Guid.NewGuid().ToString();
        RelayWhitelistUniqueId = Guid.NewGuid().ToString();

        await _messagePublisher.PublishAsync(new RelayMessage(RelayUniqueId));
        await _messagePublisher.PublishAsync(new RelayWhitelistMessage(RelayWhitelistUniqueId), isWhitelisted: true);

        switch (message.Result)
        {
            case MessagingTestMessageResultsEnum.Success:
                return true;
            case MessagingTestMessageResultsEnum.Fail:
                return false;
            case MessagingTestMessageResultsEnum.Exception:
                throw new Exception("Test Exception");
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
