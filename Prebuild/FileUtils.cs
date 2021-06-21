using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Prebuild
{
    public static class FileUtils
    {
        public static bool DeleteIfExist(string fileName)
        {
            bool deleted = false;
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
                deleted = true;
            }
            return deleted;
        }


        public static int IndexOfInt(string text)
        {
            int ind = -1;
            if (string.IsNullOrEmpty(text))
                return ind;

            for(int i = 0; i < text.Length; i++)
            {
                string s = text[i].ToString();

                int? nb = s.ToNullableInt();
                if (nb.HasValue)
                {
                    ind = i;
                    break;
                }
            }
            return ind;
        }

        public static int? ToNullableInt(this string s)
        {
            int i;
            if (int.TryParse(s, out i)) return i;
            return null;
        }
    }
}
