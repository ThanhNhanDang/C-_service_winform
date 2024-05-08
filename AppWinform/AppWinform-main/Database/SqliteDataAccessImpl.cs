using System.Data;
using System.Data.SQLite;
using System.Reflection;

using Dapper;
using static Dapper.SqlMapper;

namespace AppWinform_main.Database
{
    internal class SqliteDataAccessImpl<T> : ISqliteDataAccess<T> where T : class
    {
        private static IDbConnection conn = new SQLiteConnection(SqliteUtil.DATA_BASE_DIRECTORY);
        public SqliteDataAccessImpl()
        {
        }
        public async Task<List<T>?> GetAll(string orderBy)
        {
            List<T> entities;
            IEnumerable<T> output = await conn.QueryAsync<T>($"select * from {typeof(T).Name} order by {orderBy}", new DynamicParameters());
            entities = output.ToList();
            if (!entities.Any()) return null;
            return entities;

        }
        public async Task<T?> FindByKey<TValue>(string key, TValue value)
        {
            Task<T?> entity;
            try
            {
                entity = conn.QueryFirstOrDefaultAsync<T>($"select * from TagInfo where {key}='{value}'");
                await entity;
                if (entity.Result == null)
                {
                    return null;
                }
                return entity.Result;
            }
            catch (SQLiteException)
            {
                return null;
            }
        }

        public async Task<T?> FindByForeignKey<TValue>(string key, TValue value)
        {
            Task<T?> entity;
            try
            {
                entity = conn.QueryFirstOrDefaultAsync<T>($"select * from TagInfo where {key}='{value}'");
                await entity;
                if (entity.Result == null)
                {
                    return null;
                }
                return entity.Result;
            }
            catch (SQLiteException)
            {
                return null;
            }
        }

        public async Task<T?> UpdateByKey<TValue, TValueCondition>(string key, TValue value, string keyCondition, TValueCondition valueCondition)
        {
            string query = $"UPDATE {typeof(T).Name} SET ";
            query += $"{key} = '{value}'";
            query += $"WHERE {keyCondition} = '{valueCondition}'";
            try
            {
                await conn.ExecuteAsync(query);
            }
            catch (SQLiteException)
            {
                return null;
            }
            return await FindByKey(keyCondition, valueCondition);
        }

        public async Task<T?> UpdateByMulKey<TValue, TValueCondition>(string[] key, TValue[] value, string keyCondition, TValueCondition valueCondition)
        {
            if (value.Length == 0 || key.Length == 0) return null;
            if (value.Length != key.Length) return null;

            int length = key.Length;
            string query = $"UPDATE {typeof(T).Name} SET ";

            for (int i = 0; i < length; i++)
            {
                query += $"{key[i]} = '{value[i]}'";
                if (i < length - 1) query += ", ";
            }
            query += $"WHERE {keyCondition} = '{valueCondition}'";
            try
            {
                await conn.ExecuteAsync(query);
            }
            catch (SQLiteException)
            {
                return null;
            }
            return await FindByKey(keyCondition, valueCondition);
        }

        public async Task Save(T entity)
        {
            //Return all public properties of the current Type
            Type obj = typeof(T);
            PropertyInfo[] propertyInfos = obj.GetProperties();
            string str1 = "";
            string str2 = "";
            int l = propertyInfos.Length - 1;
            str1 += propertyInfos[l].Name;
            str2 += "@" + propertyInfos[l].Name;
            for (int i = l - 1; i >= 0; i--)
            {
                if (propertyInfos[i].Name != "id" && propertyInfos[i].Name != "createDateTime")
                {
                    if (i != 0) { str1 += ","; str2 += ","; }
                    str1 += propertyInfos[i].Name;
                    str2 += "@" + propertyInfos[i].Name;
                }
                else
                    continue;
            }
            try
            {
                await conn.ExecuteAsync($"insert into {obj.Name} ({str1})values({str2})", entity);
            }
            catch (SQLiteException)
            {
                return;
            }
        }

        public async Task<List<T>?> GetAllByKey<TValue>(string key, TValue value, int tagId, string orderBy)
        {
            List<T> entities = new List<T>();
            IEnumerable<T> output = await conn.QueryAsync<T>($"select * from {typeof(T).Name} where {key} = {value} and tagId = {tagId} order by {orderBy}", new DynamicParameters());
            entities = output.ToList();
            if (!entities.Any()) return null;
            return entities;
        }

        public async Task DeleteByKey<TValue>(string key, TValue value)
        {
            await conn.ExecuteAsync($"delete from {typeof(T).Name} where {key}={value}");
        }

        public async Task<T?> FindOrByMulKey<TValue>(string[] key, TValue value)
        {
            if (key.Length == 0 || key.Length < 1)
            {
                return null;
            }

            string query = "select * from TagInfo where";

            for (int i = key.Length - 1; i >= 0; i--)
            {
                if (i == 0)
                    query += $" {key[i]}='{value}';";
                else
                    query += $" {key[i]}='{value}' or";
            }
            try
            {
                Task<T?> entity = conn.QueryFirstOrDefaultAsync<T>(query);
                await entity;
                if (entity.Result == null)
                    return null;
                return entity.Result;
            }
            catch (Exception)
            {
                return null;
            }

        }
    }
}
