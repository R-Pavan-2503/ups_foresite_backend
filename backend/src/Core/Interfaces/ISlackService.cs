namespace CodeFamily.Api.Core.Interfaces;

public interface ISlackService
{
    /// <summary>
    /// Send a direct message to a user.
    /// userId should be the Slack user ID or email.
    /// </summary>
    Task SendDirectMessage(string userId, string message);

    /// <summary>
    /// Post a message to a channel.
    /// </summary>
    Task PostMessage(string channel, string message);
}
