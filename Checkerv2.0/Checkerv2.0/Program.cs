using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Excel = Microsoft.Office.Interop.Excel;
using System.Reflection;

namespace Checkerv2._0
{
    public class SDInfo
    {
        public string Principal { get; set; }
        public string Type { get; set; }
        public int AccessMask { get; set; }
        public List<string> Access { get; set; }
        public SecurityIdentifier SID { get; set; }
    }

    public class ObjectInfo
    {
        public string ApplicationID { get; set; }
        public string ApplicationName { get; set; }
        public string RunAs { get; set; }
        public string AuthLevel { get; set; }
        public string ImpLevel { get; set; }
        public List<string> CLSIDs { get; set; }

        public List<SDInfo> AccessInfo { get; set; }
        public List<SDInfo> LaunchInfo { get; set; }
    }

    public class Parser
    {
        const int COM_RIGHTS_EXECUTE = 1;
        const int COM_RIGHTS_EXECUTE_LOCAL = 2;
        const int COM_RIGHTS_EXECUTE_REMOTE = 4;
        const int COM_RIGHTS_ACTIVATE_LOCAL = 8;
        const int COM_RIGHTS_ACTIVATE_REMOTE = 16;

        public static List<ObjectInfo> GetDCOMObjects()
        {
            var result = new List<ObjectInfo>();

            foreach (var appID in GetAppIDs())
            {
                var keyPath = $@"HKEY_CLASSES_ROOT\AppID\{appID}";
                var obj = new ObjectInfo();

                obj.ApplicationID = appID;
                obj.ApplicationName = GetValueFromRegistry(keyPath, "");
                obj.RunAs = GetValueFromRegistry(keyPath, "RunAs");

                if (string.IsNullOrEmpty(obj.RunAs))
                {
                    obj.RunAs = "The Launching User";
                }

                obj.AccessInfo = GetPermissionsForAppID(keyPath, "AccessPermission");
                obj.LaunchInfo = GetPermissionsForAppID(keyPath, "LaunchPermission");
                obj.CLSIDs = GetCLSIDsFromAppid(obj.ApplicationID);

                result.Add(obj);
            }

            return result;
        }

        private static List<string> GetCLSIDsFromAppid(string appID)
        {
            var list = new List<string>();

            using (RegistryKey clsidRoot = Registry.ClassesRoot.OpenSubKey("CLSID"))
            {
                if (clsidRoot == null)
                {
                    Console.WriteLine("[-] Cant open HKCR\\CLSID.");
                    return list;
                }

                var clsidNames = clsidRoot.GetSubKeyNames();
                var partitioner = Partitioner.Create(clsidNames, EnumerablePartitionerOptions.NoBuffering);

                var concurrentBag = new ConcurrentBag<string>();

                Parallel.ForEach(partitioner, clsid =>
                {
                    using (var subKey = clsidRoot.OpenSubKey(clsid))
                    {
                        if (subKey == null) return;

                        object appidValue = subKey.GetValue("AppID");

                        if (appidValue is string appid && appid == appID)
                        {
                            concurrentBag.Add(clsid);
                        }
                    }
                });

                list = concurrentBag.ToList();
            }

            return list;
        }

