/*
 * OpenVPNWrapper
 * Copyright (C) 2021 Simon Geier
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenVPNWrapper
{
    class Program
    {
        static readonly string upd = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        static readonly string cwd = AppDomain.CurrentDomain.BaseDirectory;
        static readonly string scriptFile = $@"{cwd}ResolveIPv6.ps1";
        static readonly string logDir = $@"{upd}\Documents\OpenVPNWrapper";
        static readonly string logFile = $@"{logDir}\OpenVPNWrapper_{DateTimeOffset.Now.ToUnixTimeSeconds()}.log";

        [STAThread]
        public static void Main()
        {
            try
            {
                Execute().Wait();
            }
            catch (Exception ex)
            {
                if (ex.GetType() == typeof(AggregateException) && ex.InnerException?.GetType() == typeof(FileNotFoundException))
                {
                    Console.WriteLine("OpenVPN GUI (openvpn-gui.exe) not found automatically. Please specify the path manually.");
                    OpenFileDialog fd = new OpenFileDialog
                    {
                        InitialDirectory = @"C:\",
                        Filter = "OpenVPN GUI (*.exe)|*.exe"
                    };
                    List<string> psLines = null;

                    try
                    {
                        psLines = File.ReadAllLines(scriptFile).ToList();
                        psLines.RemoveAll(entry => entry.StartsWith("$ovpnProgram"));

                        if (fd.ShowDialog() == DialogResult.OK)
                        {
                            Process.Start(fd.FileName);
                            psLines.Insert(psLines.IndexOf(string.Empty), $"$ovpnProgram = \"{fd.FileName}\"");
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        ex = e;
                    }
                    finally
                    {
                        if (psLines != null) File.WriteAllLines(scriptFile, psLines);
                    }
                }

                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
                File.WriteAllText(logFile, $"Exception occurred during call of OpenVPNWrapper: {ex}");
                Console.WriteLine("Exception occurred during execution of OpenVPNWrapper.");
                Console.WriteLine($"See {logFile} for details.");
                AvoidAutomaticApplicationClosure().Wait();
            }
        }

        public static async Task Execute()
        {
            string script = File.ReadAllText(scriptFile);
            var result = string.Empty;
            try
            {
                Console.WriteLine($"Running script {scriptFile} in PowerShell...");
                result = PowerShell
                    .Create()
                    .AddScript(script)
                    .Invoke("Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy Unrestricted")
                    .FirstOrDefault()
                    .ToString();
            }
            catch (Exception ex)
            {
                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
                File.WriteAllText(logFile, $"Exception occurred during call of OpenVPNWrapper: {ex}");
                Console.WriteLine("Exception occurred during execution of PowerShell script.");
                Console.WriteLine($"See {logFile} for details.");
                await AvoidAutomaticApplicationClosure();
                return;
            }

            string ovpnFile = script.Split('\n').Where(line => line.StartsWith("$ovpnFile")).FirstOrDefault();
            int firstIndex = ovpnFile?.IndexOf("\"") + 1 ?? default;
            int lastIndex = ovpnFile?.LastIndexOf("\"") ?? default;
            ovpnFile = ovpnFile?.Substring(firstIndex, lastIndex - firstIndex);

            if (string.IsNullOrEmpty(result))
            {
                Console.WriteLine("Error: Result of PowerShell script resolving IPv6 address was null or empty.");
                Console.WriteLine("Please ensure correctness of the DNS server in the PowerShell script and IPv6 network availability.");
                await AvoidAutomaticApplicationClosure();
                return;
            }

            Console.WriteLine("Opening OpenVPN configuration file...");

            if (string.IsNullOrEmpty(ovpnFile))
            {
                Console.WriteLine("Error: OpenVPN configuration file (.ovpn) could not be received from PowerShell script.");
                Console.WriteLine("Please ensure it is specified as absolute path correctly between quotation marks as variable.");
                Console.WriteLine("Example: $ovpnFile = \"C:\\Program Files\\OpenVPN\\config\\example.ovpn\"");
                await AvoidAutomaticApplicationClosure();
                return;
            }

            Console.WriteLine($"Exchanging current IPv6 address with address {result} resolved...");

            string ovpnConfig = Regex.Replace(File.ReadAllText(ovpnFile), "(?:^" +
               @"|(?<=\s))(([0-9a-fA-F]{1,4}:){7,7}[0-9a-fA-F]{1,4}" +
                "|([0-9a-fA-F]{1,4}:){1,7}:" +
                "|([0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}" +
                "|([0-9a-fA-F]{1,4}:){1,5}(:[0-9a-fA-F]{1,4}){1,2}" +
                "|([0-9a-fA-F]{1,4}:){1,4}(:[0-9a-fA-F]{1,4}){1,3}" +
                "|([0-9a-fA-F]{1,4}:){1,3}(:[0-9a-fA-F]{1,4}){1,4}" +
                "|([0-9a-fA-F]{1,4}:){1,2}(:[0-9a-fA-F]{1,4}){1,5}" +
                "|[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})" +
                "|:((:[0-9a-fA-F]{1,4}){1,7}|:)" +
                "|fe80:(:[0-9a-fA-F]{0,4}){0,4}%[0-9a-zA-Z]{1,}" +
               @"|::(ffff(:0{1,4}){0,1}:){0,1}((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]" +
                "|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])" +
               @"|([0-9a-fA-F]{1,4}:){1,4}:((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.)" +
               @"{3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9]))(?=\s|$)",
               result.ToString());

            if (ovpnConfig != File.ReadAllText(ovpnFile))
            {
                Console.WriteLine("Exchanging IPv6 address: Writing address to file...");
                File.WriteAllText(ovpnFile, ovpnConfig);
                Console.WriteLine("IPv6 address of server was successfully written to configuration file. Starting OpenVPN GUI...");
            }
            else
            {
                Console.WriteLine("No changes on IPv6 address registered. " +
                    "Please check the correct behaviour of the attempted address resolution.");
                Console.WriteLine("Starting OpenVPN GUI in 5 seconds, press Enter to start immediately...");
                CancellationTokenSource cts = new CancellationTokenSource();
                await Task.WhenAny(Task.Run(Console.In.ReadLineAsync, cts.Token), Task.Delay(5000, cts.Token));
                cts.Cancel();
            }

            string ovpnProgram = script.Split('\n').Where(line => line.StartsWith("$ovpnProgram")).FirstOrDefault();
            firstIndex = ovpnProgram?.IndexOf("\"") + 1 ?? default;
            lastIndex = ovpnProgram?.LastIndexOf("\"") ?? default;
            ovpnProgram = ovpnProgram?.Substring(firstIndex, lastIndex - firstIndex);

            try
            {
                if (!string.IsNullOrEmpty(ovpnProgram))
                {
                    Console.WriteLine("Using cached OpenVPN GUI program path automatically in 5 seconds, " +
                        "press Enter to declare path manually again...");
                    if (await UserInterrupted()) throw new FileNotFoundException("openvpn-gui.exe");
                    Process.Start(ovpnProgram);
                }
                else if (File.Exists(@"C:\Program Files\OpenVPN\bin\openvpn-gui.exe"))
                    Process.Start(@"C:\Program Files\OpenVPN\bin\openvpn-gui.exe");
                else if (File.Exists(@"C:\Program Files (x86)\OpenVPN\bin\openvpn-gui.exe"))
                    Process.Start(@"C:\Program Files (x86)\OpenVPN\bin\openvpn-gui.exe");
                else throw new FileNotFoundException("openvpn-gui.exe");
            }
            catch (FileNotFoundException) { throw; }
            catch (Exception ex)
            {
                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
                File.WriteAllText(logFile, $"Exception occurred during call of OpenVPNWrapper: {ex}");
                Console.WriteLine("Exception occurred during call of OpenVPN GUI application.");
                Console.WriteLine($"See {logFile} for details.");
                await AvoidAutomaticApplicationClosure();
            }
        }

        public static async Task AvoidAutomaticApplicationClosure()
        {
            Console.WriteLine("Press Enter to avoid automatic application closure in 5 seconds...");
            if (await UserInterrupted())
            {
                Console.WriteLine("Press Enter to manually close application.");
                await Console.In.ReadLineAsync();
            }
        }

        public static async Task<bool> UserInterrupted()
        {
            DateTime start = DateTime.Now;
            CancellationTokenSource cts = new CancellationTokenSource();
            await Task.WhenAny(Task.Run(Console.In.ReadLineAsync, cts.Token), Task.Delay(5000, cts.Token));
            DateTime end = DateTime.Now;
            cts.Cancel();
            return end - start < TimeSpan.FromSeconds(5);
        }
    }
}
