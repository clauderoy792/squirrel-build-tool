using Prebuild;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Security;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using EnvDTE;
using Semver;
using NuGet;

namespace Postbuild
{
    class Postbuild
    {
        static async Task Main(string[] args)
        {
            //test args
            //args = new string[] { @"Organic-Wizard|C:\Git\Organic-Wizard\src\Organic-Wizard| C:\Git\Organic-Wizard\src\| C:\Git\Organic-Wizard\src\Organic-Wizard\| Release"};
            BuildData.Init(args);
            if (!BuildConfig.ShouldExecuteBuildEvent())
                return;


            CopyReleaseFolderToPostBuildFolder(BuildData.TargetDir, BuildData.SolutionDir);
            var version = BuildConfig.GetAppVersionString();
            NugetHelper.PackageInfo info = new NugetHelper.PackageInfo()
            {
                Id = BuildData.ProjectName,
                Version = version,
                Author = "Claude",
                Description = BuildData.ProjectName,
                Title = BuildData.ProjectName,
                FilesFolder = BuildData.TargetDir
            };
            string packageName = NugetHelper.CreatePackage(info, BuildData.SolutionDir);
            string squirrelReleaseFolder = Path.Combine(BuildData.SolutionDir, "Releases");
            string squirrelPackageName = GetSquirrelPackageName(packageName);
            DeleteSetupFiles(squirrelReleaseFolder, squirrelPackageName);
            RemoveOlderVerisons(squirrelReleaseFolder);
            CopyReleaseDirToTempAndCopyBack(squirrelReleaseFolder);
            bool succes = await Releasify(squirrelReleaseFolder, packageName, squirrelPackageName, 5000);
            if (succes)
            {
                File.Delete(Path.Combine(BuildData.SolutionDir, packageName));
                CopyReleaseFilesToGitFolder(squirrelReleaseFolder);
                GitHelper.AddCommitPush();
                await GitHelper.CreateRelease();
                ChangeTxtFileVersion();
                BuildConfig.SetTxtFileVersion(BuildData.SettingsVersion);
                File.Copy(BuildConfig.GetTxtVersionFilePath(),
                    Path.Combine(BuildConfig.GetGitRepoLocalPath(), BuildConfig.GetVerionTxtFileName()), true);
                GitHelper.AddCommitPush();
                Console.WriteLine($"Version {version} released successfully!");
            }
            else
                Console.WriteLine($"Failed to release package {packageName}");
        }

        private static void CopyReleaseDirToTempAndCopyBack(string squirrelReleaseFolder)
        {
            string tempDir = squirrelReleaseFolder + "-temp";
            if (Directory.Exists(tempDir))
                DirectoryUtils.DeleteDirectoryRecursive(tempDir);
            Directory.CreateDirectory(tempDir);
            DirectoryUtils.CopyDirectory(squirrelReleaseFolder, tempDir, true);
            DirectoryUtils.DeleteDirectoryRecursive(squirrelReleaseFolder);
            DirectoryUtils.CopyDirectory(tempDir, squirrelReleaseFolder, true);
            DirectoryUtils.DeleteDirectoryRecursive(tempDir);
        }

        private static void ChangeTxtFileVersion()
        {
            string versionFile = Path.Combine(BuildConfig.GetGitRepoLocalPath(), "version.txt");
            FileUtils.DeleteIfExist(versionFile);
            File.WriteAllText(versionFile, BuildConfig.GetAppVersionString());
        }

        private static void CopyReleaseFilesToGitFolder(string squirrelReleaseFolder)
        {
            var files = Directory.GetFiles(squirrelReleaseFolder);
            foreach (var squirrelFile in files)
            {
                FileInfo info = new FileInfo(squirrelFile);
                string localGitFile = Path.Combine(BuildConfig.GetGitRepoLocalPath(), info.Name);
                if (File.Exists(localGitFile))
                    File.Delete(localGitFile);
                File.Copy(squirrelFile, localGitFile, true);
            }
        }

        private static void DeleteSetupFiles(string squirrelReleaseFolder, string squirrelPackageName)
        {
            List<string> filesToDel = new List<string>()
            {
                Path.Combine(squirrelReleaseFolder,"RELEASES"),
                Path.Combine(squirrelReleaseFolder,"setup.exe"),
                Path.Combine(squirrelReleaseFolder,"setup.msi"),
                squirrelPackageName
            };
            foreach (var file in filesToDel)
            {
                FileUtils.DeleteIfExist(file);
            }
        }

        private static async Task<bool> Releasify(string squirrelReleaseFolder, string packageName, string squirrelPackageName, int timeout)
        {
            ExecutePMCommand($"Squirrel --releasify {packageName} ");
            int tick = 10;
            int curWaitTime = 0;
            bool success = false;
            List<string> filesToCreate = new List<string>()
            {
                Path.Combine(squirrelReleaseFolder,"RELEASES"),
                Path.Combine(squirrelReleaseFolder,"setup.exe"),
                Path.Combine(squirrelReleaseFolder,"setup.msi"),
                Path.Combine(squirrelReleaseFolder,squirrelPackageName)
            };

            while (!success && curWaitTime < timeout)
            {
                await Task.Delay(tick);
                curWaitTime += tick;
                success = true;
                foreach (var file in filesToCreate)
                {
                    if (!File.Exists(file))
                    {
                        success = false;
                        break;
                    }
                }
            }

            //Wait while  squirrell is done, takes more time than juste creating the files
            await Task.Delay(4000);

            return success;
        }

