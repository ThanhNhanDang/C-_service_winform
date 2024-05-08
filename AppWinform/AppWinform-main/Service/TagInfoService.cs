using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using AppWinform_main.DTO;
using AppWinform_main.Entity;
using AppWinform_main.Database;

namespace AppWinform_main.Service
{
    internal class TagInfoService : SqliteDataAccessImpl<TagInfo>
    {
        private static DTOTagInfo EntityToDto(TagInfo entity)
        {
            DTOTagInfo dto = new(
                    entity.id,
                    entity.nameNg, entity.nameXe, entity.epcNg,
                    entity.epcXe, entity.tidNg, entity.tidXe,
                    entity.passNg, entity.passXe, entity.typeXe,
                    entity.lastUpdate.ToLocalTime(), entity.isInNg == 1, entity.isInXe == 1, entity.imgBienSoPath, entity.imgNgPath, entity.imgXePath
                   );
            return dto;
        }
        public async Task<List<ETagInfoSync>?> SyncDatbase()
        {
            using (IDbConnection conn = new SQLiteConnection(SqliteUtil.DATA_BASE_DIRECTORY))
            {
                List<ETagInfoSync> entities;

                Task<IEnumerable<ETagInfoSync>> output;
                try
                {
                    output = conn.QueryAsync<ETagInfoSync>("select tidNg, isInNg, isInXe from TagInfo", new DynamicParameters());
                    await output;
                }
                catch (SQLiteException)
                {
                    return null;
                }
                entities = output.Result.ToList();
                if (!entities.Any()) return null;
                //  conn.ExecuteAsync("ALTER TABLE TagInfo ADD COLUMN password TEXT NOT NULL");
                return entities;
            }
        }

        public async Task<DTOTagInfo?> FindByKey(string key, string value)
        {
            TagInfo? entity = await base.FindByKey(key, value);
            if (entity == null) return null;
            DTOTagInfo dto = EntityToDto(entity);
            return dto;
        }

        public async Task<List<DTOTagInfo>?> GetAll()
        {
            List<TagInfo>? entities = await base.GetAll("id");
            if (entities == null)
                return null;
            List<DTOTagInfo> dtos = new();
            foreach (TagInfo entity in entities)
            {
                dtos.Add(EntityToDto(entity));
            }
            return dtos;
        }

        public async Task<DTOTagInfo?> UpdateByMulKey(string[] key, string[] value, string keyCondition, string valueCondition)
        {
            TagInfo? entity = await base.UpdateByMulKey(key, value, keyCondition, valueCondition);
            if (entity == null)
                return null;
            DTOTagInfo dto = EntityToDto(entity);
            return dto;
        }

        public async Task<DTOTagInfo?> UpdateByKey(string key, string value, string keyCondition, string valueCondition)
        {
            TagInfo? entity = await base.UpdateByKey(key, value, keyCondition, valueCondition);
            if (entity == null) return null;
            DTOTagInfo dto = EntityToDto(entity);
            return dto;
        }

        public async Task Save(DTOTagInfo dto)
        {
            TagInfo entity = new TagInfo(
                dto.nameNg, dto.nameXe, dto.epcNg,
                dto.epcXe, dto.tidNg, dto.tidXe,
                dto.passNg, dto.passXe, dto.typeXe,
                dto.isInNg ? 1 : 0, dto.isInXe ? 1 : 0);
            await base.Save(entity);
        }

        public async Task<DTOTagInfo?> FindOrByMulKey(string[] key, string value)
        {
            TagInfo? entity = await base.FindOrByMulKey(key, value);
            if (entity == null)
                return null;
            DTOTagInfo dto = EntityToDto(entity);
            return dto;

        }
    }
}
