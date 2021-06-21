using Semver;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Prebuild
{
    public static class BuildConfig
    {
        const string VERSION_FILE_NAME = "version.txt";

        public static bool ShouldExecuteBuildEvent()
        {
            return IsCurrentBuildNewVersion() &&
                BuildData.ConfigurationName.ToLower() == "release" &&
                BuildData.IsValidData && Directory.Exists(BuildData.TargetDir);
        }

        public static bool IsCurrentBuildNewVersion()
        {
            SemVersion currentTxtVersion = new SemVersion(new Version(GetTxtFileVersion()));
            SemVersion settingsVersion = new SemVersion(new Version(GetAppVersionString()));

            return settingsVersion>currentTxtVersion;
        }

        public static bool ShouldIncludeFileInReleaseAsset(string file)
        {
            FileInfo current = new FileInfo(file);
            if (!current.Exists)
                return false;

            bool include = false;
            List<string> releaseFiles = new List<string>() { BuildConfig.GetTxtVersionFilePath() };

            //Include all files that are under the Releases folder that Squirrel generated for us
            var files = Directory.GetFiles(Path.Combine(BuildData.SolutionDir, "Releases"));
            foreach(var f in files)
            {
                var info = new FileInfo(f);
                releaseFiles.Add(info.Name);
            }

            //Check if file if is there
            foreach(var f in releaseFiles)
            {
                var fileInfo = new FileInfo(f);
                if (fileInfo.Name == current.Name)
                {
                    include = true;
                    break;
                }
            }

            return include;
        }

        public static string GetAppVersionString()
        {
            return GetSettingsValue("AppVersion");
        }

        public static string GetGitRepoLocalPath()
        {
            return GetSettingsValue("GitRepoLocalPath");
        }

        public static string GetGitRepoUrl()
        {
            return GetSettingsValue("GitRepoUrl");
        }

        public static string GetGitAppName()
        {
            return GetGitRepoUrl().Substring(GetGitRepoUrl().LastIndexOf('/') + 1).Replace(".git","");
        }
        public static string GetSSHKeyLocation()
        {
            return GetSettingsValue("SSHKeyLocation");
        }

        private static string GetSettingsValue(string name)
        {
            string val = "";
            string settingsPath = Path.Combine(BuildData.ProjectDir, "Properties/settings.settings");
            var lines = File.ReadAllLines(settingsPath).ToList();
            bool previousLineIsSettings = false;
            foreach (var line in lines)
            {
                if (previousLineIsSettings && line.Trim().ToLower().StartsWith("<value"))
                {
                    int first = line.IndexOf("\">");
                    int last = line.IndexOf("</");
                    val = line.Substring(first + 2, last - first - 2);
                    break;
                }
                else if (line.ToLower().Contains($"<Setting Name=\"{name}\"".ToLower()))
                {
                    previousLineIsSettings = true;
                }
            }
            return val;
        }

        public static string GetGitToken()
        {
            if (!File.Exists(GetSettingsValue("GitTokenFilePath")))
                throw new Exception("Fail to find GitTokenFile path setting");
            return File.ReadAllText(GetSettingsValue("GitTokenFilePath"));
        }

        public static string GetVerionTxtFileName()
        {
            return VERSION_FILE_NAME;
        }

        public static string GetTxtVersionFilePath()
        {
            return Path.Combine(BuildData.SolutionDir, GetVerionTxtFileName());
        }

        public static void SetTxtFileVersion(string newVersion)
        {
            string versionFile = GetTxtVersionFilePath();
            FileUtils.DeleteIfExist(versionFile);
            File.WriteAllText(versionFile, newVersion);
        }

        public static string GetTxtFileVersion()
        {
            string versionFile = GetTxtVersionFilePath();
            if (!File.Exists(versionFile))
                throw new Exception($"Could not find version.txt located at {versionFile}");
            return File.ReadAllText(versionFile);
        }

        public static int GetVersionInt(string strVer)
        {
            return int.Parse(strVer.Replace(".", ""));
        }

        public static void LogArgs(string file,string[] args)
        {
            StringBuilder sb = new StringBuilder();
            args.ToList().ForEach(s => { sb.AppendLine(s); });
            File.WriteAllText(file, sb.ToString());
        }
    }
}