        private static void RemoveOlderVerisons(string squirrelRealseDir)
        {
            var files = Directory.GetFiles(squirrelRealseDir);
            SemVersion curIntVer = new SemVersion(new Version(BuildConfig.GetAppVersionString()));
            List<string> nugetPackages = new List<string>();
            //List all previous versions
            foreach (var file in files)
            {
                if (!file.EndsWith(".nupkg"))
                    continue;
                SemVersion intVer = GetPackageVersion(file);
                if (curIntVer != intVer)
                    nugetPackages.Add(file);
            }

            //file the previous version
            SemVersion previousVer = new SemVersion(0);
            foreach (var fileName in nugetPackages)
            {
                SemVersion ver = GetPackageVersion(fileName);
                if (ver < curIntVer && ver > previousVer)
                    previousVer = ver;
            }

            //delete  every other version and invalid named verison
            foreach (var file in nugetPackages)
            {
                SemVersion ver = GetPackageVersion(file);
                bool containsSquirrelNaming = file.Contains("-delta") || file.Contains("-full");
                if (ver == previousVer)
                {
                    if (!containsSquirrelNaming || file.Contains("-delta"))
                        File.Delete(file);
                }
                if (ver != curIntVer && ver != previousVer)
                    File.Delete(file);
                if (ver == curIntVer && !containsSquirrelNaming)
                    File.Delete(file);

            }
        }

        private static SemVersion GetPackageVersion(string fullFilename)
        {
            if (Directory.Exists(fullFilename))
                return new SemVersion(0);

            int firstInd = 0;
            int lastInd = 0;
            string fileName = new FileInfo(fullFilename).Name;

            for (int i = 0; i < fileName.Length; i++)
            {
                string s = fileName[i].ToString();
                if (s == ".")
                    continue;

                int? nb = s.ToNullableInt();
                if (nb.HasValue)
                {
                    if (firstInd == 0)
                        firstInd = i;
                    else
                        lastInd = i;
                }
                else if (firstInd > 0 && lastInd > firstInd)
                    break;
            }

            string ver = fileName.Substring(firstInd, lastInd - firstInd + 1);
            return new SemVersion(new Version(ver));
        }

        static void CopyReleaseFolderToPostBuildFolder(string targetDir, string solutionDir)
        {
            string postBuildDir = GetPosBuildDir();
            if (Directory.Exists(postBuildDir))
            {
                string libDir = Path.Combine(postBuildDir, "lib");
                if (Directory.Exists(libDir))
                    DirectoryUtils.DeleteDirectoryRecursive(libDir);

                string net45 = Path.Combine(libDir, "net45");
                DirectoryUtils.DeleteIfExists(net45);
                Directory.CreateDirectory(net45);
                DirectoryUtils.CopyDirectory(targetDir, net45, true);
            }
        }

        public static string GetPosBuildDir()
        {
            BuildfolderInfo releaseDir = new BuildfolderInfo("release");
            BuildfolderInfo debugeDir = new BuildfolderInfo("debug");

            if (releaseDir.Exists && releaseDir.ContainsExe)
                return releaseDir.BuildDestination;
            else if (debugeDir.Exists && debugeDir.ContainsExe)
                return debugeDir.BuildDestination;

            throw new Exception("Failed to find postbuild.exe");
        }

        internal class BuildfolderInfo
        {
            internal string ConfigName { get; set; }
            internal string BuildDestination { get; set; }
            internal bool Exists { get; set; }
            internal bool ContainsExe { get; set; }
            internal BuildfolderInfo(string configName)
            {
                this.ConfigName = configName;
                BuildDestination = Path.Combine(BuildData.SolutionDir, "postbuild", "bin", this.ConfigName);
                Exists = Directory.Exists(BuildDestination);
                ContainsExe = Exists && Directory.GetFiles(BuildDestination).Where((s) => { return s.ToLower().EndsWith("postbuild.exe"); }).Any();
            }
        }


        private static string GetSquirrelPackageName(string packageName)
        {
            int extensionIndex = packageName.IndexOf(".nupkg");
            string sqPackage = packageName.Insert(extensionIndex, "-full");
            int vStart = FileUtils.IndexOfInt(packageName);
            sqPackage = sqPackage.Remove(vStart - 1, 1);
            sqPackage = sqPackage.Insert(vStart - 1, "-");
            return sqPackage;
        }

        static void ExecutePMCommand(string command)
        {
            var objDte = Marshal.GetActiveObject("VisualStudio.DTE") as DTE;
            objDte.ExecuteCommand("View.PackageManagerConsole", command);
        }

    }
}
