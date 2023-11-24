using BOG.DropZone.Common.Dto;
using BOG.DropZone.Entity;
using BOG.DropZone.Interface;
using BOG.DropZone.Storage;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;

namespace BOG.DropZone.Providers
{
    /// <summary>
    /// Storage for queue items and reference items using SqliteDB
    /// </summary>
    public class SqlLiteStorage : IStorage
    {
        const string ZoneNamePattern = @"^[A-Za-z][A-Za-z0-9_\-\.]{0,58}[A-Za-z0-9\.]$";
        const string KeyNamePattern = @"^[A-Za-z][A-Za-z0-9_\-\.]{0,58}[A-Za-z0-9\.]$";

        private readonly Dictionary<string, SqlDb> dbCatalog = new Dictionary<string, SqlDb>();
        private string dbRootPath = string.Empty;

        /// <summary>
        /// Instantiate
        /// </summary>
        /// <param name="dbPath">the root path for storage, or null/empty to use default folder in profile.</param>
        public SqlLiteStorage(string dbPath)
        {
            if (string.IsNullOrWhiteSpace(dbPath))
            {
                dbRootPath = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.UserProfile,
                        Environment.SpecialFolderOption.DoNotVerify
                    ),
                    "dropzone",
                    "sqliteDB"
                 );
            }
            else
            {
                dbRootPath = Path.Combine(dbPath, "dropzone", "sqliteDB"); ;
            }
            dbRootPath = dbPath;
            dbCatalog.Clear();
            if (!Directory.Exists(dbPath))
            {
                Directory.CreateDirectory(dbPath);
            }
            else
            {
                foreach (var sqlDbFile in Directory.GetFiles(dbPath, "*.db", SearchOption.TopDirectoryOnly))
                {
                    dbCatalog.Add(Path.GetFileNameWithoutExtension(sqlDbFile),
                        new SqlDb
                        {
                            SqlDbFilename = sqlDbFile,
                            LastActivity = DateTime.Now,
                            LockObject = new object(),
                            dbConnector = new SqliteConnection()
                        });
                }
            }
        }

        public string AccessToken { get; set; }
        public string AdminToken { get; set; }
        public int MaxDropzones { get; set; }
        public int MaximumFailedAttemptsBeforeLockout { get; set; }
        public int LockoutSeconds { get; set; }
        public Dictionary<string, DropPoint> DropZoneList { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public List<ClientWatch> ClientWatchList { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public void Clear(string dropZoneName)
        {
            throw new NotImplementedException();
        }

        public void DeleteBlob(string zoneName, string key)
        {
            throw new NotImplementedException();
        }

        public List<string> GetBlobKeys(string zoneName)
        {
            throw new NotImplementedException();
        }

        public bool IsValidKeyName(string key)
        {
            throw new NotImplementedException();
        }

        public bool IsValidZoneName(string zoneName)
        {
            throw new NotImplementedException();
        }

        public string ReadBlob(string zoneName, string key, string value)
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public void SaveBlob(string zoneName, string key, string value)
        {
            throw new NotImplementedException();
        }

        public void Shutdown()
        {
            throw new NotImplementedException();
        }

        #region Helpers

        #endregion


    }
}
