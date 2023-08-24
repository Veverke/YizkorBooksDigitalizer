using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;

namespace YizkorBooksDigitalizer.Types.SQLiteDAL
{
    public class DAL
    {
        private SQLiteConnection _connection;

        public string DbFile { get; private set; }

        public string Error { get; private set; }

        private SQLiteConnection DbConnection
        {
            get
            {
                if (_connection == null)
                {
                    _connection = new SQLiteConnection($"Data Source={DbFile}; Version=3;");
                    _connection.Open();
                }
                return _connection;
            }
        }

        public DAL(string dbFileName)
        {
            DbFile = dbFileName ?? string.Empty;
        }

        public DataTable Query(string sql)
        {
            SQLiteCommand sQLiteCommand = new SQLiteCommand(DbConnection);
            sQLiteCommand.CommandText = sql;
            SQLiteDataReader sQLiteDataReader = sQLiteCommand.ExecuteReader();
            DataTable dataTable = new DataTable("Result");
            if (sQLiteDataReader == null)
            {
                return dataTable;
            }
            for (int i = 0; i < sQLiteDataReader.FieldCount; i++)
            {
                dataTable.Columns.Add(sQLiteDataReader.GetName(i), sQLiteDataReader.GetFieldType(i));
            }
            while (sQLiteDataReader.Read())
            {
                List<object> list = new List<object>();
                for (int j = 0; j < sQLiteDataReader.FieldCount; j++)
                {
                    list.Add(sQLiteDataReader[j]);
                }
                dataTable.Rows.Add(list.ToArray());
            }
            return dataTable;
        }

        public int ExecuteNonQuery(string sql)
        {
            SQLiteCommand sQLiteCommand = new SQLiteCommand(DbConnection);
            sQLiteCommand.CommandText = sql;
            return sQLiteCommand.ExecuteNonQuery();
        }

        public T ExecuteScalar<T>(string sql)
        {
            SQLiteCommand sQLiteCommand = new SQLiteCommand(DbConnection);
            sQLiteCommand.CommandText = sql;
            object value = sQLiteCommand.ExecuteScalar();
            return (T)Convert.ChangeType(value, typeof(T));
        }

        public bool CreateTable(string tableName, IEnumerable<SQLiteDALColumnDefinition> columnDefinitions, bool dropIfExistent)
        {
            SQLiteCommand sQLiteCommand = new SQLiteCommand(DbConnection);
            sQLiteCommand.CommandText = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{tableName}';";
            if (sQLiteCommand.ExecuteScalar() == null)
            {
                sQLiteCommand.CommandText = string.Format("\r\n                CREATE TABLE {0}\r\n                (\r\n                    {1}\r\n                );", tableName, string.Join(",", columnDefinitions.Select((SQLiteDALColumnDefinition c) => string.Format("`{0}` {1} {2}", c.Name, c.Type, c.Nullable ? "NULL" : "NOT NULL"))));
                try
                {
                    int num = sQLiteCommand.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    Error = string.Format("DAL.{0} failed. Error: {1}", "CreateTable", ex.Message);
                }
            }
            else
            {
                if (dropIfExistent)
                {
                    sQLiteCommand.CommandText = $"DROP TABLE {tableName};";
                    sQLiteCommand.ExecuteNonQuery();
                }
                sQLiteCommand.CommandText = string.Format("\r\n                CREATE TABLE {0}\r\n                (\r\n                    {1}\r\n                );", tableName, string.Join(",", columnDefinitions.Select((SQLiteDALColumnDefinition c) => string.Format("`{0}` {1} {2}", c.Name, c.Type, c.Nullable ? "NULL" : "NOT NULL"))));
                try
                {
                    int num2 = sQLiteCommand.ExecuteNonQuery();
                }
                catch (Exception ex2)
                {
                    Error = string.Format("DAL.{0} failed. Error: {1}", "CreateTable", ex2.Message);
                }
            }
            return string.IsNullOrEmpty(Error);
        }

        public bool Insert(string sql)
        {
            SQLiteCommand sQLiteCommand = new SQLiteCommand(DbConnection);
            sQLiteCommand.CommandText = sql;
            try
            {
                int num = sQLiteCommand.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Error = string.Format("DAL.{0} failed. Error: {1}", "Insert", ex.Message);
            }
            return string.IsNullOrEmpty(Error);
        }
    }
}