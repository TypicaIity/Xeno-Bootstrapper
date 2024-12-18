using System.Diagnostics;
using Newtonsoft.Json.Linq;
using System.IO.Compression;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace Bootstrapper {
	[SupportedOSPlatform("windows")]
	internal class Program {
		static string latestVersion = "";
		static string downloadUrl = "";
		static string currentVersion = "0.0.0";
		static string bootstrapperVersion = "v2";

		private static bool CheckVersion() {
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
						Console.ForegroundColor = ConsoleColor.Red;
						Console.WriteLine("[F] Failed to fetch version! (2)");

						return false;
					}
				}
			} catch {
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("[F] Failed to fetch version! (1)");

				return false;
			}
		}

		private static string[] GetBootstrapperVersion() { 
			try {
				using (var client = new HttpClient()) {
					client.DefaultRequestHeaders.Add("User-Agent", "win-x64 .NET 8.0 Application 'Xeno Bootstrapper'");

					var task = Task.Run(() => client.GetAsync("https://typicaiity.github.io/endpoints/xbootstrapper.json"));
					task.Wait();

					JObject json = JObject.Parse(task.Result.Content.ReadAsStringAsync().Result);

					return [(string)json["latest"]!, (string)json["changelog"]!];
				}
			} catch (Exception e) {
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine($"[F] Failed to fetch version: {e.Message}");

				return [bootstrapperVersion, ""];
			}
		}

		private static bool RegistryKeyExists(string key) {
			try {
				using (var registryKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(key))
					return registryKey != null;
			} catch {
				return false;
			}
		}

		private static bool IsDotnetInstalled() {
			try {
				var psi = new ProcessStartInfo {
					FileName = "dotnet",
					Arguments = "--version",
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true
				};
				using (var process = Process.Start(psi)) {
					process?.WaitForExit();
					return process?.ExitCode == 0;
				}
			}
			catch {
				return false;
			}
		}

		private static void CheckDependencies() {
			if (!RegistryKeyExists(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64")) {
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine("[!] Visual C++ Redistributable not found. Downloading and installing...");

				string vcInstaller = Path.Combine(Path.GetTempPath(), "vc_redist.x64.exe");
				Task.Run(async () => {
					var content = await new HttpClient().GetByteArrayAsync("https://aka.ms/vs/17/release/vc_redist.x64.exe");
					File.WriteAllBytes(vcInstaller, content);
				}).Wait();

				var processStartInfo = new ProcessStartInfo {
					FileName = vcInstaller,
					Arguments = "/quiet /norestart",
					UseShellExecute = true
				};
				Process.Start(processStartInfo)!.WaitForExit();
			}

			Thread.Sleep(100);

			if (!IsDotnetInstalled()) {
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("[!] The .NET CLI is not installed. Downloading and installing...");
				string netInstaller = Path.Combine(Path.GetTempPath(), "dotnet_installer.exe");
				Task.Run(async () => {
					var content = await new HttpClient().GetByteArrayAsync("https://go.microsoft.com/fwlink/?linkid=2088631");
					File.WriteAllBytes(netInstaller, content);
				}).Wait();

				var processStartInfo = new ProcessStartInfo {
					FileName = netInstaller,
					Arguments = "/quiet /norestart",
					UseShellExecute = true
				};
				Process.Start(processStartInfo)!.WaitForExit();
			} else {
				var psi = new ProcessStartInfo {
					FileName = "dotnet",
					Arguments = "--list-runtimes",
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true
				};

				using (var process = Process.Start(psi)!) {
					using (var reader = process.StandardOutput) {
						if (!reader.ReadToEnd().Contains("Microsoft.NETCore.App 8.")) {
							Console.ForegroundColor = ConsoleColor.Yellow;
							Console.WriteLine("[!] .NET Runtime not found. Downloading and installing...");

							string netInstaller = Path.Combine(Path.GetTempPath(), "dotnet_installer.exe");
							Task.Run(async () => {
								var content = await new HttpClient().GetByteArrayAsync("https://go.microsoft.com/fwlink/?linkid=2088631");
								File.WriteAllBytes(netInstaller, content);
							}).Wait();

							var processStartInfo = new ProcessStartInfo {
								FileName = netInstaller,
								Arguments = "/quiet /norestart",
								UseShellExecute = true
							};
							Process.Start(processStartInfo)!.WaitForExit();
						}
					}
				}
			}
		}

		private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs) {
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

		public static void Main() {
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine("[*] discord.gg/getxeno");

			var programData = "C:\\ProgramData\\Xeno Bootstrapper\\";
			Directory.CreateDirectory(programData);

			Console.ForegroundColor = ConsoleColor.DarkGray;
			Console.WriteLine("[~] Saving current version...");
			Thread.Sleep(100);

			if (File.Exists(programData + "CurrentVersion"))
				currentVersion = File.ReadAllText(programData + "CurrentVersion").Trim();
			else
				File.WriteAllText(programData + "CurrentVersion", currentVersion);

			if (File.Exists(programData + "BootstrapperVersion"))
				bootstrapperVersion = File.ReadAllText(programData + "BootstrapperVersion").Trim();
			else
				File.WriteAllText(programData + "BootstrapperVersion", bootstrapperVersion);

			Console.ForegroundColor = ConsoleColor.DarkGray;
			Console.WriteLine("[~] Checking bootstrapper version...");
			Thread.Sleep(100);

			string[] versionInfo = GetBootstrapperVersion();

			if (versionInfo[0] != bootstrapperVersion) {
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine($"[!] A new bootstrapper version is available! ({bootstrapperVersion} -> {versionInfo[0]})");
				Thread.Sleep(100);
				Console.WriteLine($"      Changelog:\n{versionInfo[1]}");

				string currentPath = AppContext.BaseDirectory;
				string exeName = AppDomain.CurrentDomain.FriendlyName;
				string currentExePath = Path.Combine(currentPath, exeName);
				string newExePath = currentExePath + ".new";

				using (var client = new HttpClient()) {
					client.DefaultRequestHeaders.Add("User-Agent", "win-x64 .NET 8.0 Application 'Xeno Bootstrapper'");

					var res = Task.Run(() => client.GetAsync("https://github.com/TypicaIity/silly-bootstrapper/releases/latest/download/Xeno.Bootstrapper.exe"));
					res.Wait();

					using (var fs = new FileStream(newExePath, FileMode.CreateNew)) {
						res.Result.Content.CopyToAsync(fs).Wait();
					}
				}

				try {
					File.Move(newExePath, currentExePath, true);

					bootstrapperVersion = versionInfo[0];
					File.WriteAllText(programData + "BootstrapperVersion", bootstrapperVersion);

					Process.Start(new ProcessStartInfo { FileName = currentExePath, UseShellExecute = true });
					Environment.Exit(0);
				} catch (Exception ex) {
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine($"[F] Update failed: {ex.Message}");
					if (File.Exists(newExePath)) File.Delete(newExePath);

					Environment.Exit(1);
				}
			}

			Console.WriteLine("[~] Checking dependencies...");
			Thread.Sleep(100);

			CheckDependencies();

			Console.ForegroundColor = ConsoleColor.DarkGray;
			Console.WriteLine("[~] Checking version...");
			Thread.Sleep(100);

			if (!CheckVersion()) {
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine("[+] Xeno is updated! Downloading latest version...");
				Thread.Sleep(100);

				using (var client = new HttpClient()) {
					client.DefaultRequestHeaders.Add("User-Agent", "win-x64 .NET 8.0 Application 'Xeno Bootstrapper'");

					var res = Task.Run(() => client.GetAsync(downloadUrl));
					res.Wait();

					using (var fs = new FileStream(programData + "Xeno-v" + latestVersion + "-x64.zip", FileMode.CreateNew)) {
						res.Result.Content.CopyToAsync(fs);

						Console.ForegroundColor = ConsoleColor.Green;
						Console.WriteLine("[+] Extracting...");
						Thread.Sleep(100);

						ZipFile.ExtractToDirectory(fs, programData, true);

						fs.Close();
					}

					string oldXenoFolder = programData + "Xeno-v" + currentVersion + "-x64\\";
					string newXenoFolder = programData + "Xeno-v" + latestVersion + "-x64\\";

					string oldScriptsFolder = Path.Combine(oldXenoFolder, "scripts");
					string newScriptsFolder = Path.Combine(newXenoFolder, "scripts");

					if (Directory.Exists(oldScriptsFolder)) {
						Console.ForegroundColor = ConsoleColor.Green;
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

					Thread.Sleep(100);
				}
			}

			Console.ForegroundColor = ConsoleColor.Blue;
			Console.WriteLine("[>] Starting Xeno...");
			Thread.Sleep(100);

			Process.Start(programData + "Xeno-v" + currentVersion + "-x64\\Xeno.exe");

			Console.ResetColor();
			Thread.Sleep(500);
		}
	}
}
