namespace AppWinform_main.Entity
{
    public class ETagInfoSync
    {
        public int id { get; set; }
        public string tidNg { get; set; }
        public int isInNg { get; set; }
        public int isInXe { get; set; }

        public ETagInfoSync()
        {
            
        }
    }
    public class TagInfo
    {
        public int id { get; set; }
        public string nameNg { get; set; }
        public string nameXe { get; set; }
        public string epcNg { get; set; }
        public string epcXe { get; set; }
        public string tidNg { get; set; }
        public string tidXe { get; set; }
        public string passNg { get; set; }
        public string passXe { get; set; }
        public string typeXe { get; set; }
        public DateTime createDateTime { get; set; }
        public DateTime lastUpdate { get; set; }
        public int isInNg { get; set; }
        public int isInXe { get; set; }
        public string imgNgPath { get; set; }
        public string imgXePath { get; set; }
        public string imgBienSoPath { get; set; }
        public TagInfo() { }

        public TagInfo(string nameNg, string nameXe, string epcNg,
                        string epcXe, string tidNg, string tidXe,
                        string passNg, string passXe, string typeXe,
                        DateTime lastUpdate, int isInNg, int isInXe)
        {
            this.nameNg = nameNg;
            this.nameXe = nameXe;
            this.epcNg = epcNg;
            this.epcXe = epcXe;
            this.tidNg = tidNg;
            this.tidXe = tidXe;
            this.passNg = passNg;
            this.passXe = passXe;
            this.typeXe = typeXe;
            this.lastUpdate = lastUpdate;
            this.isInNg = isInNg;
            this.isInXe = isInXe;
        }

        public TagInfo(string nameNg, string nameXe, string epcNg,
                       string epcXe, string tidNg, string tidXe,
                       string passNg, string passXe, string typeXe, int isInNg, int isInXe)
        {
            this.nameNg = nameNg;
            this.nameXe = nameXe;
            this.epcNg = epcNg;
            this.epcXe = epcXe;
            this.tidNg = tidNg;
            this.tidXe = tidXe;
            this.passNg = passNg;
            this.passXe = passXe;
            this.typeXe = typeXe;
            this.isInNg = isInNg;
            this.isInXe = isInXe;
        }

        public TagInfo(string nameNg, string nameXe, string epcNg,
                      string epcXe, string tidNg, string tidXe,
                      string passNg, string passXe, string typeXe, int isInNg, int isInXe, string imgBienSoPath, string imgNgPath, string imgXePath)
        {
            this.nameNg = nameNg;
            this.nameXe = nameXe;
            this.epcNg = epcNg;
            this.epcXe = epcXe;
            this.tidNg = tidNg;
            this.tidXe = tidXe;
            this.passNg = passNg;
            this.passXe = passXe;
            this.typeXe = typeXe;
            this.isInNg = isInNg;
            this.isInXe = isInXe;
            this.imgBienSoPath = imgBienSoPath;
            this.imgNgPath = imgNgPath;
            this.imgXePath = imgXePath;
        }
    }
}
