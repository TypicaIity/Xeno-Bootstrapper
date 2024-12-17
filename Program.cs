using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using System.IO.Compression;
using System.IO;
using System.Net;
using System.Net.Http;
using System;
using System.Diagnostics;

namespace Bootstrapper {
	internal class Program {
		static string latestVersion = "";
		static string downloadUrl = "";
		static string currentVersion = "0.0.0";

		static bool CheckVersion() {
			try {
				using (var client = new HttpClient()) {
					client.DefaultRequestHeaders.Add("User-Agent", "win-x64 .NET 8.0 Application 'Xeno Bootstrapper'");

					var task = Task.Run(() => client.GetAsync("https://api.github.com/repos/rlz-ve/x/releases/latest"));
					task.Wait();

					JObject json = JObject.Parse(task.Result.Content.ReadAsStringAsync().Result);

					downloadUrl = (string)json["assets"]![0]!["browser_download_url"]!;
					Match match = (
						new Regex(@"https:\/\/github\.com\/rlz-ve\/x\/releases\/download\/.+\/Xeno-v(\d+\.\d+\.\d+)-x64\.zip")
					).Match(downloadUrl);

					if (match.Success) {
						latestVersion = match.Groups[1].Value;
						return currentVersion == latestVersion;
					} else {
						Console.WriteLine("[!] Failed to fetch version! (2)");
						return false;
					}
				}
			} catch {
				Console.WriteLine("[!] Failed to fetch latest version! (1)");
				return false;
			}
		}

		public static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs) {
			DirectoryInfo dir = new DirectoryInfo(sourceDirName);

			if (!dir.Exists) {
				throw new DirectoryNotFoundException("Source directory does not exist: " + sourceDirName);
			}

			DirectoryInfo[] dirs = dir.GetDirectories();

			if (!Directory.Exists(destDirName))
				Directory.CreateDirectory(destDirName);

			FileInfo[] files = dir.GetFiles();
			foreach (FileInfo file in files) {
				string tempPath = Path.Combine(destDirName, file.Name);
				file.CopyTo(tempPath, true);
			}

			if (copySubDirs) {
				foreach (DirectoryInfo subDir in dirs) {
					string tempPath = Path.Combine(destDirName, subDir.Name);
					DirectoryCopy(subDir.FullName, tempPath, copySubDirs);
				}
			}
		}

		static void Main() {
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine("[-] discord.gg/getxeno | Bootstrapper by .nyc2");

			var programData = "C:\\ProgramData\\Xeno Bootstrapper\\";
			Directory.CreateDirectory(programData);

			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine("[-] Saving current version...");
			Thread.Sleep(100);

			if (File.Exists(programData + "CurrentVersion"))
				currentVersion = File.ReadAllText(programData + "CurrentVersion").Trim();
			else
				File.WriteAllText(programData + "CurrentVersion", currentVersion);

			Console.WriteLine("[-] Checking version...");
			Thread.Sleep(100);

			if (!CheckVersion()) {
				Console.WriteLine("[+] Xeno is updated! Downloading latest version...");
				Thread.Sleep(100);

				using (var client = new HttpClient()) {
					client.DefaultRequestHeaders.Add("User-Agent", "win-x64 .NET 8.0 Application 'Xeno Bootstrapper'");

					var res = Task.Run(() => client.GetAsync(downloadUrl));
					res.Wait();

					using (var fs = new FileStream(programData + "Xeno-v" + latestVersion + "-x64.zip", FileMode.CreateNew)) {
						res.Result.Content.CopyToAsync(fs);

						Console.ForegroundColor = ConsoleColor.Cyan;
						Console.WriteLine("[+] Extracting...");
						Thread.Sleep(100);

						ZipFile.ExtractToDirectory(fs, programData, true);
					}

					string oldXenoFolder = programData + "Xeno-v" + currentVersion + "-x64\\";
					string newXenoFolder = programData + "Xeno-v" + latestVersion + "-x64\\";

					string oldScriptsFolder = Path.Combine(oldXenoFolder, "scripts");
					string newScriptsFolder = Path.Combine(newXenoFolder, "scripts");

					if (Directory.Exists(oldScriptsFolder)) {
						Console.ForegroundColor = ConsoleColor.Cyan;
						Console.WriteLine("[+] Copying 'scripts' folder...");
						DirectoryCopy(oldScriptsFolder, newScriptsFolder, true);
					} else {
						Console.ForegroundColor = ConsoleColor.Red;
						Console.WriteLine("[!] 'scripts' folder not found in the old Xeno folder.");
					}

					if (Directory.Exists(oldXenoFolder)) {
						Console.ForegroundColor = ConsoleColor.Cyan;
						Console.ForegroundColor = ConsoleColor.Cyan;
						Console.WriteLine("[+] Deleting old Xeno...");
						Directory.Delete(oldXenoFolder, true);
					}

					Thread.Sleep(100);
					File.Delete(programData + "Xeno-v" + latestVersion + "-x64.zip");

					currentVersion = latestVersion;
					File.WriteAllText(programData + "CurrentVersion", currentVersion);
				}
			}

			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine("[+] Starting Xeno...");
			Thread.Sleep(100);

			Process.Start(programData + "Xeno-v" + currentVersion + "-x64\\Xeno.exe");

			Console.ForegroundColor = ConsoleColor.Gray;
			Thread.Sleep(500);
		}
	}
}
