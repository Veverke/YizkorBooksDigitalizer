using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YizkorBooksDigitalizer.Types.SQLiteDAL
{
    public class SQLiteDALColumnDefinition
    {
        public string Name { get; private set; }

        public string Type { get; private set; }

        public bool Nullable { get; private set; }

        public string Comment { get; private set; }

        public SQLiteDALColumnDefinition(string name, string type, bool nullable = true, string comment = null)
        {
            Name = name ?? string.Empty;
            Type = type ?? string.Empty;
            Nullable = nullable;
            Comment = comment ?? string.Empty;
        }
    }
}


