using AppWinform_main.Database;
using AppWinform_main.DTO;
using AppWinform_main.Entity;

namespace AppWinform_main.Service
{
    internal class HistoryOutService : SqliteDataAccessImpl<HistoryOut>
    {
        public HistoryOutService()
        {

        }
        public async Task Save(DTOHistoryOut dto)
        {
            HistoryOut history = new HistoryOut(dto.tagId, DateTime.Parse(dto.dateTimeIn), dto.dateTime, dto.imgPath1, dto.imgPath2);
            List<DTOHistoryOut>? entities = await this.GetAllByKey(dto.tagId);
            if (entities != null)
            {
                if (entities.Count > 9)
                {
                    await base.DeleteByKey("id", $"{entities[0].id}");
                }
            }
            await base.Save(history);
        }

        private static DTOHistoryOut EntityToDto(HistoryOut entity)
        {
            DTOHistoryOut dto = new(entity.id, entity.createDateTime.ToLocalTime(), entity.dateTimeIn.ToLocalTime().ToString(SqliteUtil.TIME_FORMAT), entity.dateTime, entity.imgPath1, entity.imgPath2);
            return dto;
        }

        public async Task<List<DTOHistoryOut>?> GetAllByKey(int tagId)
        {
            List<HistoryOut>? entities = await base.GetAllByKey("date(createDateTime)", $"date('{DateTime.UtcNow:yyyy-MM-dd}')", tagId, "createDateTime");
            if (entities == null)
                return null;

            List<DTOHistoryOut> dtos = new();
            foreach (HistoryOut entity in entities)
            {
                dtos.Add(EntityToDto(entity));
            }
            return dtos;
        }

        public async Task<DTOHistoryOut?> FindByKey(string key, string value)
        {
            HistoryOut? HistoryOut = await base.FindByKey(key, value);
            if (HistoryOut == null)
                return null;
            return EntityToDto(HistoryOut);
        }

    }
}
