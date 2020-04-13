using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Watchalong.Utils
{
    public static class Uuid
    {
        private static int currentUuid = -1;
        public static int GetUuid()
        {
            currentUuid++;
            return currentUuid;
        }
    }

    public static class Helpful
    {
        public static string GetExecutingDirectory()
        {
            Uri location = new Uri(Assembly.GetEntryAssembly().GetName().CodeBase);
            return new FileInfo(location.LocalPath).Directory.FullName;
        }
    }
}
