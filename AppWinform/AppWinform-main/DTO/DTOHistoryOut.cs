
namespace AppWinform_main.DTO
{
    internal class DTOHistoryOut
    {
        public long id { get; set; }
        public DateTime createDateTime { get; set; }
        public string dateTimeIn { get; set; }
        public string dateTime { get; set; }
        public int tagId { get; set; }
        public string imgPath1 { get; set; }
        public string imgPath2 { get; set; }

        public DTOHistoryOut(long id, DateTime createDateTime, string dateTimeIn, string dateTime, string imgPath1, string imgPath2)
        {
            this.id = id;
            this.createDateTime = createDateTime;
            this.dateTimeIn = dateTimeIn;
            this.dateTime = dateTime;
            this.imgPath1 = imgPath1;
            this.imgPath2 = imgPath2;
        }

        public DTOHistoryOut(int tagId, string dateTimeIn, string dateTime, string imgPath1, string imgPath2)
        {
            this.tagId = tagId;
            this.dateTimeIn = dateTimeIn;
            this.dateTime = dateTime;
            this.imgPath1 = imgPath1;
            this.imgPath2 = imgPath2;
        }
    }
}
