using BOG.DropZone.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BOG.DropZone.Interface
{
    public interface IStorage
    {
        Dictionary<string, Dropzone> DropzoneList { get; set; }
        void Reset();
        void Shutdown();
    }
}
