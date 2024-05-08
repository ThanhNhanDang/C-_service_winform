using AppWinform_main.DTO;
using Emgu.CV.Rapid;
using System;
namespace AppWinform_main.Entity
{
    internal class HistoryOut
    {
        public long id { get; set; }
        public DateTime createDateTime { get; set; }
        public DateTime dateTimeIn { get; set; }
        public string dateTime { get; set; }
        public int tagId { get; set; }
        public string imgPath1 { get; set; }
        public string imgPath2 { get; set; }

        public HistoryOut()
        {
        }
        public HistoryOut(int tagId, DateTime dateTimeIn, string dateTime, string imgPath1, string imgPath2)
        {
            this.dateTimeIn = dateTimeIn;
            this.dateTime = dateTime;
            this.tagId = tagId;
            this.imgPath1 = imgPath1;
            this.imgPath2 = imgPath2;
        }   
    }
}
