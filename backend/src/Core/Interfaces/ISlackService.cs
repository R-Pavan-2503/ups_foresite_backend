namespace CodeFamily.Api.Core.Interfaces;

public interface ISlackService
{
    Task SendDirectMessage(string userId, string message);
    Task PostMessage(string channel, string message);
}
