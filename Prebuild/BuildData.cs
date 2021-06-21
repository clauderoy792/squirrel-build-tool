using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Prebuild
{
    public static class BuildData
    {
        public static void Init(string[] args)
        {
            IsValidData = false;
            args = args[0].Split('|');
            if (args == null || args.Length < 5)
                return;

            ProjectName = args[0].Trim();
            ProjectDir = args[1].Trim();
            SolutionDir = args[2].Trim();
            TargetDir = args[3].Trim();
            ConfigurationName = args[4];
            SettingsVersion = BuildConfig.GetAppVersionString();
            CurrentVersion = BuildConfig.GetTxtFileVersion();
            IsValidData = true;
        }

        public static bool IsValidData { get; private set; }

        public static string ConfigurationName { get; private set; }
        public static string SettingsVersion { get; private set; }
        public static string CurrentVersion { get; private set; }
        public static string ProjectName { get; private set; }
        public static string ProjectDir { get; private set; }
        public static string SolutionDir { get; private set; }

        public static string TargetDir { get; private set; }
    }
}
