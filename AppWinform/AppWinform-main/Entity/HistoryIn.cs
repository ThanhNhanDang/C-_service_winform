namespace AppWinform_main.Entity
{

    internal class HistoryIn
    {
        public long id { get; set; }
        public DateTime createDateTime { get; set; }
        public int tagId { get; set; }
        public string imgPath1 { get; set; }
        public string imgPath2 { get; set; }
        public HistoryIn() { }
        public HistoryIn(int tagId, string imgPath1,string imgPath2)
        {
            this.tagId = tagId;
            this.imgPath1 = imgPath1;
            this.imgPath2 = imgPath2;
        }
    }
}
