﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Shell32;
using NLog;
using System.Security.AccessControl;
using System.Security.Principal;

namespace TripwireService
{
    public class Util
    {
        public static string GetSpecificFileProperties(string file, params int[] indexes)
        {
            string fileName = Path.GetFileName(file);
            string folderName = Path.GetDirectoryName(file);
            Shell32.Shell shell = new Shell32.Shell();
            Shell32.Folder objFolder;
            objFolder = shell.NameSpace(folderName);
            StringBuilder sb = new StringBuilder();

            foreach (Shell32.FolderItem2 item in objFolder.Items())
            {
                if (fileName == item.Name)
                {
                    for (int i = 0; i < indexes.Length; i++)
                    {
                        sb.Append(objFolder.GetDetailsOf(item, indexes[i]) + ",");
                    }

                    break;
                }
            }

            string result = sb.ToString().Trim();
            //Protection for no results causing an exception on the `SubString` method
            if (result.Length == 0)
            {
                return string.Empty;
            }
            return result.Substring(0, result.Length - 1);
        }
    }
}