        private static List<SDInfo> GetPermissionsForAppID(string keyPath, string permissionName)
        {
            var result = new List<SDInfo>();

            try
            {
                var regPerms = (byte[])Registry.GetValue(keyPath, permissionName, null);
                if (regPerms == null)
                {
                    // Console.WriteLine($"[-] Cant get {permissionName} for {keyPath}");
                }

                var sd = new RawSecurityDescriptor(regPerms, 0);

                foreach (CommonAce ace in sd.DiscretionaryAcl)
                {
                    var access = new List<string>();
                    var userName = "";
                    var sid = ace.SecurityIdentifier;

                    try
                    {
                        userName = sid.Translate(typeof(NTAccount)).ToString();
                    }
                    catch
                    {
                        //Console.WriteLine("Unable to map SID to username");        
                    }

                    if (permissionName == "LaunchPermission")
                    {
                        if ((ace.AccessMask & COM_RIGHTS_EXECUTE_LOCAL) != 0 ||
                            ((ace.AccessMask & COM_RIGHTS_EXECUTE) != 0 &&
                            (ace.AccessMask & (COM_RIGHTS_EXECUTE_REMOTE | COM_RIGHTS_ACTIVATE_REMOTE | COM_RIGHTS_ACTIVATE_LOCAL)) == 0))
                        {
                            access.Add("LocalLaunch");
                        }

                        if ((ace.AccessMask & COM_RIGHTS_EXECUTE_REMOTE) != 0 ||
                            ((ace.AccessMask & COM_RIGHTS_EXECUTE) != 0 &&
                            (ace.AccessMask & (COM_RIGHTS_EXECUTE_LOCAL | COM_RIGHTS_ACTIVATE_REMOTE | COM_RIGHTS_ACTIVATE_LOCAL)) == 0))
                        {
                            access.Add("RemoteLaunch");
                        }

                        if ((ace.AccessMask & COM_RIGHTS_ACTIVATE_LOCAL) != 0 ||
                            ((ace.AccessMask & COM_RIGHTS_EXECUTE) != 0 &&
                            (ace.AccessMask & (COM_RIGHTS_EXECUTE_LOCAL | COM_RIGHTS_EXECUTE_REMOTE | COM_RIGHTS_ACTIVATE_REMOTE)) == 0))
                        {
                            access.Add("LocalActivation");
                        }

                        if ((ace.AccessMask & COM_RIGHTS_ACTIVATE_REMOTE) != 0 ||
                            ((ace.AccessMask & COM_RIGHTS_EXECUTE) != 0 &&
                            (ace.AccessMask & (COM_RIGHTS_EXECUTE_LOCAL | COM_RIGHTS_EXECUTE_REMOTE | COM_RIGHTS_ACTIVATE_LOCAL)) == 0))
                        {
                            access.Add("RemoteActivation");
                        }
                    }
                    else if (permissionName == "AccessPermission")
                    {
                        if ((ace.AccessMask & COM_RIGHTS_EXECUTE_LOCAL) != 0 ||
                            ((ace.AccessMask & COM_RIGHTS_EXECUTE) != 0 &&
                            (ace.AccessMask & COM_RIGHTS_EXECUTE_REMOTE) == 0))
                        {
                            access.Add("LocalAccess");
                        }

                        if ((ace.AccessMask & COM_RIGHTS_EXECUTE_REMOTE) != 0 ||
                            ((ace.AccessMask & COM_RIGHTS_EXECUTE) != 0 &&
                            (ace.AccessMask & COM_RIGHTS_EXECUTE_LOCAL) == 0))
                        {
                            access.Add("RemoteAccess");
                        }
                    }

                    result.Add(new SDInfo
                    {
                        Access = access,
                        AccessMask = ace.AccessMask,
                        Type = ace.AceType.ToString(),
                        SID = sid,
                        Principal = userName
                    });
                }

            }
            catch (Exception ex)
            {

            }

            return result;
        }

        private static List<string> GetAppIDs()
        {
            var list = new List<string>();
            var registryKey = $"AppID";

            using (var key = Registry.ClassesRoot.OpenSubKey(registryKey))
            {
                if (key != null)
                {
                    foreach (var subkeyName in key.GetSubKeyNames())
                    {
                        using (var subkey = key.OpenSubKey(subkeyName))
                        {
                            if (subkey != null)
                            {
                                if (IsGuid(subkeyName))
                                {
                                    list.Add(subkeyName);
                                }
                                else
                                {
                                    list.Add(subkey.GetValue("AppID") as string);
                                }
                            }
                        }
                    }
                }
            }
            return list;
        }

        private static bool IsGuid(string candidate)
        {
            return Guid.TryParse(candidate, out _);
        }

        private static string GetValueFromRegistry(string keyPath, string value)
        {
            return (string)Registry.GetValue(keyPath, value, null);
        }
    }


