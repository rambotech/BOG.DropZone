using System;
using System.Collections.Generic;
using System.Text;
using BOG.DropZone.Client.Model;
using BOG.SwissArmyKnife;

namespace BOG.DropZone.Client.Helpers
{
    public static class HashMD5
    {
        public static bool Validate(Lockbox lockbox)
        {
            return lockbox.MD5 == Hasher.GetHashFromStringContent(lockbox.Content, Encoding.UTF8, Hasher.HashMethod.MD5);
        }

        public static void Calculate(Lockbox lockbox)
        {
            lockbox.MD5 = Hasher.GetHashFromStringContent(lockbox.Content, Encoding.UTF8, Hasher.HashMethod.MD5);
        }

    }
}
