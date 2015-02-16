using Fiddler;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace Gizeta.KanColleModifier.KCV
{
    public class ModifierViewModel : INotifyPropertyChanged
    {
        private static ModifierViewModel instance = new ModifierViewModel();

        private Dictionary<string, string> data = new Dictionary<string, string>();
        private Dictionary<string, string> iniData = new Dictionary<string, string>();
        private string ipAddress = "";

        [DllImport(@"wininet.dll", SetLastError = true)]
        public static extern long DeleteUrlCacheEntry(string lpszUrlName);

        [DllImport("kernel32", EntryPoint = "GetPrivateProfileStringW", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileString(string section, string key, string defVal, byte[] retVal, int size, string filePath);

        private ModifierViewModel()
        {
            readModifierData();
            setModifier();
            if (File.Exists("modifier.enable"))
            {
                ModifierOn = true;
            }
        }

        public static ModifierViewModel Instance
        {
            get { return instance; }
        }

        public void Initialize()
        {
            readModifierData();
        }

        public string ModifierButtonContent
        {
            get { return modifierOn ? "关闭魔改" : "开启魔改"; }
        }

        public string ModifierListContent
        {
            get
            {
                string str = "魔改文件：";
                foreach (var d in data)
                {
                    str += "\r\n" + d.Value;
                }
                return str;
            }
        }

        private bool modifierOn = false;
        public bool ModifierOn
        {
            get { return modifierOn; }
            set
            {
                if (modifierOn != value)
                {
                    modifierOn = value;
                    OnPropertyChanged("ModifierButtonContent");
                    if (value)
                    {
                        if (!File.Exists("modifier.enable"))
                        {
                            File.Create("modifier.enable").Close();
                        }
                    }
                    else
                    {
                        if (File.Exists("modifier.enable"))
                        {
                            File.Delete("modifier.enable");
                        }
                    }
                }
            }
        }

        public ICommand SwitchModifierOn
        {
            get { return new RelayCommand(() => ModifierOn = !ModifierOn); }
        }

        public ICommand UpdateModifierList
        {
            get { return new RelayCommand(() => readModifierData() ); }
        }

        public ICommand CleanCache
        {
            get
            {
                return new RelayCommand(() =>
                {
                    foreach (var item in data)
                    {
                        if (item.Key == "font")
                        {
                            DeleteUrlCacheEntry("http://" + ipAddress + "/kcs/resources/swf/font.swf?version=2.3");
                        }
                        else
                        {
                            /* KCV竟没给Graph建Model，先偷个懒 */
                            DeleteUrlCacheEntry("http://" + ipAddress + "/kcs/resources/swf/ships/" + item.Key + ".swf?VERSION=1");
                            DeleteUrlCacheEntry("http://" + ipAddress + "/kcs/resources/swf/ships/" + item.Key + ".swf?VERSION=2");
                            DeleteUrlCacheEntry("http://" + ipAddress + "/kcs/resources/swf/ships/" + item.Key + ".swf?VERSION=3");
                            DeleteUrlCacheEntry("http://" + ipAddress + "/kcs/resources/swf/ships/" + item.Key + ".swf?VERSION=4");
                            DeleteUrlCacheEntry("http://" + ipAddress + "/kcs/resources/swf/ships/" + item.Key + ".swf?VERSION=5");
                            DeleteUrlCacheEntry("http://" + ipAddress + "/kcs/resources/swf/ships/" + item.Key + ".swf?VERSION=6");
                        }
                    }
                });
            }
        }
        
        #region private method

        private void readModifierData()
        {
            if (File.Exists("魔改.txt"))
            {
                data.Clear();
                iniData.Clear();
                var file = File.Open("魔改.txt", FileMode.Open);
                Encoding enc = getEncoding(file);
                file.Close();
                file = File.Open("魔改.txt", FileMode.Open); // 重新打开文件流
                using (var stream = new StreamReader(file, enc))
                {
                    while (!stream.EndOfStream)
                    {
                        var str = stream.ReadLine();
                        if(str.EndsWith(".hack.swf"))
                        {
                            addSwfFile(str);
                        }
                        else if(str.EndsWith(".config.ini"))
                        {
                            addIniFile(str);
                        }
                        else
                        {
                            addDirectory(str);
                        }
                    }
                }
                file.Close();
                OnPropertyChanged("ModifierListContent");
            }
            else
            {
                ModifierOn = false;
            }
        }

        private void addDirectory(string path)
        {
            var folder = new DirectoryInfo(path);
            if (folder.Exists)
            {
                foreach (var file in folder.GetFiles())
                {
                    if(file.FullName.EndsWith(".hack.swf"))
                    {
                        addSwfFile(file.FullName);
                    }
                    else if(file.FullName.EndsWith(".config.ini"))
                    {
                        addIniFile(file.FullName);
                    }
                }
                foreach (var f in folder.GetDirectories())
                {
                    addDirectory(f.FullName);
                }
            }
        }

        private void addSwfFile(string path)
        {
            if (File.Exists(path))
            {
                var st = path.LastIndexOf('\\') + 1;
                var ed = path.LastIndexOf(".hack.swf");
                if (st > 0 && ed > 0)
                {
                    data[path.Substring(st, ed - st)] = path;
                }
            }
        }

        private void addIniFile(string path)
        {
            if (File.Exists(path))
            {
                var st = path.LastIndexOf('\\') + 1;
                var ed = path.LastIndexOf(".config.ini");
                if (st > 0 && ed > 0)
                {
                    iniData[path.Substring(st, ed - st)] = path;
                }
            }
        }

        private Encoding getEncoding(FileStream fs)
        {
            BinaryReader r = new BinaryReader(fs, Encoding.Default);
            byte[] ss = r.ReadBytes(4);
            r.Close();
            if (ss[0] <= 0xEF)
            {
                if (ss[0] == 0xEF && ss[1] == 0xBB & ss[2] == 0xBF)
                {
                    return Encoding.UTF8;
                }
                else if (ss[0] == 0xFE && ss[1] == 0xFF)
                {
                    return Encoding.BigEndianUnicode;
                }
                else if (ss[0] == 0xFF && ss[1] == 0xFE)
                {
                    return Encoding.Unicode;
                }
                else
                {
                    return Encoding.Default;
                }
            }
            else
                return Encoding.Default;
        }

        private void setShipName(Session oSession, string key, string iniPath)
        {
            byte[] sb = new byte[20];
            int buf = GetPrivateProfileString("info", "ship_name", "", sb, 20, iniPath);
            string shipName = Encoding.Unicode.GetString(sb, 0, buf * 2).Trim();
            if (shipName != "")
            {
                var str = oSession.GetResponseBodyAsString();
                var regex = new Regex("api_sortno\":(\\d+),\"api_filename\":\"" + key);
                string sortno = regex.Match(str).Groups[1].Value;
                oSession.utilReplaceRegexInResponse("api_sortno\":" + sortno + "(?'1'.+?)api_name\":\"(.+?)\"", "api_sortno\":" + sortno + "${1}api_name\":\"" + shipName + "\"");
            }
        }

        private void setShipGraph(Session oSession, string key, string iniPath, string item, string idx)
        {
            byte[] sb = new byte[20];
            int buf = GetPrivateProfileString("graph", item + "_" + idx, "", sb, 20, iniPath);
            string value = Encoding.Unicode.GetString(sb, 0, buf * 2).Trim();
            if (value != "")
            {
                if(idx == "left")
                {
                    oSession.utilReplaceRegexInResponse(key + "(?'1'.+?)" + item + "\":\\[(?'2'[\\d-]+),(?'3'[\\d-]+)\\]", key + "${1}" + item + "\":[" + value + ",${3}]");
                }
                else if(idx == "top")
                {
                    oSession.utilReplaceRegexInResponse(key + "(?'1'.+?)" + item + "\":\\[(?'2'[\\d-]+),(?'3'[\\d-]+)\\]", key + "${1}" + item + "\":[${2}," + value + "]");
                }
            }
        }

        #region Fiddler

        private void setModifier()
        {
            FiddlerApplication.BeforeRequest += FiddlerApplication_BeforeRequest;
            FiddlerApplication.BeforeResponse += FiddlerApplication_BeforeResponse;
        }

        private void FiddlerApplication_BeforeResponse(Session oSession)
        {
            if (modifierOn && oSession.fullUrl.IndexOf("/kcsapi/api_start2") >= 0)
            {
                foreach(var key in iniData.Keys)
                {
                    setShipName(oSession, key, iniData[key]);

                    setShipGraph(oSession, key, iniData[key], "boko_n", "left");
                    setShipGraph(oSession, key, iniData[key], "boko_n", "top");
                    setShipGraph(oSession, key, iniData[key], "boko_d", "left");
                    setShipGraph(oSession, key, iniData[key], "boko_d", "top");

                    setShipGraph(oSession, key, iniData[key], "kaisyu_n", "left");
                    setShipGraph(oSession, key, iniData[key], "kaisyu_n", "top");
                    setShipGraph(oSession, key, iniData[key], "kaisyu_d", "left");
                    setShipGraph(oSession, key, iniData[key], "kaisyu_d", "top");

                    setShipGraph(oSession, key, iniData[key], "kaizo_n", "left");
                    setShipGraph(oSession, key, iniData[key], "kaizo_n", "top");
                    setShipGraph(oSession, key, iniData[key], "kaizo_d", "left");
                    setShipGraph(oSession, key, iniData[key], "kaizo_d", "top");

                    setShipGraph(oSession, key, iniData[key], "map_n", "left");
                    setShipGraph(oSession, key, iniData[key], "map_n", "top");
                    setShipGraph(oSession, key, iniData[key], "map_d", "left");
                    setShipGraph(oSession, key, iniData[key], "map_d", "top");

                    setShipGraph(oSession, key, iniData[key], "ensyuf_n", "left");
                    setShipGraph(oSession, key, iniData[key], "ensyuf_n", "top");
                    setShipGraph(oSession, key, iniData[key], "ensyuf_d", "left");
                    setShipGraph(oSession, key, iniData[key], "ensyuf_d", "top");

                    setShipGraph(oSession, key, iniData[key], "ensyue_n", "left");
                    setShipGraph(oSession, key, iniData[key], "ensyue_n", "top");

                    setShipGraph(oSession, key, iniData[key], "battle_n", "left");
                    setShipGraph(oSession, key, iniData[key], "battle_n", "top");
                    setShipGraph(oSession, key, iniData[key], "battle_d", "left");
                    setShipGraph(oSession, key, iniData[key], "battle_d", "top");
                }
            }
        }

        private void FiddlerApplication_BeforeRequest(Session oSession)
        {
            if (ipAddress == "" && (oSession.fullUrl.Contains("/kcs/resources") || oSession.fullUrl.Contains("/kcsapi/")))
            {
                var ip = oSession.fullUrl.IndexOf("://") + 3;
                ipAddress = oSession.fullUrl.Substring(ip, oSession.fullUrl.IndexOf("/kcs") - ip);
            }
            if (ModifierOn && oSession.fullUrl.IndexOf("/kcs/resources/swf/ships/") >= 0)
            {
                var tmp1 = oSession.fullUrl.Split('/');
                var tmp2 = tmp1.Last().Split('.');
                if (tmp2.Length >= 1)
                {
                    if (data.ContainsKey(tmp2[0]))
                    {
                        oSession.utilCreateResponseAndBypassServer();
                        oSession.ResponseBody = File.ReadAllBytes(data[tmp2[0]]);
                        oSession.oResponse.headers.HTTPResponseCode = 200;
                        oSession.oResponse.headers.HTTPResponseStatus = "200 OK";
                        oSession.oResponse.headers["Content-Type"] = "application/x-shockwave-flash";
                    }
                }
            }
            if (ModifierOn && oSession.fullUrl.IndexOf("/kcs/resources/swf/font.swf") >= 0)
            {
                if (data.ContainsKey("font"))
                {
                    System.Windows.MessageBox.Show("font");
                    oSession.utilCreateResponseAndBypassServer();
                    oSession.ResponseBody = File.ReadAllBytes(data["font"]);
                    oSession.oResponse.headers.HTTPResponseCode = 200;
                    oSession.oResponse.headers.HTTPResponseStatus = "200 OK";
                    oSession.oResponse.headers["Content-Type"] = "application/x-shockwave-flash";
                }
            }
            if (ModifierOn && oSession.fullUrl.IndexOf("/kcsapi/api_start2") >= 0)
            {
                oSession.bBufferResponse = true;
            }
        }

        #endregion

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion
    }
}
