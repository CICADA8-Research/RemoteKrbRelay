using NetFwTypeLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;

namespace CheckPort
{
    internal class Program
    {
        static void Main(string[] args)
        {
            ShowBanner();
            if (args.Length == 0)
            {
                Console.WriteLine("[?] You didn't specify anything. Look FindAvailablePort.exe -h");
            }
            var programs = new string[] { "SYSTEM", "ANY", "C:\\windows\\system32\\svchost.exe" };
            foreach (var entry in args.Select((value, index) => new { index, value }))
            {
                var argument = entry.value.ToLower();
                switch (argument)
                {
                    case "-program":
                        programs = args[entry.index + 1].Split(',');
                        break;
                    case "-h":
                    case "--help":
                        ShowHelp();
                        Environment.Exit(0);
                        break;
                }
            }

            var found = checkPorts(programs);
            if (!found)
            {
                Console.WriteLine("[-] No available ports found");
                Console.WriteLine("[-] Firewall will block our COM connection.");
                return;
            }
        }

        private static void ShowBanner()
        {
            var art = @"
                            /\_/\____,          /\     /\
                  ,___/\_/\ \  ~     /            \ _____\     
                  \     ~  \ )   XXX               (_)-(_)
                    XXX     /    /\_/\___,      Checker Collection  
                       \o-o/-o-o/   ~    / 
                        ) /     \    XXX
                       _|    / \ \_/
                    ,-/   _  \_/   \
                   / (   /____,__|  )
                  (  |_ (    )  \) _|
                 _/ _)   \   \__/   (_
                (,-(,(,(,/      \,),),)
            ";

            Console.WriteLine(art);
            Console.WriteLine("\t\tCICADA8 Research Team");
            Console.WriteLine("\t\tFrom MzHmO");
            Console.WriteLine("");
        }

        private static void ShowHelp()
        {
            Console.WriteLine("FindAvailablePort.exe");
            Console.WriteLine("Small tool that allow you to bypass the firewall during COM operations");
            Console.WriteLine();
            Console.WriteLine("[OPTIONS]");
            Console.WriteLine("-program : name of program to test. Default value is 'SYSTEM', 'ANY','C:\\windows\\system32\\svchost.exe' ");
            Console.WriteLine("-h/--help : shows this windows");
            Console.WriteLine();
        }
        private static bool checkPorts(string[] names)
        {
            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            var tcpConnInfoArray = ipGlobalProperties.GetActiveTcpListeners();
            var tcpPorts = tcpConnInfoArray.Select(i => i.Port).ToList();
            var find = false;
            foreach (var name in names)
            {
                for (var i = 10; i < 65535; i++)
                {
                    if (checkPort(i, name) && !tcpPorts.Contains(i))
                    {
                        Console.WriteLine("[*] {0} Is allowed through port {1}", name, i);
                        find = true;
                    }
                }
            }
            return find;
        }
        private static bool checkPort(int port, string name)
        {
            var mgr = (INetFwMgr)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwMgr"));
            if (!mgr.LocalPolicy.CurrentProfile.FirewallEnabled)
            {
                return true;
            }
            mgr.IsPortAllowed(name, NET_FW_IP_VERSION_.NET_FW_IP_VERSION_ANY, port, "", NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_TCP, out object allowed, out object restricted);
            return (bool)allowed;
        }
    }
}