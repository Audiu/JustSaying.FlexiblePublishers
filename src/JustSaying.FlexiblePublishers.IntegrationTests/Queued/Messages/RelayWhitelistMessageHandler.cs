using System.Threading.Tasks;
using JustSaying.Messaging.MessageHandling;

namespace JustSaying.FlexiblePublishers.IntegrationTests.Queued.Messages;

public class RelayWhitelistMessageHandler : IHandlerAsync<RelayWhitelistMessage>
{
    public static string LastUniqueId { get; private set; }

    public async Task<bool> Handle(RelayWhitelistMessage message)
    {
        LastUniqueId = message.UniqueId;

        return true;
    }

    public static void ResetIds()
    {
        LastUniqueId = null;
    }
}
