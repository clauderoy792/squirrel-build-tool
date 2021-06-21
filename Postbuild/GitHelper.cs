using Octokit;
using Prebuild;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Security;

namespace Postbuild
{
    public static class GitHelper
    {
        public static void AddCommitPush()
        {
            using (PowerShell powershell = PowerShell.Create())
            {
                //powershell.AddScript(@"Set-ExecutionPolicy -Scope LocalMachine -ExecutionPolicy RemoteSigned -Force");
                //powershell.AddScript(@"Start-Service sshd");
                powershell.AddScript($"cd '{BuildConfig.GetGitRepoLocalPath()}'");
                powershell.AddScript(@"Start-Service ssh-agent");
                powershell.AddScript($"ssh-add \"{BuildConfig.GetSSHKeyLocation()}\"");
                powershell.AddScript(@"git pull");
                powershell.AddScript(@"git add *");
                powershell.AddScript($"git commit -m 'Commit version {BuildConfig.GetAppVersionString()}'");
                powershell.AddScript(@"git push");

                Collection<PSObject> results = powershell.Invoke();
            }
        }

        internal static async Task CreateRelease()
        {
            try
            {
                string version = BuildConfig.GetAppVersionString();
                var newRelease = new NewRelease(version);
                newRelease.Name = $"New Update {version}";
                newRelease.Body = $"Update for version {version}";
                newRelease.Draft = false;
                newRelease.Prerelease = false;

                var gitHub = new GitHubClient(new ProductHeaderValue(BuildConfig.GetGitAppName()));
                gitHub.Credentials = new Credentials(BuildConfig.GetGitToken());
                bool success = false;
                int maxTries = 2;
                int current = 0;
                Release release = null;
                while (!success && current++ < maxTries)
                {
                    release = await gitHub.Repository.Release.Create("clauderoy790", BuildConfig.GetGitAppName(), newRelease);
                    success = release != null && !string.IsNullOrEmpty(release.UploadUrl);
                }

                if (!success)
                {
                    throw new Exception($"Failed to create new releaes, tried {current} times.");
                }

                var files = Directory.GetFiles(BuildConfig.GetGitRepoLocalPath());
                foreach (var file in files)
                {
                    if (!BuildConfig.ShouldIncludeFileInReleaseAsset(file))
                        continue;

                    using (var fs = File.OpenRead(file))
                    {
                        FileInfo info = new FileInfo(file);
                        var assetUpload = new ReleaseAssetUpload()
                        {
                            FileName = info.Name,
                            ContentType = MimeMapping.GetMimeMapping(info.Name),
                            RawData = fs
                        };
                        var asset = await gitHub.Repository.Release.UploadAsset(release, assetUpload);
                        Console.WriteLine($"Uploaded asset {info.Name} to release {version} successfully");
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create release: {ex.Message}");
            }
            Console.WriteLine("Release creation done");
        }

        public class TokenInfo
        {
            public string Token { get; set; }
            public Exception Error { get; set; }

            public TokenInfo(string token = null)
            {
                Token = token;
            }
        }
    }
}
