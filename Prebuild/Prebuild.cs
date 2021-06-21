using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Prebuild
{
    class Prebuild
    {
        static void Main(string[] args)
        {
            //test args
            //args = new string[] { @"SquirrelWinnform|C:\Code Projects\SquirrelWinnform\SquirrelWinnform\|C:\Code Projects\SquirrelWinnform\|C:\Code Projects\SquirrelWinnform\SquirrelWinnform\bin\debug\|debug" };
            BuildData.Init(args);

            if (!BuildConfig.ShouldExecuteBuildEvent())
                return;

            if (BuildConfig.IsCurrentBuildNewVersion())
            {
                WriteVersionInAssemblyInfo(BuildData.SettingsVersion, BuildData.ProjectDir);
                DeleteBinObj(BuildData.ProjectDir);
            }
        }

        private static void DeleteBinObj(string projectDir)
        {
            var dirs = new List<string>() { Path.Combine(projectDir, "bin"), Path.Combine(projectDir, "obj") };
            try
            {
                foreach (var dir in dirs)
                {
                    if (Directory.Exists(dir))
                        DirectoryUtils.DeleteDirectoryRecursive(dir);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static void WriteVersionInAssemblyInfo(string newVersion, string projectPath)
        {
            var directories = GetPropertiesFolders(projectPath);
            foreach (var dir in directories)
            {
                //Write assmeblies only for project being built
                if (dir.Contains(projectPath))
                    WriteNewVersionToAssemblyFile(Path.Combine(dir, "AssemblyInfo.cs"), newVersion);
            }
        }

        private static void WriteNewVersionToAssemblyFile(string assemblyFile, string newVersion)
        {
            var tempFile = assemblyFile + ".temp";
            FileUtils.DeleteIfExist(tempFile);
            List<string> lines = new List<string>();

            //Read asembly lines
            using (StreamReader sr = new StreamReader(assemblyFile))
            {
                while (sr.Peek() >= 0)
                {
                    lines.Add(sr.ReadLine());
                }
            }

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (!line.StartsWith("/") && (line.Contains("AssemblyVersion(") || line.Contains("AssemblyFileVersion(")))
                {
                    int start = line.IndexOf("\"");
                    int end = line.LastIndexOf("\"");
                    line = line.Remove(start + 1, end - start - 1);
                    line = line.Insert(start + 1, newVersion);
                }
                lines[i] = line;
            }

            File.WriteAllLines(tempFile, lines);
            File.Delete(assemblyFile);
            File.Copy(tempFile, assemblyFile,true);
            File.Delete(tempFile);
        }

        static List<string> GetPropertiesFolders(string projectPath)
        {
            List<string> folders = new List<string>();
            DirectoryInfo info = new DirectoryInfo(projectPath);
            DirectoryInfo parentDir = info.Parent;
            var directories = Directory.GetDirectories(parentDir.FullName);

            foreach (var dir in directories)
            {
                List<string> childDirs = Directory.GetDirectories(dir)?.ToList();
                string propertyDir = GetProperyDirectory(childDirs);
                if (!string.IsNullOrEmpty(propertyDir))
                    folders.Add(propertyDir);
            }
            return folders;
        }

        private static string GetProperyDirectory(List<string> childDirs)
        {
            string propertyDir = "";
            foreach (var child in childDirs)
            {
                if (child.EndsWith("Properties"))
                {
                    propertyDir = child;
                    break;
                }
            }
            return propertyDir;
        }
    }
}
