using Microsoft.Office.Interop.Excel;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;


class Excel
{
    public static void AddValue(_Worksheet worksheet, int row, int col, string value)
    {
        worksheet.Cells[row, col] = value;
    }
}

class Parser
{
    const int COM_RIGHTS_EXECUTE = 1;
    const int COM_RIGHTS_EXECUTE_LOCAL = 2;
    const int COM_RIGHTS_EXECUTE_REMOTE = 4;
    const int COM_RIGHTS_ACTIVATE_LOCAL = 8;
    const int COM_RIGHTS_ACTIVATE_REMOTE = 16;

    public class AppInfo
    {
        public string ApplicationID { get; set; }
        public string AppName { get; set; }
        public string Type { get; set; }
        public string Identity { get; set; }
        public int AccessMask { get; set; }
        public List<string> Access { get; set; }
        public string Principal { get; set; }
        public SecurityIdentifier SID { get; set; }
    }

    public static List<string> GetAppIDs()
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

    public static List<AppInfo> GetAppInfoDetails(List<string> applicationIDs, string type)
    {
        var result = new List<AppInfo>();

        foreach (var appID in applicationIDs)
        {
            var keyPath = $@"HKEY_CLASSES_ROOT\AppID\{appID}";
            var permissionValueName = "";
            var appName = (string)Registry.GetValue(keyPath, permissionValueName, null);
            permissionValueName = "RunAs";
            var identity = (string)Registry.GetValue(keyPath, permissionValueName, null);
            if (identity == null)
            {
                identity = "The Launching User";
            }

            permissionValueName = type + "Permission";

            try
            {

                var regPerms = (byte[])Registry.GetValue(keyPath, permissionValueName, null);
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
                        //userName = sid.ToString(); // Unable to map SID to name
                    }

                    if (type == "Launch")
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
                    else // Access
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

                    result.Add(new AppInfo
                    {
                        ApplicationID = appID,
                        Type = ace.AceType.ToString(),
                        Access = access,
                        AccessMask = ace.AccessMask,
                        SID = sid,
                        Principal = userName,
                        AppName = appName,
                        Identity = identity,
                    });
                }
            }
            catch (Exception ex)
            {

            }
        }

        return result;
    }

    private static bool IsGuid(string candidate)
    {
        return Guid.TryParse(candidate, out _);
    }

}

class Program
{
    static void Main(string[] args)
    {
        ShowBanner();

        if (args.Length == 0)
        {
            Console.WriteLine("[?] You didn't specify anything. Look Checker.exe -h");
        }

        var type = "Launch"; // Launch / Access
        var outFormat = "csv"; //csv / xlsx
        var outFile = $"Output";
        var showTable = false;
        foreach (var entry in args.Select((value, index) => new { index, value }))
        {
            var argument = entry.value.ToLower();
            switch (argument)
            {
                case "-launch":
                    type = "Launch";
                    break;

                case "-access":
                    type = "Access";
                    break;

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
        Console.WriteLine($"[+] Result will be in {outFile}, format {outFormat}, info about {type}Permissions");
        var applicationIDs = Parser.GetAppIDs();

        var appInfoDetails = Parser.GetAppInfoDetails(applicationIDs, type);

        switch (outFormat)
        {
            case "csv":
                try
                {
                    var csvFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{outFile}.{outFormat}");

                    using (var writer = new StreamWriter(csvFilePath, false, System.Text.Encoding.UTF8))
                    {
                        var headers = new string[] { "AppId", "AppName", "Identity", "Access", "Type", "Principal", "SID" };
                        writer.WriteLine(string.Join(",", headers));

                        foreach (var app in appInfoDetails)
                        {
                            var row = new string[]
                            {
                            app.ApplicationID,
                            app.AppName,
                            app.Identity,
                            string.Join(". ", app.Access),
                            app.Type,
                            app.Principal,
                            app.SID.ToString()
                            };
                            writer.WriteLine(string.Join(",", row));
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
                    var excelApp = new Application();
                    var workbook = excelApp.Workbooks.Add();
                    var worksheet = workbook.Sheets[1];
                    var row = 2;
                    excelApp.Visible = showTable;

                    Excel.AddValue(worksheet, 1, 1, "AppId");
                    Excel.AddValue(worksheet, 1, 2, "AppName");
                    Excel.AddValue(worksheet, 1, 3, "Identity"); // on whose behalf the com-server is run
                    Excel.AddValue(worksheet, 1, 4, "Access"); // what rights
                    Excel.AddValue(worksheet, 1, 5, "Type"); // accessallowed or denied on Access column
                    Excel.AddValue(worksheet, 1, 6, "Principal"); // who owns the rights
                    Excel.AddValue(worksheet, 1, 7, "SID"); // who owns the rights (Raw SID)

                    foreach (var app in appInfoDetails)
                    {
                        Excel.AddValue(worksheet, row, 1, app.ApplicationID);
                        Excel.AddValue(worksheet, row, 2, app.AppName);
                        Excel.AddValue(worksheet, row, 3, app.Identity);
                        Excel.AddValue(worksheet, row, 4, $"{string.Join(", ", app.Access)}");
                        Excel.AddValue(worksheet, row, 5, app.Type);
                        Excel.AddValue(worksheet, row, 6, app.Principal);
                        Excel.AddValue(worksheet, row, 7, app.SID.ToString());
                        row++;
                    }
                    workbook.SaveAs(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{outFile}.{outFormat}"));

                    workbook.Close(false);
                    Marshal.ReleaseComObject(workbook);
                    excelApp.Quit();
                    Marshal.ReleaseComObject(excelApp);
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
        Console.WriteLine("Check.exe");
        Console.WriteLine("Small tool that allow you to find vulnerable DCOM applications");
        Console.WriteLine();
        Console.WriteLine("[OPTIONS]");
        Console.WriteLine("-launch : enums LaunchPermissions Access Rights. First Step(!)");
        Console.WriteLine("-access : enumc AccessPermissions Access Rights. Second Step(!)");
        Console.WriteLine("-outfile : output filename");
        Console.WriteLine("-outformat : output format. Accepted 'csv' and 'xlsx'");
        Console.WriteLine("-showtable : show the xlsx table when it gets filled");
        Console.WriteLine("-h/--help : shows this windows");
        Console.WriteLine();
    }
}