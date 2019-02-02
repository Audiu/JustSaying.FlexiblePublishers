using JustSaying.Models;

namespace JustSaying.FlexiblePublishers.Queued
{
    public class MessageContainer
    {
        public Message Message { get; set; }
        public bool IsWhitelisted { get; set; }
    }
}