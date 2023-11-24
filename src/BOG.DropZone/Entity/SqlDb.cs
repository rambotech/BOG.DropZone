using Microsoft.Data.Sqlite;
using System;

namespace BOG.DropZone.Entity
{
    public class SqlDb
    {
        public string SqlDbFilename { get; set; } = string.Empty;

        public object LockObject { get; set; } = new object();

        public DateTime LastActivity { get; set; } = DateTime.Now;

        public SqliteConnection dbConnector { get; set; }
    }
}
