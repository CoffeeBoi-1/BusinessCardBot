namespace LandingBot.Models
{
    public interface IAppRepository
    {
        Task SetUserSubscriptionLevel(long userID, int subscriptionLevel, DateTime subscriptionPurchased, DateTime subscriptionExpires, bool isTrial = false);
        Task<bool> UserAbleToChangeSubscriptionLevel(long userID, int subscriptionLevel);
        Task<User?> GetFullUserInfo(long userID);
        Task SetUserInfoMessage(long userID, string message, string infoTable);
        Task<string> GetUserInfoMessage(long userID, string infoTable);
        Task<int> GetUserSubscriptionLevel(long userID);
    }
}
