using Fiddler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Gizeta.KanColleModifier.KCV
{
    /// <summary>
    /// ModifierView.xaml 的交互逻辑
    /// </summary>
    public partial class ModifierView : UserControl
    {
        private static bool modifierOn = false;
        private static bool hasInitialize = false;
        private static Dictionary<string, string> data = new Dictionary<string, string>();
        private static string ipAddress = "";

        [DllImport(@"wininet.dll", SetLastError = true)]
        public static extern long DeleteUrlCacheEntry(string lpszUrlName);

        public ModifierView()
        {
            InitializeComponent();
            readModifierData();
            showList();
            setModifier();
            toggle(modifierOn);
        }

        public static void Initialize()
        {
            if(File.Exists("modifier.enable"))
            {
                modifierOn = true;
                readModifierData();
                setModifier();
            }
        }

        private static void setModifier()
        {
            if (!hasInitialize)
            {
                hasInitialize = true;
                FiddlerApplication.BeforeRequest += FiddlerApplication_BeforeRequest;
                FiddlerApplication.BeforeResponse += FiddlerApplication_BeforeResponse;
            }
        }

        private static void FiddlerApplication_BeforeResponse(Session oSession)
        {
            if (modifierOn && oSession.fullUrl.IndexOf("/kcs/resources/swf/ships/") >= 0)
            {
                var tmp1 = oSession.fullUrl.Split('/');
                var tmp2 = tmp1.Last().Split('.');
                if (tmp2.Length >= 1)
                {
                    if (data.ContainsKey(tmp2[0]))
                    {
                        oSession.utilDecodeResponse();
                        oSession.ResponseBody = File.ReadAllBytes(data[tmp2[0]]);
                        oSession.oResponse.headers.HTTPResponseCode = 200;
                        oSession.oResponse.headers.HTTPResponseStatus = "200 OK";
                    }
                }
            }
        }

        private static void FiddlerApplication_BeforeRequest(Session oSession)
        {
            if (ipAddress == "" && (oSession.fullUrl.Contains("/kcs/resources") || oSession.fullUrl.Contains("/kcsapi/")))
            {
                var ip = oSession.fullUrl.IndexOf("://") + 3;
                ipAddress = oSession.fullUrl.Substring(ip, oSession.fullUrl.IndexOf("/kcs") - ip);
            }
            if (modifierOn && oSession.fullUrl.IndexOf("/kcs/resources/swf/ships/") >= 0)
            {
                var tmp1 = oSession.fullUrl.Split('/');
                var tmp2 = tmp1.Last().Split('.');
                if (tmp2.Length >= 1)
                {
                    if (data.ContainsKey(tmp2[0]))
                    {
                        oSession.bBufferResponse = true;
                    }
                }
            }
        }

        private static void readModifierData()
        {
            if (File.Exists("魔改.txt"))
            {
                data.Clear();
                var file = File.Open("魔改.txt", FileMode.Open);
                Encoding enc = getEncoding(file);
                file.Close();
                file = File.Open("魔改.txt", FileMode.Open);
                using (var stream = new StreamReader(file, enc))
                {
                    while (!stream.EndOfStream)
                    {
                        var str = stream.ReadLine();
                        var st = str.LastIndexOf('\\') + 1;
                        var ed = str.LastIndexOf(".hack.swf");
                        if (st > 0 && ed > 0)
                        {
                            if (File.Exists(str))
                            {
                                data.Add(str.Substring(st, ed - st), str);
                            }
                        }
                    }
                }
                file.Close();
            }
            else
            {
                modifierOn = false;
            }
        }

        private static Encoding getEncoding(FileStream fs)
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

        private void showList()
        {
            Modifier_List.Text = "魔改文件：";
            foreach (var d in data)
            {
                Modifier_List.Text += "\r\n" + d.Value;
            }
        }

        private void Modifier_Switch_Click(object sender, RoutedEventArgs e)
        {
            toggle(Modifier_Switch.Content.ToString() == "开启魔改");
        }

        private void Modifier_UpdateList_Click(object sender, RoutedEventArgs e)
        {
            readModifierData();
            showList();
        }

        private void toggle(bool on)
        {
            if(on)
            {
                modifierOn = true;
                Modifier_Switch.Content = "关闭魔改";
                if(!File.Exists("modifier.enable"))
                {
                    File.Create("modifier.enable").Close();
                }
            }
            else
            {
                modifierOn = false;
                Modifier_Switch.Content = "开启魔改";
                if (File.Exists("modifier.enable"))
                {
                    File.Delete("modifier.enable");
                }
            }
        }

        private void Modifier_CleanCache_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in data)
            {
                /* KCV竟没给Graph建Model，先偷个懒 */
                DeleteUrlCacheEntry("http://" + ipAddress + "/kcs/resources/swf/ships/" + item.Key + ".swf?VERSION=1");
                DeleteUrlCacheEntry("http://" + ipAddress + "/kcs/resources/swf/ships/" + item.Key + ".swf?VERSION=2");
                DeleteUrlCacheEntry("http://" + ipAddress + "/kcs/resources/swf/ships/" + item.Key + ".swf?VERSION=3");
            }
        }
    }
}
