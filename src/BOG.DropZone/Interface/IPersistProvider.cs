using System;

namespace BOG.DropZone.Interface
{
    /// <summary>
    /// Methods for writing to the various storage location choices.
    /// </summary>
    public interface IPersistProvider
    {
        string RootFolder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    }
}
