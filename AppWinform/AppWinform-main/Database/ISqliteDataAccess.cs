
namespace AppWinform_main.Database
{
    internal interface ISqliteDataAccess<T>
    {
        public Task Save(T entity);
        public Task DeleteByKey<TValue>(string key, TValue value);
        public Task<List<T>?> GetAll(string orderBy);
        public Task<List<T>?> GetAllByKey<TValue>(string key, TValue value,int tagId, string orderBy);
        public Task<T?> FindByKey<TValue>(string key, TValue value);
        public Task<T?> UpdateByKey<TValue, TValueCondition>(string key, TValue value, string keyCondition, TValueCondition valueCondition);
        public Task<T?> UpdateByMulKey<TValue, TValueCondition>(string[] key, TValue[] value, string keyCondition, TValueCondition valueCondition);
    }
}
