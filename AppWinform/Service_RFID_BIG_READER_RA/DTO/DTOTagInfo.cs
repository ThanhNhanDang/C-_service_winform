using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service_RFID_BIG_READER.DTO
{
    public class DTOTagInfo
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
        public bool isInNg { get; set; }
        public bool isInXe { get; set; }
        public DTOTagInfo() { }

        public DTOTagInfo(string nameNg, string nameXe, string epcNg,
                        string epcXe, string tidNg, string tidXe,
                        string passNg, string passXe, string typeXe,
                        DateTime lastUpdate, bool isInNg, bool isInXe)
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

        public DTOTagInfo(string nameNg, string nameXe, string epcNg,
                        string epcXe, string tidNg, string tidXe,
                        string passNg, string passXe, string typeXe, bool isInNg, bool isInXe)
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
    }
}
