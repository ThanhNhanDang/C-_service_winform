﻿using Service_RFID_BIG_READER.DTO;
using Service_RFID_BIG_READER.Entity;
using Dapper;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Linq;
using System.Threading.Tasks;
using static Dapper.SqlMapper;

namespace Service_RFID_BIG_READER.Database
{
    internal class SqliteDataAccess
    {
        public static string _timeFormat = "yyyy-MM-dd HH:mm:ss";
        private async void createTricgger()
        {
            using (IDbConnection conn = new SQLiteConnection(LoadConnectionString()))
            {
                conn.Open();
                string query = "CREATE TRIGGER ro_columns" +
                                "BEFORE UPDATE OF nameNg, nameXe, epcNg," +
                                "epcXe, tidNg, tidXe, passNg, passXe," +
                                "typeXe, createDateTime ON TagInfo" +
                                "BEGIN" +
                                    "SELECT raise(abort, 'Read Only!');" +
                                "END";
                await conn.ExecuteAsync(query);
            };
        }

        private async void DropTricgger()
        {
            using (IDbConnection conn = new SQLiteConnection(LoadConnectionString()))
            {
                conn.Open();
                string query = "DROP TRIGGER ro_columns";
                await conn.ExecuteAsync(query);
            }
        }

        public static List<DTOTagInfo> LoadTag()
        {
            using (IDbConnection conn = new SQLiteConnection(LoadConnectionString()))
            {
                conn.Open();
                List<ETagInfo> entities;
                IEnumerable<ETagInfo> output = null;
                try
                {
                    output = conn.Query<ETagInfo>("select * from TagInfo", new DynamicParameters());
                    entities = output.ToList();
                }
                catch (Exception ex)
                {
                    Service1.WriteLog(ex.Message);
                    return null;
                }
                if (entities.Count == 0) return null;
                //  conn.ExecuteAsync("ALTER TABLE TagInfo ADD COLUMN password TEXT NOT NULL");
                List<DTOTagInfo> dtos = new List<DTOTagInfo>();
                foreach (ETagInfo entity in entities)
                {
                    dtos.Add(new DTOTagInfo(
                        entity.nameNg, entity.nameXe, entity.epcNg,
                        entity.epcXe, entity.tidNg, entity.tidXe,
                        entity.passNg, entity.passXe, entity.typeXe,
                        entity.lastUpdate, entity.lastUpdateXe,
                        entity.isInNg == 1, entity.isInXe == 1
                       ));
                }
                return dtos;
            }
        }
        public static async Task<DTOTagInfo> FindByKey(string key, string value)
        {
            using (IDbConnection conn = new SQLiteConnection(LoadConnectionString()))
            {
                conn.Open();
                DTOTagInfo dto;
                Task<ETagInfo> entity = null;
                try
                {
                    entity = conn.QueryFirstOrDefaultAsync<ETagInfo>($"select * from TagInfo where {key}='{value}'");
                    await entity;
                }
                catch (Exception ex)
                {
                    Service1.WriteLog(ex.Message);
                    return null;
                }
                if (entity.Result == null)
                {
                    return null;
                }
                ETagInfo e = entity.Result;
                dto = new DTOTagInfo(
                    e.nameNg, e.nameXe, e.epcNg,
                    e.epcXe, e.tidNg, e.tidXe,
                    e.passNg, e.passXe, e.typeXe,
                     e.lastUpdate.ToLocalTime(), e.lastUpdateXe.ToLocalTime(),
                     e.isInNg == 1, e.isInXe == 1
                    );
                return dto;
            }
        }

        public static async Task<DTOTagInfo> FindOrByMulKey(string[] key, string value)
        {
            using (IDbConnection conn = new SQLiteConnection(LoadConnectionString()))
            {
                conn.Open();
                //if (value == null) return null;
                if (key.Length == 0 || key.Length < 1)
                {
                    return null;
                }
                DTOTagInfo dto;


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
                    Task<ETagInfo> entity = conn.QueryFirstOrDefaultAsync<ETagInfo>(query);
                    await entity;
                    if (entity.Result == null)
                    {
                        return null;
                    }
                    ETagInfo e = entity.Result;
                    dto = new DTOTagInfo(
                        e.nameNg, e.nameXe, e.epcNg,
                        e.epcXe, e.tidNg, e.tidXe,
                        e.passNg, e.passXe, e.typeXe,
                         e.lastUpdate.ToLocalTime(), e.lastUpdateXe.ToLocalTime()
                         , e.isInNg == 1, e.isInXe == 1
                        );
                    return dto;
                }
                catch (Exception ex)
                {
                    Service1.WriteLog(ex.Message);
                    return null;
                }
            }
        }

        public static async Task<bool> UpdateByKey(string table, string[] key, string[] value, string keyCondition, string valueCondition)
        {
            using (IDbConnection conn = new SQLiteConnection(LoadConnectionString()))
            {
                conn.Open();
                if (value.Length == 0 || key.Length == 0) return false;
                if (value.Length != key.Length) return false;

                int length = key.Length;
                string query = $"UPDATE {table} SET ";

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
                catch (Exception ex)
                {
                    Service1.WriteLog(ex.Message);
                    return false;
                }

                return true;
            }
        }

        public static async Task<bool> UpdateByKey(string table, string key, string value, string keyCondition, string valueCondition)
        {
            using (IDbConnection conn = new SQLiteConnection(LoadConnectionString()))
            {
                conn.Open();
                string query = $"UPDATE {table} SET ";
                query += $"{key} = '{value}'";
                query += $"WHERE {keyCondition} = '{valueCondition}'";
                try
                {
                    await conn.ExecuteAsync(query);
                }
                catch (Exception ex)
                {
                    Service1.WriteLog(ex.Message);
                    return false;
                }
                return true;
            }
        }

        public static void SaveTag(DTOTagInfo dto)
        {
            using (IDbConnection conn = new SQLiteConnection(LoadConnectionString()))
            {
                conn.Open();
                ETagInfo entity = new ETagInfo(
            dto.nameNg, dto.nameXe, dto.epcNg,
            dto.epcXe, dto.tidNg, dto.tidXe,
            dto.passNg, dto.passXe, dto.typeXe, dto.isInNg ? 1 : 0, dto.isInXe ? 1 : 0
            );
                conn.Execute("insert into TagInfo (" +
                    "nameNg, nameXe, epcNg," +
                    "epcXe, tidNg, tidXe," +
                    "passNg, passXe, typeXe, isIn) " +
                    "values (" +
                    "@nameNg, @nameXe, @epcNg," +
                    "@epcXe, @tidNg, @tidXe," +
                    "@passNg, @passXe, @typeXe, @isIn)", entity);
            }
        }

        private static string LoadConnectionString(string id = "Default")
        {
            string t = $@"Data Source ={AppContext.BaseDirectory}Database\Database.db; Version = 3;";
            return t;
        }
    }
}
