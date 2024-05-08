using AppWinform_main.Database;
using AppWinform_main.DTO;
using AppWinform_main.Entity;

namespace AppWinform_main.Service
{
    internal class HistoryInService : SqliteDataAccessImpl<HistoryIn>
    {
        public HistoryInService()
        {

        }
        public async Task Save(DTOHistoryIn dto)
        {
            HistoryIn history = new HistoryIn(dto.tagId, dto.imgPath1, dto.imgPath2);
            List<DTOHistoryIn>? entities = await this.GetAllByKey(dto.tagId);
            if (entities != null)
            {
                if (entities.Count > 9)
                {
                    await base.DeleteByKey("id", $"{entities[0].id}");
                }
            }
            await base.Save(history);
        }

        private static DTOHistoryIn EntityToDto(HistoryIn entity)
        {
            DTOHistoryIn dto = new(entity.id,entity.createDateTime.ToLocalTime(), entity.imgPath1, entity.imgPath2);
            return dto;
        }

        public async Task<List<DTOHistoryIn>?> GetAllByKey(int tagId)
        {
            List<HistoryIn>? entities = await base.GetAllByKey("date(createDateTime)", $"date('{DateTime.UtcNow:yyyy-MM-dd}')", tagId,"createDateTime");
            if (entities == null)
                return null;

            List<DTOHistoryIn> dtos = new();
            foreach (HistoryIn entity in entities)
            {
                dtos.Add(EntityToDto(entity));
            }
            return dtos;
        }

        public async Task<DTOHistoryIn?> FindByKey(string key, string value)
        {
            HistoryIn? historyIn = await base.FindByKey(key, value);
            if (historyIn == null)
                return null;
            return EntityToDto(historyIn);
        }

    }
}
