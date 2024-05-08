namespace AppWinform_main.DTO
{
    internal class DTOHistoryIn
    {
        public long id { get; set; }
        public DateTime createDateTime { get; set; }
        public int tagId { get; set; }
        public string imgPath1 { get; set; }
        public string imgPath2 { get; set; }

        public DTOHistoryIn(int tagId, string imgPath1, string imgPath2)
        {
            this.tagId = tagId;
            this.imgPath1 = imgPath1;
            this.imgPath2 = imgPath2;
        }

        public DTOHistoryIn(long id, DateTime createDateTime, string imgPath1, string imgPath2)
        {
            this.id = id;
            this.createDateTime = createDateTime;
            this.imgPath1 = imgPath1;
            this.imgPath2 = imgPath2;
        }
    }
}
