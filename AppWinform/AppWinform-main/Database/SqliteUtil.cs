using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppWinform_main.Database
{
    internal class SqliteUtil
    {
        public static string TIME_FORMAT = "yyyy-MM-dd HH:mm:ss";

        public static string DATA_BASE_DIRECTORY = $@"Data Source ={AppContext.BaseDirectory}Database\Database.db; Version = 3;";
    }
}
