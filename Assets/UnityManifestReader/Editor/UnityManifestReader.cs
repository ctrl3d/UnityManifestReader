using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace work.ctrl3d.UnityManifestReader
{
    public static class UnityManifestReader
    {
        public enum PackageType
        {
            Unity,
            Git,
            OpenUpm,
            Standard
        }
        
        [Serializable]
        public class PackageInfo
        {
            public string Name { get; }
            public string Url { get; }
            public PackageType Type { get; }
            public string Version => GetVersionFromCache(Name);

            public PackageInfo(string name, string url, PackageType type)
            {
                Name = name;
                Url = url;
                Type = type;
            }
            
            public string GetVersionFromCache(string packageName)
            {
                try
                {
                    var packageCachePath = Path.Combine(Application.dataPath, "..", "Library", "PackageCache");

                    if (!Directory.Exists(packageCachePath))
                    {
                        Debug.LogError("Package cache directory not found");
                        return "";
                    }

                    // 지정된 패키지 폴더 찾기
                    var packageDirectories = Directory.GetDirectories(packageCachePath, $"{packageName}@*");

                    if (packageDirectories.Length == 0)
                    {
                        Debug.LogError($"Package {packageName} not found in cache");
                        return "";
                    }

                    // 폴더 이름에서 버전 추출 (패키지명@버전 형식)
                    var folderName = Path.GetFileName(packageDirectories[0]);
                    var version = folderName.Split('@')[1];

                    // package.json에서 정확한 버전 확인
                    var packageJsonPath = Path.Combine(packageDirectories[0], "package.json");

                    if (!File.Exists(packageJsonPath)) return version;

                    var jsonData = File.ReadAllText(packageJsonPath);
                    var packageJson = JObject.Parse(jsonData);
                    version = packageJson["version"]?.ToString() ?? version;

                    return version;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to get package version: {ex.Message}");
                    return "";
                }
            }

            public override string ToString()
            {
                if (string.IsNullOrEmpty(Version))
                {
                    return $"{Name} (URL: {Url})";
                }

                return $"{Name}@{Version}";
            }
        }

        public static List<PackageInfo> GetAllPackages(string manifestPath = null)
        {
            try
            {
                var manifestJson = ReadManifestJson(manifestPath);
                if (manifestJson == null) return new List<PackageInfo>();

                var openUpmScopes = GetOpenUpmScopes(manifestJson);
                return ParseDependencies(manifestJson, openUpmScopes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Errors during package analysis: {ex.Message}");
                return new List<PackageInfo>();
            }
        }

        public static List<PackageInfo> GetPackages(PackageType type, string manifestPath = null)
        {
            return GetAllPackages(manifestPath)
                .Where(p => p.Type == type)
                .ToList();
        }

        public static void PrintPackages(string manifestPath = null)
        {
            var allPackages = GetAllPackages(manifestPath);
            var packagesByType = allPackages.GroupBy(p => p.Type)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var kvp in packagesByType)
            {
                Debug.Log($"=== {kvp.Key} Package ({kvp.Value.Count}) ===");
                foreach (var package in kvp.Value)
                {
                    Debug.Log($"  {package.Name} - {package.Url}");
                }
            }
        }

        public static Dictionary<PackageType, List<PackageInfo>> GetPackagesByType(string manifestPath = null)
        {
            var allPackages = GetAllPackages(manifestPath);
            return allPackages.GroupBy(p => p.Type)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        private static JObject ReadManifestJson(string manifestPath)
        {
            if (string.IsNullOrEmpty(manifestPath))
            {
                manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
            }

            if (!File.Exists(manifestPath))
            {
                Debug.LogError($"The manifest.json file was not found: {manifestPath}");
                return null;
            }

            // JSON 파일 읽기
            var jsonText = File.ReadAllText(manifestPath);
            return JObject.Parse(jsonText);
        }

        private static List<PackageInfo> ParseDependencies(JObject manifestJson, HashSet<string> openUpmScopes)
        {
            var result = new List<PackageInfo>();
            var dependencies = (JObject)manifestJson["dependencies"];
            if (dependencies == null) return result;

            foreach (var property in dependencies.Properties())
            {
                var packageName = property.Name;
                var packageUrl = property.Value.ToString();

                var packageType = DeterminePackageType(packageName, packageUrl, openUpmScopes);

                // 패키지 정보 생성 및 추가
                var packageInfo = new PackageInfo(packageName, packageUrl, packageType);
                result.Add(packageInfo);
            }

            return result;
        }

        private static PackageType DeterminePackageType(string packageName, string packageUrl,
            HashSet<string> openUpmScopes)
        {
            if (packageName.StartsWith("com.unity", StringComparison.Ordinal))
            {
                return PackageType.Unity;
            }

            if (IsGitPackage(packageUrl)) return PackageType.Git;


            if (IsOpenUpmPackage(packageName, openUpmScopes))
            {
                return PackageType.OpenUpm;
            }

            return PackageType.Standard;
        }

        private static bool IsGitPackage(string url)
        {
            return url.StartsWith("git+", StringComparison.OrdinalIgnoreCase) ||
                   url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   url.Contains("github.com", StringComparison.OrdinalIgnoreCase) ||
                   url.Contains("gitlab.com", StringComparison.OrdinalIgnoreCase) ||
                   url.Contains("bitbucket.org", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsOpenUpmPackage(string packageName, HashSet<string> openUpmScopes)
        {
            return openUpmScopes.Any(packageName.StartsWith);
        }

        private static HashSet<string> GetOpenUpmScopes(JObject manifestJson)
        {
            var openUpmScopes = new HashSet<string>();

            if (manifestJson["scopedRegistries"] is not JArray scopedRegistries) return openUpmScopes;

            foreach (var registry in scopedRegistries)
            {
                var url = registry["url"]?.ToString();
                var isOpenUpm = url != null && url.Contains("openupm");

                if (isOpenUpm)
                {
                    var scopes = (JArray)registry["scopes"];
                    if (scopes != null)
                    {
                        foreach (var scope in scopes)
                        {
                            openUpmScopes.Add(scope.ToString());
                        }
                    }
                }
            }

            return openUpmScopes;
        }
    }
}