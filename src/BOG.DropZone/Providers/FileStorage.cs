using BOG.DropZone.Common.Dto;
using BOG.DropZone.Interface;
using BOG.DropZone.Storage;
using System;
using System.Collections.Generic;

namespace BOG.DropZone.Providers
{
    public class FileStorage : IStorage
    {
        public string AccessToken { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string AdminToken { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int MaxDropzones { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int MaximumFailedAttemptsBeforeLockout { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int LockoutSeconds { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
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

        public void EnqueuePayload(string zoneName, string recipient, string tracking, DateTime expiresOn)
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

        public StoredValue PullFromQueue(string zoneName, string recipient, out string payload )
        {
            throw new NotImplementedException();
        }

        public void PushToQueue(string zoneName, string recipient, string tracking, DateTime expiresOn, string payload)
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
    }
}
