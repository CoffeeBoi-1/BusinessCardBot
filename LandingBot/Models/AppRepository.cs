using Dapper;
using MySqlConnector;
using Telegram.Bot.Types;

namespace LandingBot.Models
{
    public class AppRepository : IAppRepository
    {
        private MySqlDataSource _dataSource;
        public AppRepository(MySqlDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        public async Task<User?> GetFullUserInfo(long userID)
        {
            using (MySqlConnection conn = _dataSource.OpenConnection())
            {
                var query = @"SELECT U.ID, U.HadFreeSubscription, U.SubscriptionPurchased, U.SubscriptionExpires, S.`Level`, S.Name 
                      FROM users U 
                      INNER JOIN subscriptiontypes S ON U.SubscriptionLevel = S.`Level` 
                      WHERE U.ID = @UserId";

                User? user = null;

                IEnumerable<User>? result = await conn.QueryAsync<User, SubscriptionLevel, User>(
                    query,
                    (u, s) =>
                    {
                        u.SubscriptionLevel = s;
                        user = u;
                        return user;
                    },
                    new { UserId = userID },
                    splitOn: "Level"
                );

                return user;
            }
        }

        public async Task SetUserSubscriptionLevel(long userID, int subscriptionLevel, DateTime subscriptionPurchased, DateTime subscriptionExpires, bool isTrial = false)
        {
            string sql = @$"INSERT INTO users (ID, SubscriptionLevel, SubscriptionPurchased, SubscriptionExpires, HadFreeSubscription) VALUES({userID}, {subscriptionLevel}, '{subscriptionPurchased.ToString("yyyy.M.d")}', '{subscriptionExpires.ToString("yyyy.M.d")}', {isTrial}) 
                       ON DUPLICATE KEY UPDATE SubscriptionLevel = {subscriptionLevel}, SubscriptionPurchased = '{subscriptionPurchased.ToString("yyyy.M.d")}', SubscriptionExpires = '{subscriptionExpires.ToString("yyyy.M.d")}', HadFreeSubscription = {isTrial};";

            using (MySqlConnection conn = _dataSource.OpenConnection())
                await conn.QueryAsync(sql);
        }

        public async Task<bool> UserAbleToChangeSubscriptionLevel(long userID, int newSubscriptionLevel)
        {
            using (MySqlConnection conn = _dataSource.OpenConnection())
                return await conn.ExecuteScalarAsync<bool>(
                $"SELECT IF(NOT EXISTS(SELECT 1 FROM users WHERE ID = {userID}) OR SubscriptionLevel <= {newSubscriptionLevel}, 1, 0) AS result FROM (SELECT 1 AS dummy) AS dummy_table LEFT JOIN users ON users.ID = {userID};"
                );
        }

        public async Task SetUserInfoMessage(long userID, string message, string infoTable)
        {
            string sql = $"INSERT INTO {infoTable} (UserID, Message) VALUES({userID}, '{message}') ON DUPLICATE KEY UPDATE Message = '{message}';";

            using (MySqlConnection conn = _dataSource.OpenConnection())
                await conn.QueryAsync(sql);
        }

        public async Task<string> GetUserInfoMessage(long userID, string infoTable)
        {
            string sql = $"SELECT IFNULL((SELECT Message FROM {infoTable} WHERE UserID = {userID}), (SELECT DEFAULT(Message) FROM {infoTable} LIMIT 1)) AS Message;";

            using (MySqlConnection conn = _dataSource.OpenConnection())
                return await conn.QueryFirstAsync<string>(sql);
        }

        public async Task<int> GetUserSubscriptionLevel(long userID)
        {
            string sql = $"SELECT SubscriptionLevel FROM users WHERE id = {userID};";

            using (MySqlConnection conn = _dataSource.OpenConnection())
                return await conn.QueryFirstOrDefaultAsync<int>(sql);
        }
    }
}