    internal class Program
    {
        static void Main(string[] args)
        {
            ShowBanner();

            if (args.Length == 0)
            {
                Console.WriteLine("[?] You didn't specify anything. Look Checker.exe -h");
            }

            var outFormat = "csv"; //csv / xlsx
            var outFile = $"Output";
            var showTable = false;
            foreach (var entry in args.Select((value, index) => new { index, value }))
            {
                var argument = entry.value.ToLower();
                switch (argument)
                {
                    case "-outformat":
                        outFormat = args[entry.index + 1];
                        break;

                    case "-outfile":
                        outFile = args[entry.index + 1];
                        break;
                    case "-showtable":
                        showTable = true;
                        break;

                    case "-h":
                    case "--help":
                        ShowHelp();
                        Environment.Exit(0);
                        break;
                }
            }

            Console.WriteLine($"[+] Result will be in {outFile}, format {outFormat}");

            var objects = Parser.GetDCOMObjects();

            switch (outFormat)
            {
                case "csv":
                    try
                    {
                        var csvFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{outFile}.{outFormat}");

                        using (var writer = new StreamWriter(csvFilePath, false, System.Text.Encoding.UTF8))
                        {
                            var headers = new string[] { "ApplicationID", "ApplicationName", "RunAs",
                                    "LaunchAccess", "LaunchType", "LaunchPrincipal", "LaunchSID",
                                    "AccessAccess", "AccessType", "AccessPrincipal", "AccessSID",
                                    "AuthLevel", "ImpLevel", "CLSIDs"};
                            writer.WriteLine(string.Join(",", headers));

                            foreach (var obj in objects)
                            {
                                var clsids = "";
                                if (obj.CLSIDs != null)
                                {
                                    clsids = string.Join(";", obj.CLSIDs);
                                }
                                var maxEntries = Math.Max(obj.LaunchInfo.Count, obj.AccessInfo.Count);

                                for (var i = 0; i < maxEntries; i++)
                                {
                                    var launch = (i < obj.LaunchInfo.Count) ? obj.LaunchInfo[i] : null;
                                    var access = (i < obj.AccessInfo.Count) ? obj.AccessInfo[i] : null;

                                    var row = new string[]
                                    {
                                        obj.ApplicationID,
                                        obj.ApplicationName,
                                        obj.RunAs,
                                        launch != null ? string.Join(". ", launch.Access) : "",
                                        launch != null ? launch.Type : "",
                                        launch != null ? launch.Principal : "",
                                        launch != null ? launch.SID.ToString() : "",
                                        access != null ? string.Join(". ", access.Access) : "",
                                        access != null ? access.Type : "",
                                        access != null ? access.Principal : "",
                                        access != null ? access.SID.ToString() : "",
                                        obj.AuthLevel,
                                        obj.ImpLevel,
                                        clsids
                                    };

                                    writer.WriteLine(string.Join(", ", row));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[-] Could not write to CSV file.");
                        Console.WriteLine(ex.Message);
                    }


                    break;

                case "xlsx":
                    try
                    {

                        var excelFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{outFile}.{outFormat}");

                        var excelApp = new Excel.Application();
                        excelApp.Visible = false;
                        excelApp.DisplayAlerts = false;

                        var workBook = excelApp.Workbooks.Add(Missing.Value);
                        var workSheet = (Excel.Worksheet)workBook.Worksheets[1];

                        var headers = new string[] { "ApplicationID", "ApplicationName", "RunAs",
                                         "LaunchAccess", "LaunchType", "LaunchPrincipal", "LaunchSID",
                                         "AccessAccess", "AccessType", "AccessPrincipal", "AccessSID",
                                         "AuthLevel", "ImpLevel", "CLSIDs"};
                        for (var i = 0; i < headers.Length; i++)
                        {
                            workSheet.Cells[1, i + 1] = headers[i];
                            var headerCell = (Excel.Range)workSheet.Cells[1, i + 1];
                            headerCell.Font.Bold = true;
                            headerCell.BorderAround2(Excel.XlLineStyle.xlContinuous);
                        }

                        var rowIndex = 2;

                        foreach (var obj in objects)
                        {
                            var clsids = "";
                            if (obj.CLSIDs != null)
                            {
                                clsids = string.Join(";", obj.CLSIDs);
                            }
                            var maxEntries = Math.Max(obj.LaunchInfo.Count, obj.AccessInfo.Count);

                            for (var i = 0; i < maxEntries; i++)
                            {
                                var launch = (i < obj.LaunchInfo.Count) ? obj.LaunchInfo[i] : null;
                                var access = (i < obj.AccessInfo.Count) ? obj.AccessInfo[i] : null;

                                var row = new object[]
                                {
                                obj.ApplicationID,
                                obj.ApplicationName,
                                obj.RunAs,
                                launch != null ? string.Join(". ", launch.Access) : "",
                                launch != null ? launch.Type : "",
                                launch != null ? launch.Principal : "",
                                launch != null ? launch.SID.ToString() : "",
                                access != null ? string.Join(". ", access.Access) : "",
                                access != null ? access.Type : "",
                                access != null ? access.Principal : "",
                                access != null ? access.SID.ToString() : "",
                                obj.AuthLevel,
                                obj.ImpLevel,
                                clsids
                                };

                                for (var j = 0; j < row.Length; j++)
                                {
                                    workSheet.Cells[rowIndex, j + 1] = row[j];
                                    var cell = (Excel.Range)workSheet.Cells[rowIndex, j + 1];
                                    //cell.BorderAround2(Excel.XlLineStyle.xlContinuous);
                                }
                                rowIndex++;
                            }
                        }

                        workBook.SaveAs(excelFilePath);
                        workBook.Close(false, Missing.Value, Missing.Value);
                        excelApp.Quit();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[-] Do you have Excel? :D");
                        Console.WriteLine(ex.Message);
                    }

                    break;

                default:
                    Console.WriteLine($"[-] Invalid format {outFormat}");
                    break;
            }

            Console.WriteLine("[+] Success");
        }


        private static void ShowBanner()
        {
            var art = @"
                            /\_/\____,          /\     /\
                  ,___/\_/\ \  ~     /            \ _____\     
                  \     ~  \ )   XXX               (_)-(_)
                    XXX     /    /\_/\___,      Checkerv2.0 Collection  
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
            Console.WriteLine("\t\tFrom Michael Zhmaylo (MzHmO)");
            Console.WriteLine("");
        }

        private static void ShowHelp()
        {
            Console.WriteLine("Check.exe");
            Console.WriteLine("Small tool that allow you to find vulnerable DCOM applications");
            Console.WriteLine();
            Console.WriteLine("[OPTIONS]");
            Console.WriteLine("-outfile : output filename");
            Console.WriteLine("-outformat : output format. Accepted 'csv' and 'xlsx'");
            Console.WriteLine("-showtable : show the xlsx table when it gets filled");
            Console.WriteLine("-h/--help : shows this windows");
            Console.WriteLine();
        }
    }
}
