namespace AppWinform_main.Reader
{
    internal class TagAlert
    {
        public DateTime dateTime { get; set; }
        public string tidXe { get; set; }
        public bool isEncPassNg { get; set; }
        public bool isEncPassXe { get; set; }
        public bool isIn { get; set; }
        public bool isNg { get; set; }
        public bool isXe { get; set; }

        public TagAlert(DateTime dateTime, string tidXe, bool isEncPassNg,bool isEncPassXe, bool isIn, bool isNg, bool isXe)
        {
            this.dateTime = dateTime;
            this.tidXe = tidXe;
            this.isEncPassNg = isEncPassNg;
            this.isEncPassXe = isEncPassXe;
            this.isIn = isIn;
            this.isNg = isNg;
            this.isXe = isXe;
        }
    }
}
