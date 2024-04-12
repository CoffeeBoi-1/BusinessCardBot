namespace LandingBot.Models
{
    public class User
    {
        public long ID { get; set; }
        public SubscriptionLevel SubscriptionLevel { get; set; }
        public bool HadFreeSubscription { get; set; }
        public DateTime SubscriptionPurchased { get; set; }
        public DateTime SubscriptionExpires { get; set; }
    }
}
