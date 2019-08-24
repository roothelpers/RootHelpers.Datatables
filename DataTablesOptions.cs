using System.Collections.Generic;

namespace RootHelpers.Datatables
{
    public class DataTablesOptions
    {
        public Dictionary<string, string> SearchAliases { get; set; }

        public DataTablesOptions()
        {
            SearchAliases = new Dictionary<string, string>();
        }
    }
}