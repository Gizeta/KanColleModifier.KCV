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

        private List<ModifyInfo> list = new List<ModifyInfo>();
        private string ipAddress = "";
        private string jsonData = "";

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
                foreach (var d in list)
                {
                    if (d.ResourcePath != null)
                        str += "\r\n" + d.ResourcePath;
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
            get { return new RelayCommand(() => list.ForEach(x => x.CleanIECache(ipAddress))); }
        }
        
        #region private method

        private void readModifierData()
        {
            if (File.Exists("魔改.txt"))
            {
                list.Clear();
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
                    var graphKey = path.Substring(st, ed - st);
                    var info = list.FirstOrDefault(x => x.GraphKey == graphKey);
                    if (info == null)
                    {
                        list.Add(new ModifyInfo { GraphKey = graphKey, ResourcePath = path });
                    }
                    else
                    {
                        info.ResourcePath = path;
                    }
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
                    var graphKey = path.Substring(st, ed - st);
                    var info = list.FirstOrDefault(x => x.GraphKey == graphKey);
                    if (info == null)
                    {
                        list.Add(new ModifyInfo { GraphKey = graphKey, ResourceIniPath = path });
                    }
                    else
                    {
                        info.ResourceIniPath = path;
                    }
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

        private void setModifiedData(Session session, ModifyInfo info)
        {
            if (info.GraphKey == "font") return;
            
            string graphStr = Regex.Match(jsonData, @"\{([^{]+?)" + info.GraphKey + @"([^}]+?)\}").Groups[0].Value;
            string sortNo = Regex.Match(graphStr, @"api_sortno"":(\d+)").Groups[1].Value;
            string infoStr = Regex.Match(jsonData, @"\{([^{]+?)api_sortno"":" + sortNo + @"([^}]+?)\}").Groups[0].Value;

            info.GraphVersion = Regex.Match(graphStr, @"api_version"":""(.+?)""").Groups[1].Value;

            if (info.ResourceIniPath == null) return;
            string graphReplaceStr = graphStr;
            string infoReplaceStr = infoStr;

            var str = getIniValue(info.ResourceIniPath, "info", "ship_name");
            if (str != "")
            {
                infoReplaceStr = Regex.Replace(infoReplaceStr, @"api_name"":""(.+?)""", @"api_name"":""" + str + @"""");
            }
            var modList = new string[] { "boko_n", "boko_d",
                "kaisyu_n", "kaisyu_d",
                "kaizo_n", "kaizo_d",
                "map_n", "map_d",
                "ensyuf_n", "ensyuf_d",
                "ensyue_n",
                "battle_n", "battle_d" };
            foreach (var mod in modList)
            {
                str = getIniValue(info.ResourceIniPath, "graph", mod + "_left");
                if (str != "")
                {
                    graphReplaceStr = Regex.Replace(graphReplaceStr, mod + @""":\[([\d-]+),([\d-]+)\]", mod + @""":[" + str + @",$2]");
                }

                str = getIniValue(info.ResourceIniPath, "graph", mod + "_top");
                if (str != "")
                {
                    graphReplaceStr = Regex.Replace(graphReplaceStr, mod + @""":\[([\d-]+),([\d-]+)\]", mod + @"_"":[$1," + str + @"]");
                }
            }

            jsonData = jsonData.Replace(graphStr, graphReplaceStr);
            jsonData = jsonData.Replace(infoStr, infoReplaceStr);
        }

        private string getIniValue(string path, string section, string key)
        {
            var sb = new byte[20];
            int buf = GetPrivateProfileString(section, key, "", sb, 20, path);
            return Encoding.Unicode.GetString(sb, 0, buf * 2).Trim();
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
                jsonData = oSession.GetResponseBodyAsString();

                list.ForEach(x => setModifiedData(oSession, x));

                oSession.utilSetResponseBody(jsonData);
            }
        }

        private void FiddlerApplication_BeforeRequest(Session oSession)
        {
            if (ipAddress == "" && (oSession.fullUrl.Contains("/kcs/resources") || oSession.fullUrl.Contains("/kcsapi/")))
            {
                var ip = oSession.fullUrl.IndexOf("://") + 3;
                ipAddress = oSession.fullUrl.Substring(ip, oSession.fullUrl.IndexOf("/kcs") - ip);
            }
            if (modifierOn && oSession.fullUrl.IndexOf("/kcsapi/api_start2") >= 0)
            {
                oSession.bBufferResponse = true;
                return;
            }
            if (ModifierOn && (oSession.fullUrl.IndexOf("/kcs/resources/swf/ships/") >= 0 || oSession.fullUrl.IndexOf("/kcs/resources/swf/font.swf") >= 0))
            {
                var info = list.First(x => x.HitTest(oSession.fullUrl));
                if (info.GraphKey == "font")
                {
                    info.GraphVersion = Regex.Match(oSession.fullUrl, @"version=([\d.]+)").Groups[1].Value;
                }
                if (info != null)
                    info.ReplaceResponseBody(oSession);
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

        internal class ModifyInfo
        {
            public string GraphKey { get; set; }
            public string GraphVersion { get; set; }
            public string ResourcePath { get; set; }
            public string ResourceIniPath { get; set; }

            public bool HitTest(string url)
            {
                var tmp1 = url.Split('/');
                var tmp2 = tmp1.Last().Split('.');
                if (tmp2.Length >= 1)
                {
                    return tmp2[0] == this.GraphKey;
                }
                return false;
            }

            public void ReplaceResponseBody(Session session)
            {
                session.utilCreateResponseAndBypassServer();
                session.ResponseBody = File.ReadAllBytes(this.ResourcePath);
                session.oResponse.headers.HTTPResponseCode = 200;
                session.oResponse.headers.HTTPResponseStatus = "200 OK";
                session.oResponse.headers["Content-Type"] = "application/x-shockwave-flash";
            }

            public void CleanIECache(string ipAddress)
            {
                if (ipAddress == "" || ResourcePath == null) return;
                if (this.GraphKey == "font")
                {
                    DeleteUrlCacheEntry(string.Format("http://{0}/kcs/resources/swf/font.swf?version={1}", ipAddress, this.GraphVersion));
                }
                else
                {
                    DeleteUrlCacheEntry(string.Format("http://{0}/kcs/resources/swf/ships/{1}.swf?VERSION={2}", ipAddress, this.GraphKey, this.GraphVersion));
                }
            }
        }
    }
}
