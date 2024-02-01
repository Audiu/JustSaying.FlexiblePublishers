namespace JustSaying.FlexiblePublishers.IntegrationTests.Queued.Messages;

public class MessagingTestMessage : TestMessageBase
{
    public MessagingTestMessageResultsEnum Result { get; }

    protected MessagingTestMessage(string uniqueId) : base(uniqueId)
    {
    }

    public MessagingTestMessage(string uniqueId, MessagingTestMessageResultsEnum result) : base(uniqueId)
    {
        Result = result;
    }
}
