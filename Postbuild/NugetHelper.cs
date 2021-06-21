using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet;
using System.IO;
using System.Xml.Schema;
using System.Data.OleDb;
using System.Runtime.InteropServices.WindowsRuntime;

namespace Postbuild
{
    public static class NugetHelper
    {
        public static string CreatePackage(PackageInfo packageInfo,string savePath)
        {
            Manifest nuspec = new Manifest(); //creating a nuspec

            ManifestMetadata metadata = new ManifestMetadata()
            {
                Authors = packageInfo.Author,
                Version = packageInfo.Version,
                Id = packageInfo.Id,
                Description = packageInfo.Description,
            };

            nuspec.Metadata = metadata;
            List<ManifestFile> mfs = GetManifestFilesForDirectory(packageInfo.FilesFolder);
            nuspec.Files = mfs;

            PackageBuilder builder = new PackageBuilder()
            {
                Id = packageInfo.Id,
                Description = packageInfo.Description,
                Version = new SemanticVersion(packageInfo.Version)
            };

            builder.Populate(nuspec.Metadata);

            foreach (ManifestFile value in nuspec.Files)
            {
                builder.Files.Add(new PhysicalPackageFile()
                {
                    SourcePath = value.Source,
                    TargetPath = value.Target
                });
            }
            string pkgName = packageInfo.Id + "." + packageInfo.Version + ".nupkg";
            string pgkPath = Path.Combine(savePath, pkgName);
            if (File.Exists(pgkPath))
                File.Delete(pgkPath);
            
            using (FileStream stream = File.Open(pgkPath, FileMode.Create))
            {
                builder.Save(stream);
            }
            return pkgName;
        }

        private static List<ManifestFile> GetManifestFilesForDirectory(string baseFolder,string curFolder = null)
        {
            List<ManifestFile> mfs = new List<ManifestFile>();
            string folder = curFolder ?? baseFolder;
            var dirs = Directory.GetFiles(folder)?.ToList();

            if (dirs == null || dirs.Count == 0)
                return mfs;

            foreach(var file in dirs)
            {
                FileAttributes attr = File.GetAttributes(file);
                if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                    mfs.AddRange(GetManifestFilesForDirectory(baseFolder,file));
                else if (!file.EndsWith(".pdb"))
                {   
                    ManifestFile mf = new ManifestFile();
                    string relFile = file.Replace(baseFolder,string.Empty);
                    mf.Source = Path.Combine(@"lib\net45", relFile);
                    mf.Target = Path.Combine(@"lib\net45", relFile);
                    mfs.Add(mf);
                }
            }

            return mfs;
        }

        public class PackageInfo
        {
            public string Id { get; set; }
            public string Version { get; set; }
            public string Author { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public string FilesFolder { get; set; }

        }
    }
}
