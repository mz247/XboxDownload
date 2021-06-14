using NetFwTypeLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.AccessControl;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace XboxDownload
{
    public partial class Form1 : Form
    {
        internal static Boolean bServiceFlag = false, bRecordLog = true;
        internal static ConcurrentDictionary<String, Byte[]> dicDomain = new ConcurrentDictionary<String, Byte[]>();
        private readonly DNSProxy dnsProxy;
        private readonly HTTPProxy httpProxy;
        private readonly DataTable dtDomain = new DataTable("Domain");
        private readonly String domainPath = Application.StartupPath + "\\Domain";

        public Form1()
        {
            InitializeComponent();

            //删除过时文件
            if (Directory.Exists(Application.StartupPath + "\\Store"))
            {
                Directory.Delete(Application.StartupPath + "\\Store", true);
            }
            if (File.Exists(Application.StartupPath + "\\Games.json"))
            {
                File.Delete(Application.StartupPath + "\\Games.json");
            }
            if (File.Exists(Application.StartupPath + "\\IP列表(assets1.xboxlive.cn).txt"))
            {
                File.Delete(Application.StartupPath + "\\IP列表(assets1.xboxlive.cn).txt");
            }
            if (File.Exists(Application.StartupPath + "\\使用说明.docx"))
            {
                File.Delete(Application.StartupPath + "\\使用说明.docx");
            }



            dnsProxy = new DNSProxy(this);
            httpProxy = new HTTPProxy(this);

            tbDnsIP.Text = Properties.Settings.Default.DnsIP;
            tbComIP.Text = Properties.Settings.Default.ComIP;
            tbCnIP.Text = Properties.Settings.Default.CnIP;
            tbAppIP.Text = Properties.Settings.Default.AppIP;
            ckbRedirect.Checked = Properties.Settings.Default.Redirect;
            ckbTruncation.Checked = Properties.Settings.Default.Truncation;
            ckbLocalUpload.Checked = Properties.Settings.Default.LocalUpload;
            if (string.IsNullOrEmpty(Properties.Settings.Default.LocalPath))
                Properties.Settings.Default.LocalPath = Application.StartupPath + "\\Upload";
            tbLocalPath.Text = Properties.Settings.Default.LocalPath;
            cbListenIP.SelectedIndex = Properties.Settings.Default.ListenIP;
            ckbDnsService.Checked = Properties.Settings.Default.DnsService;
            ckbHttpService.Checked = Properties.Settings.Default.HttpService;
            if (Environment.OSVersion.Version.Major >= 10)
            {
                ckbMicrosoftStore.Enabled = true;
                ckbMicrosoftStore.Checked = Properties.Settings.Default.MicrosoftStore;
            }

            IPAddress[] ipAddresses = Array.FindAll(Dns.GetHostEntry(string.Empty).AddressList, a => a.AddressFamily == AddressFamily.InterNetwork);
            cbLocalIP.Items.AddRange(ipAddresses);
            if (cbLocalIP.Items.Count >= 1)
            {
                int index = 0;
                if (cbLocalIP.Items.Count >= 1 && !string.IsNullOrEmpty(Properties.Settings.Default.LocalIP))
                {
                    for (int i = 0; i < cbLocalIP.Items.Count; i++)
                    {
                        if (cbLocalIP.Items[i].ToString() == Properties.Settings.Default.LocalIP)
                        {
                            index = i;
                            break;
                        }
                    }
                }
                cbLocalIP.SelectedIndex = index;
            }

            dtDomain.Columns.Add("Enable", typeof(Boolean));
            dtDomain.Columns.Add("Domain", typeof(String));
            dtDomain.Columns.Add("IPv4", typeof(String));
            dtDomain.Columns.Add("Remark", typeof(String));
            if (File.Exists(domainPath))
            {
                try
                {
                    dtDomain.ReadXml(domainPath);
                }
                catch { }
                dtDomain.AcceptChanges();
            }
            dgvHosts.DataSource = dtDomain;
            AddDomain();

            cbSnifferMarket.Items.AddRange((new List<Market>
            {
                //new Market("阿根廷", "AR", "es-AR"),
                //new Market("巴西", "BR", "pt-BR"),
                //new Market("土耳其", "TR", "tr-TR"),
                new Market("港服", "HK", "zh-HK"),
                new Market("台服", "TW", "zh-TW"),
                new Market("日服", "JP", "ja-JP"),
                new Market("美服", "US", "en-US"),
                new Market("国服", "CN", "zh-CN")
            }).ToArray());
            cbSnifferMarket.SelectedIndex = 0;
            pbSniffer.Image = pbSniffer.InitialImage;

            LinkRefreshDrive_LinkClicked(null, null);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            ToolTip toolTip1 = new ToolTip
            {
                AutoPopDelay = 30000,
                IsBalloon = true
            };
            toolTip1.SetToolTip(this.label3, "包括以下com游戏下载域名\nassets1.xboxlive.com\nassets2.xboxlive.com\n7.assets1.xboxlive.com\ndlassets.xboxlive.com\ndlassets2.xboxlive.com\nd1.xboxlive.com\nd2.xboxlive.com\nxvcf1.xboxlive.com\nxvcf2.xboxlive.com");
            toolTip1.SetToolTip(this.label5, "包括以下cn游戏下载域名\nassets1.xboxlive.cn\nassets2.xboxlive.cn\ndlassets.xboxlive.cn\ndlassets2.xboxlive.cn\nd1.xboxlive.cn\nd2.xboxlive.cn");
            toolTip1.SetToolTip(this.label7, "包括以下应用下载域名\ndl.delivery.mp.microsoft.com\ntlu.dl.delivery.mp.microsoft.com");

            if (Properties.Settings.Default.NextUpdate == 0)
            {
                Properties.Settings.Default.NextUpdate = DateTime.Now.AddDays(7).Ticks;
                Properties.Settings.Default.Save();
            }
            else if (DateTime.Compare(DateTime.Now, new DateTime(Properties.Settings.Default.NextUpdate)) >= 0)
            {
                Properties.Settings.Default.NextUpdate = DateTime.Now.AddDays(7).Ticks;
                Properties.Settings.Default.Save();
                ThreadPool.QueueUserWorkItem(delegate { UpdateFile.Start(true); });
            }
        }

        private void TsmiMinimizeTray_Click(object sender, EventArgs e)
        {
            this.Hide();
            this.notifyIcon1.Visible = true;
            this.notifyIcon1.ShowBalloonTip(1000);
        }

        private void TsmiExit_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void NotifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            this.Visible = true;
            this.notifyIcon1.Visible = false;
        }

        private void TsmUpdate_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.NextUpdate = DateTime.Now.AddDays(7).Ticks;
            Properties.Settings.Default.Save();
            ThreadPool.QueueUserWorkItem(delegate {
                XboxDownload.UpdateFile.Start(false);
            });
        }

        private void TsmGuide_Click(object sender, EventArgs e)
        {
            string file = Application.StartupPath + "\\ProductManual.pdf";
            if (File.Exists(file))
            {
                Process.Start(file);
            }
            else
            {
                SocketPackage socketPackage = ClassWeb.HttpRequest(UpdateFile.updateUrl + "/ProductManual.pdf", "GET", null, null, true, false, false, null, null, null, null, null, null, null, 0, null);
                if (string.IsNullOrEmpty(socketPackage.Err) && socketPackage.Headers.StartsWith("HTTP/1.1 200 OK"))
                {
                    using (FileStream fs = File.Create(file))
                    {
                        fs.Write(socketPackage.Buffer, 0, socketPackage.Buffer.Length);
                        fs.Close();
                    }
                    Process.Start(file);
                }
                else MessageBox.Show("文件不存在", "Error", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
        }

        private void TsmAbout_Click(object sender, EventArgs e)
        {
            FormAbout dialog = new FormAbout();
            dialog.ShowDialog();
            dialog.Dispose();
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.M:
                    if (e.Control && e.Alt)
                    {
                        using (FileStream fs = File.Create(Application.ExecutablePath + ".md5"))
                        {
                            Byte[] b = new UTF8Encoding(true).GetBytes(UpdateFile.GetPathMD5(Application.ExecutablePath));
                            fs.Write(b, 0, b.Length);
                            fs.Close();
                        }
                    }
                    break;
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (bServiceFlag) ButStart_Click(null, null);
        }


        //选项卡-服务
        private void ButBrowse_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dlg = new FolderBrowserDialog
            {
                Description = "选择本地上传文件夹",
                SelectedPath = tbLocalPath.Text
            };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                tbLocalPath.Text = dlg.SelectedPath;
            }
        }

        private void ButStart_Click(object sender, EventArgs e)
        {
            if (bServiceFlag)
            {
                bServiceFlag = false;
                if (string.IsNullOrEmpty(Properties.Settings.Default.DnsIP)) tbDnsIP.Clear();
                if (string.IsNullOrEmpty(Properties.Settings.Default.ComIP)) tbComIP.Clear();
                if (string.IsNullOrEmpty(Properties.Settings.Default.CnIP)) tbCnIP.Clear();
                if (string.IsNullOrEmpty(Properties.Settings.Default.AppIP)) tbAppIP.Clear();
                pictureBox1.Image = Properties.Resources.Xbox1;
                butStart.Text = "开始监听";
                tbDnsIP.Enabled = tbComIP.Enabled = tbCnIP.Enabled = tbAppIP.Enabled = ckbRedirect.Enabled = ckbTruncation.Enabled = ckbLocalUpload.Enabled = tbLocalPath.Enabled = butBrowse.Enabled = cbListenIP.Enabled = ckbDnsService.Enabled = ckbHttpService.Enabled = cbLocalIP.Enabled = true;
                if (Environment.OSVersion.Version.Major >= 10)
                {
                    ckbMicrosoftStore.Enabled = true;
                    if (Properties.Settings.Default.MicrosoftStore) ModifyHostsFile(false);
                }
                linkTestDns.Enabled = false;
                dnsProxy.Close();
                httpProxy.Close();
                Program.SystemSleep.RestoreForCurrentThread();
            }
            else
            {
                string sRuleName = this.Text, sRulePath = Application.ExecutablePath;
                bool bRuleAdd = true;
                try
                {
                    INetFwPolicy2 policy2 = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
                    foreach (INetFwRule rule in policy2.Rules)
                    {
                        if (rule.Name == sRuleName)
                        {
                            if (bRuleAdd && rule.ApplicationName == sRulePath && rule.Direction == NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN && rule.Protocol == (int)NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_ANY && rule.Action == NET_FW_ACTION_.NET_FW_ACTION_ALLOW && rule.Profiles == (int)NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_ALL && rule.Enabled)
                                bRuleAdd = false;
                            else
                                policy2.Rules.Remove(rule.Name);
                        }
                        else if (String.Equals(rule.ApplicationName, sRulePath, StringComparison.CurrentCultureIgnoreCase))
                        {
                            policy2.Rules.Remove(rule.Name);
                        }
                    }
                    if (bRuleAdd)
                    {
                        INetFwRule rule = (INetFwRule)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwRule"));
                        rule.Name = sRuleName;
                        rule.ApplicationName = sRulePath;
                        rule.Enabled = true;
                        policy2.Rules.Add(rule);
                    }
                }
                catch { }

                string dnsIP = string.Empty;
                if (!string.IsNullOrEmpty(tbDnsIP.Text.Trim()))
                {
                    if (IPAddress.TryParse(tbDnsIP.Text, out IPAddress ipAddress))
                    {
                        dnsIP = ipAddress.ToString();
                    }
                    else
                    {
                        MessageBox.Show("DNS 服务器 IP 不正确", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        tbDnsIP.Focus();
                        return;
                    }
                }
                string comIP = string.Empty;
                if (!string.IsNullOrEmpty(tbComIP.Text.Trim()))
                {
                    if (IPAddress.TryParse(tbComIP.Text, out IPAddress ipAddress))
                    {
                        comIP = ipAddress.ToString();
                    }
                    else
                    {
                        MessageBox.Show("指定 com 下载域名 IP 不正确", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        tbComIP.Focus();
                        return;
                    }
                }
                string cnIP = string.Empty;
                if (!string.IsNullOrEmpty(tbCnIP.Text.Trim()))
                {
                    if (IPAddress.TryParse(tbCnIP.Text, out IPAddress ipAddress))
                    {
                        cnIP = ipAddress.ToString();
                    }
                    else
                    {
                        MessageBox.Show("指定 cn 下载域名 IP 不正确", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        tbCnIP.Focus();
                        return;
                    }
                }
                string appIP = string.Empty;
                if (!string.IsNullOrEmpty(tbAppIP.Text.Trim()))
                {
                    if (IPAddress.TryParse(tbAppIP.Text, out IPAddress ipAddress))
                    {
                        appIP = ipAddress.ToString();
                    }
                    else
                    {
                        MessageBox.Show("指定应用下载域名 IP 不正确", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        tbAppIP.Focus();
                        return;
                    }
                }
                Properties.Settings.Default.DnsIP = dnsIP;
                Properties.Settings.Default.ComIP = comIP;
                Properties.Settings.Default.CnIP = cnIP;
                Properties.Settings.Default.AppIP = appIP;
                Properties.Settings.Default.Redirect = ckbRedirect.Checked;
                Properties.Settings.Default.Truncation = ckbTruncation.Checked;
                Properties.Settings.Default.LocalUpload = ckbLocalUpload.Checked;
                Properties.Settings.Default.LocalPath = tbLocalPath.Text;
                Properties.Settings.Default.ListenIP = cbListenIP.SelectedIndex;
                Properties.Settings.Default.DnsService = ckbDnsService.Checked;
                Properties.Settings.Default.HttpService = ckbHttpService.Checked;
                Properties.Settings.Default.MicrosoftStore = ckbMicrosoftStore.Checked;
                Properties.Settings.Default.Save();

                string resultInfo = string.Empty;
                using (Process p = new Process())
                {
                    p.StartInfo = new ProcessStartInfo("netstat", @"-aon")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    };
                    p.Start();
                    resultInfo = p.StandardOutput.ReadToEnd();
                    p.Close();
                }
                Match result = Regex.Match(resultInfo, @"(?<protocol>TCP|UDP)\s+(?<ip>[^\s]+):(?<port>80|53)\s+[^\s]+\s+(?<status>[^\s]+\s+)?(?<pid>\d+)", RegexOptions.IgnoreCase);
                if (result.Success)
                {
                    ConcurrentDictionary<Int32, Process> dic = new ConcurrentDictionary<Int32, Process>();
                    StringBuilder sb = new StringBuilder();
                    while (result.Success)
                    {
                        if (Properties.Settings.Default.ListenIP == 0)
                        {
                            if (result.Groups["ip"].Value == Properties.Settings.Default.LocalIP)
                            {
                                string protocol = result.Groups["protocol"].Value;
                                if (protocol == "TCP" && result.Groups["status"].Value == "LISTENING" || protocol == "UDP")
                                {
                                    int port = Convert.ToInt32(result.Groups["port"].Value);
                                    if (port == 53 && Properties.Settings.Default.DnsService || port == 80 && Properties.Settings.Default.HttpService)
                                    {
                                        int pid = int.Parse(result.Groups["pid"].Value);
                                        if (!dic.ContainsKey(pid) && pid != 0)
                                        {
                                            sb.AppendLine(protocol + "\t" + result.Groups["ip"].Value + ":" + port);
                                            if (pid == 4)
                                            {
                                                dic.TryAdd(pid, null);
                                                sb.AppendLine("System");
                                            }
                                            else
                                            {
                                                try
                                                {
                                                    Process procId = Process.GetProcessById(pid);
                                                    dic.TryAdd(pid, procId);
                                                    string filename = procId.MainModule.FileName;
                                                    sb.AppendLine(filename);
                                                }
                                                catch
                                                {
                                                    sb.AppendLine("未知");
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            string protocol = result.Groups["protocol"].Value;
                            int port = Convert.ToInt32(result.Groups["port"].Value);
                            if (port == 53 && Properties.Settings.Default.DnsService || port == 80 && Properties.Settings.Default.HttpService)
                            {
                                int pid = int.Parse(result.Groups["pid"].Value);
                                if (!dic.ContainsKey(pid) && pid != 0)
                                {
                                    sb.AppendLine(protocol + "\t" + result.Groups["ip"].Value + ":" + port);
                                    if (pid == 4)
                                    {
                                        dic.TryAdd(pid, null);
                                        sb.AppendLine("System");
                                    }
                                    else
                                    {
                                        try
                                        {
                                            Process procId = Process.GetProcessById(pid);
                                            dic.TryAdd(pid, procId);
                                            string filename = procId.MainModule.FileName;
                                            sb.AppendLine(filename);
                                        }
                                        catch
                                        {
                                            sb.AppendLine("未知");
                                        }
                                    }
                                }
                            }
                        }
                        result = result.NextMatch();
                    }
                    if (dic.Count >= 1 && MessageBox.Show("检测到以下端口被占用\n" + sb.ToString() + "\n是否尝试强制结束占用端口程序？", "启用服务失败", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                    {
                        foreach (var item in dic)
                        {
                            if (item.Key == 4)
                            {
                                using (Process p = new Process())
                                {
                                    p.StartInfo.FileName = "cmd.exe";
                                    p.StartInfo.UseShellExecute = false;
                                    p.StartInfo.RedirectStandardInput = true;
                                    p.StartInfo.RedirectStandardError = true;
                                    p.StartInfo.CreateNoWindow = true;
                                    p.Start();

                                    p.StandardInput.WriteLine("iisreset /stop");
                                    p.StandardInput.WriteLine("net stop \"SQL Server Reporting Services (MSSQLSERVER)\" /Y");
                                    p.StandardInput.WriteLine("exit");

                                    p.WaitForExit();
                                    p.Close();
                                }
                            }
                            else
                            {
                                try
                                {
                                    item.Value.Kill();
                                }
                                catch { }
                            }
                        }
                    }
                }

                bServiceFlag = true;
                pictureBox1.Image = Properties.Resources.Xbox2;
                tbDnsIP.Enabled = tbComIP.Enabled = tbCnIP.Enabled = tbAppIP.Enabled = ckbRedirect.Enabled = ckbTruncation.Enabled = ckbLocalUpload.Enabled = tbLocalPath.Enabled = butBrowse.Enabled = cbListenIP.Enabled = ckbDnsService.Enabled = ckbHttpService.Enabled = cbLocalIP.Enabled = ckbMicrosoftStore.Enabled = false;
                linkTestDns.Enabled = true;
                butStart.Text = "停止监听";
                Program.SystemSleep.PreventForCurrentThread(false);

                if (Properties.Settings.Default.DnsService)
                {
                    string[] ips = Properties.Settings.Default.LocalIP.Split('.');
                    Byte[] ipByte = new byte[4] { byte.Parse(ips[0]), byte.Parse(ips[1]), byte.Parse(ips[2]), byte.Parse(ips[3]) };
                    dicDomain.AddOrUpdate(Environment.MachineName, ipByte, (oldkey, oldvalue) => ipByte);

                    Thread dnsThread = new Thread(new ThreadStart(dnsProxy.Listen))
                    {
                        IsBackground = true
                    };
                    dnsThread.Start();
                }
                if (Properties.Settings.Default.HttpService)
                {
                    Thread httpThread = new Thread(new ThreadStart(httpProxy.Listen))
                    {
                        IsBackground = true
                    };
                    httpThread.Start();
                }
                if (Properties.Settings.Default.MicrosoftStore)
                {
                    ModifyHostsFile(true);
                }
            }
        }

        private void ModifyHostsFile(bool add)
        {
            string sHostsPath = Environment.SystemDirectory + "\\drivers\\etc\\hosts";
            try
            {
                FileInfo fi = new FileInfo(sHostsPath);
                if (!fi.Exists)
                {
                    StreamWriter sw = fi.CreateText();
                    sw.Close();
                    fi.Refresh();
                }
                if ((fi.Attributes & FileAttributes.ReadOnly) != 0)
                    fi.Attributes = FileAttributes.Normal;
                FileSecurity fSecurity = fi.GetAccessControl();
                fSecurity.AddAccessRule(new FileSystemAccessRule("Administrators", FileSystemRights.FullControl, AccessControlType.Allow));
                fi.SetAccessControl(fSecurity);
                string sHosts = string.Empty;
                using (StreamReader sw = new StreamReader(sHostsPath))
                {
                    sHosts = sw.ReadToEnd();
                }
                sHosts = Regex.Replace(sHosts, @"# Added by Xbox下载助手\r\n(.*\r\n)+# End of Xbox下载助手\r\n", "");
                if (add)
                {
                    string comIP = string.IsNullOrEmpty(Properties.Settings.Default.ComIP) ? Properties.Settings.Default.LocalIP : Properties.Settings.Default.ComIP;
                    if (!Properties.Settings.Default.DnsService && string.IsNullOrEmpty(Properties.Settings.Default.ComIP))
                        tbComIP.Text = comIP;
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("# Added by Xbox下载助手");
                    sb.AppendLine(comIP + " assets1.xboxlive.com");
                    sb.AppendLine(comIP + " assets2.xboxlive.com");
                    sb.AppendLine(comIP + " dlassets.xboxlive.com");
                    sb.AppendLine(comIP + " dlassets2.xboxlive.com");
                    sb.AppendLine(comIP + " d1.xboxlive.com");
                    sb.AppendLine(comIP + " d2.xboxlive.com");
                    sb.AppendLine(comIP + " xvcf1.xboxlive.com");
                    sb.AppendLine(comIP + " xvcf2.xboxlive.com");
                    if (!string.IsNullOrEmpty(Properties.Settings.Default.CnIP))
                    {
                        sb.AppendLine(Properties.Settings.Default.CnIP + " assets1.xboxlive.cn");
                        sb.AppendLine(Properties.Settings.Default.CnIP + " assets2.xboxlive.cn");
                        sb.AppendLine(Properties.Settings.Default.CnIP + " dlassets.xboxlive.cn");
                        sb.AppendLine(Properties.Settings.Default.CnIP + " dlassets2.xboxlive.cn");
                        sb.AppendLine(Properties.Settings.Default.CnIP + " d1.xboxlive.cn");
                        sb.AppendLine(Properties.Settings.Default.CnIP + " d2.xboxlive.cn");
                    }
                    if (!string.IsNullOrEmpty(Properties.Settings.Default.AppIP))
                    {
                        sb.AppendLine(Properties.Settings.Default.AppIP + " dl.delivery.mp.microsoft.com");
                        sb.AppendLine(Properties.Settings.Default.AppIP + " tlu.dl.delivery.mp.microsoft.com");
                    }
                    foreach (var domain in dicDomain)
                    {
                        if (domain.Key == Environment.MachineName)
                            continue;
                        sb.AppendLine(string.Format("{0}.{1}.{2}.{3} {4}", domain.Value[0], domain.Value[1], domain.Value[2], domain.Value[3], domain.Key));
                    }
                    sb.AppendLine("# End of Xbox下载助手");
                    sHosts += sb.ToString();
                }
                using (StreamWriter sw = new StreamWriter(sHostsPath, false))
                {
                    sw.Write(sHosts.Trim() + "\r\n");
                }
                fSecurity.RemoveAccessRule(new FileSystemAccessRule("Administrators", FileSystemRights.FullControl, AccessControlType.Allow));
                fi.SetAccessControl(fSecurity);
            }
            catch (Exception e)
            {
                if (add) MessageBox.Show("加速Win10应用商店失败，错误信息：" + e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LvLog_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (lvLog.SelectedItems.Count == 1)
                {
                    tsmCopy.Visible = true;
                    tsmUseIP.Visible = false;
                    contextMenuStrip1.Show(MousePosition.X, MousePosition.Y);
                }
            }
        }

        private void CbLocalIP_SelectedIndexChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.LocalIP = cbLocalIP.Text;
            Properties.Settings.Default.Save();
        }

        private void LinkTestDns_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            FormDns dialog = new FormDns();
            dialog.ShowDialog();
            dialog.Dispose();
        }

        private void CbRecordLog_CheckedChanged(object sender, EventArgs e)
        {
            bRecordLog = ckbRecordLog.Checked;
        }

        private void LinkClearLog_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            lvLog.Items.Clear();
        }

        //选项卡-测速
        private void DgvIpList_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.Button != MouseButtons.Right) return;
            string host = string.Empty;
            Match result = Regex.Match(groupBox4.Text, @"\((?<host>[^\)]+)\)");
            if (result.Success) host = result.Groups["host"].Value;
            tsmUseIP1.Text = (Regex.IsMatch(host, @"\.xboxlive\.com")) ? "指定 com 下载域名 IP" : "指定 cn 下载域名 IP";
            dgvIpList.ClearSelection();
            DataGridViewRow dgvr = dgvIpList.Rows[e.RowIndex];
            dgvr.Selected = true;
            tsmCopy.Visible = false;
            tsmUseIP.Visible = true;
            contextMenuStrip1.Show(MousePosition.X, MousePosition.Y);
        }

        private void TsmCopy_Click(object sender, EventArgs e)
        {
            Clipboard.SetDataObject(lvLog.SelectedItems[0].SubItems[1].Text);
        }

        private void TsmUseIP_Click(object sender, EventArgs e)
        {
            if (bServiceFlag)
            {
                MessageBox.Show("请先停止监听后再设置。", "使用指定IP", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (dgvIpList.SelectedRows.Count != 1) return;
            DataGridViewRow dgvr = dgvIpList.SelectedRows[0];
            string ip = dgvr.Cells["Col_IP"].Value.ToString();
            ToolStripMenuItem tsmi = sender as ToolStripMenuItem;
            switch (tsmi.Name)
            {
                case "tsmUseIP1":
                    if (tsmUseIP1.Text == "指定 com 下载域名 IP")
                    {
                        tbComIP.Text = ip;
                        tabControl1.SelectedIndex = 0;
                        tbComIP.Focus();
                    }
                    else
                    {
                        tbCnIP.Text = ip;
                        tabControl1.SelectedIndex = 0;
                        tbCnIP.Focus();
                    }
                    break;
                case "tsmUseIP2":
                    tbAppIP.Text = ip;
                    tabControl1.SelectedIndex = 0;
                    tbAppIP.Focus();
                    break;
                case "tsmUseIP3":
                    if (tsmUseIP1.Text == "指定 com 下载域名 IP")
                    {
                        tbComIP.Text = ip;
                        tabControl1.SelectedIndex = 0;
                        tbComIP.Focus();
                    }
                    else
                    {
                        tbCnIP.Text = ip;
                        tabControl1.SelectedIndex = 0;
                        tbCnIP.Focus();
                    }
                    tbAppIP.Text = ip;
                    break;
            }
        }

        private void DgvIpList_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex == -1) return;
            if (e.Button == MouseButtons.Left && dgvIpList.Columns[dgvIpList.CurrentCell.ColumnIndex].Name == "Col_Speed" && dgvIpList.Rows[e.RowIndex].Tag != null)
            {
                string msg = dgvIpList.Rows[e.RowIndex].Tag.ToString().Trim();
                if (!string.IsNullOrEmpty(msg))
                    MessageBox.Show(msg, "Request Headers", MessageBoxButtons.OK, MessageBoxIcon.None);
            }
        }

        private void CkbTelecom_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            string telecom = cb.Text;
            bool isChecked = cb.Checked;
            foreach (DataGridViewRow dgvr in dgvIpList.Rows)
            {
                string ASN = dgvr.Cells["Col_ASN"].Value.ToString();
                if (telecom == "其它")
                {
                    if (!Regex.IsMatch(ASN, @"电信|联通|移动") || ASN.Contains("中华电信"))
                        dgvr.Cells["Col_Check"].Value = isChecked;
                }
                else
                {
                    if (ASN.Contains(telecom) && !ASN.Contains("中华电信"))
                        dgvr.Cells["Col_Check"].Value = isChecked;
                }
            }
        }

        private void LinkTestUrl1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            AddTestUrl("/6/5c4cab3a-4bbe-4de1-84be-68cd4caa9cd5/e4e9e474-8b68-4eeb-89a9-b43a081c6591/1.1.31695.21.398495c0-2751-4819-91a1-053b69901884/Halo5-Guardians_1.1.31695.21_x64_6_8wekyb3d8bbwe");
        }

        private void LinkTestUrl2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            AddTestUrl("/4/cfe9fd9d-753d-474e-b433-d65860d6e091/3d7efd63-f26f-41d1-9f3e-f55dfee68186/1.470.575.0.fbcfcdcf-803b-462a-a7e9-a018b2bef5ee/Sunrise_1.470.575.0_x64__8wekyb3d8bbwe");
        }

        private void LinkTestUrl3_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            AddTestUrl("/Z/b7f5457e-f45c-425d-83b5-ecd508afe699/65307831-308b-4f1b-bb57-8b10e748da82/1.1.945.0.e1aa6466-85c5-440c-bb9e-e211d7757f37/Microsoft.HalifaxBaseGame_1.1.945.0_x64__8wekyb3d8bbwe");
        }

        private void AddTestUrl(string dlFile)
        {
            Match result = Regex.Match(groupBox4.Text, @"\((?<host>[^\)]+)\)");
            if (result.Success) dlFile = "http://" + result.Groups["host"].Value + dlFile;
            tbDlFile.Text = dlFile;
        }

        private void LinkImportIP1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            dgvIpList.Rows.Clear();

            bool update = true;
            string content = string.Empty;
            FileInfo fi = new FileInfo(Application.StartupPath + "\\IP.assets1.xboxlive.cn.txt");
            if (fi.Exists)
            {
                update = DateTime.Compare(DateTime.Now, fi.LastWriteTime.AddHours(24)) >= 0;
                using (StreamReader sr = fi.OpenText())
                {
                    content = sr.ReadToEnd();
                }
            }
            if (update)
            {
                SocketPackage socketPackage = ClassWeb.HttpRequest(UpdateFile.updateUrl + "/IP.assets1.xboxlive.cn.txt", "GET", null, null, true, false, true, null, null, null, null, null, null, null, 0, null);
                if (string.IsNullOrEmpty(socketPackage.Err))
                {
                    content = socketPackage.Html;
                    using (FileStream fs = File.Create(Application.StartupPath + "\\IP.assets1.xboxlive.cn.txt"))
                    {
                        Byte[] bytes = new UTF8Encoding(true).GetBytes(content);
                        fs.Write(bytes, 0, bytes.Length);
                        fs.Close();
                    }
                }
                else if (string.IsNullOrEmpty(content))
                {
                    MessageBox.Show("下载IP出错，请稍候再试。", "在线导入IP", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            List<DataGridViewRow> list = new List<DataGridViewRow>();
            bool telecom1 = ckbTelecom1.Checked;
            bool telecom2 = ckbTelecom2.Checked;
            bool telecom3 = ckbTelecom3.Checked;
            bool telecom4 = ckbTelecom4.Checked;
            Match result = Regex.Match(content, @"(?<IP>\d{0,3}\.\d{0,3}\.\d{0,3}\.\d{0,3})\s*\((?<ASN>[^\)]+)\)|(?<IP>\d{0,3}\.\d{0,3}\.\d{0,3}\.\d{0,3})(?<ASN>.+)\dms|^\s*(?<IP>\d{0,3}\.\d{0,3}\.\d{0,3}\.\d{0,3})\s*$", RegexOptions.Multiline);
            if (result.Success)
            {
                groupBox4.Text = "IP 列表 (assets1.xboxlive.cn)";
                while (result.Success)
                {
                    string ip = result.Groups["IP"].Value;
                    string ASN = result.Groups["ASN"].Value.Trim();

                    DataGridViewRow dgvr = new DataGridViewRow();
                    dgvr.CreateCells(dgvIpList);
                    dgvr.Resizable = DataGridViewTriState.False;
                    if (telecom1 && ASN.Contains("电信") && !ASN.Contains("中华电信") || telecom2 && ASN.Contains("联通") || telecom3 && ASN.Contains("移动") || (telecom4 && (!Regex.IsMatch(ASN, @"电信|联通|移动") || ASN.Contains("中华电信"))))
                        dgvr.Cells[0].Value = true;
                    dgvr.Cells[1].Value = ip;
                    dgvr.Cells[2].Value = ASN;
                    list.Add(dgvr);
                    result = result.NextMatch();
                }
                if (list.Count >= 1)
                {
                    dgvIpList.Rows.AddRange(list.ToArray());
                    dgvIpList.ClearSelection();
                }
            }
        }

        private void LinkImportIP2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            FormImportIP dialog = new FormImportIP();
            dialog.ShowDialog();
            string host = dialog.host;
            DataTable dt = dialog.dt;
            dialog.Dispose();
            if (dt != null && dt.Rows.Count >= 1)
            {
                dgvIpList.Rows.Clear();
                bool telecom1 = ckbTelecom1.Checked;
                bool telecom2 = ckbTelecom2.Checked;
                bool telecom3 = ckbTelecom3.Checked;
                bool telecom4 = ckbTelecom4.Checked;
                List<DataGridViewRow> list = new List<DataGridViewRow>();
                groupBox4.Text = "IP 列表 (" + host + ")";
                foreach (DataRow dr in dt.Select("", "ASN, IpLong"))
                {
                    string ASN = dr["ASN"].ToString();
                    DataGridViewRow dgvr = new DataGridViewRow();
                    dgvr.CreateCells(dgvIpList);
                    dgvr.Resizable = DataGridViewTriState.False;
                    if (telecom1 && ASN.Contains("电信") && !ASN.Contains("中华电信") || telecom2 && ASN.Contains("联通") || telecom3 && ASN.Contains("移动") || (telecom4 && (!Regex.IsMatch(ASN, @"电信|联通|移动") || ASN.Contains("中华电信"))))
                        dgvr.Cells[0].Value = true;
                    dgvr.Cells[1].Value = dr["IP"];
                    dgvr.Cells[2].Value = dr["ASN"];
                    list.Add(dgvr);
                }
                if (list.Count >= 1)
                {
                    dgvIpList.Rows.AddRange(list.ToArray());
                    dgvIpList.ClearSelection();
                }
            }
        }

        private void LinkExportIP_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (dgvIpList.Rows.Count == 0) return;
            string host = string.Empty;
            Match result = Regex.Match(groupBox4.Text, @"\((?<host>[^\)]+)\)");
            if (result.Success) host = result.Groups["host"].Value;
            SaveFileDialog dlg = new SaveFileDialog
            {
                InitialDirectory = Application.StartupPath,
                Title = "导出数据",
                Filter = "文本文件(*.txt)|*.txt",
                FileName = "导出IP(" + host + ")"
            };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(host);
                sb.AppendLine("");
                foreach (DataGridViewRow dgvr in dgvIpList.Rows)
                {
                    if (dgvr.Cells["Col_Speed"].Value != null && !string.IsNullOrEmpty(dgvr.Cells["Col_Speed"].Value.ToString()))
                        sb.AppendLine(dgvr.Cells["Col_IP"].Value + "\t(" + dgvr.Cells["Col_ASN"].Value + ")\t" + dgvr.Cells["Col_Speed"].Value + "Mbps");
                    else
                        sb.AppendLine(dgvr.Cells["Col_IP"].Value + "\t(" + dgvr.Cells["Col_ASN"].Value + ")");
                }
                using (FileStream fs = File.Create(dlg.FileName))
                {
                    Byte[] log = new UTF8Encoding(true).GetBytes(sb.ToString());
                    fs.Write(log, 0, log.Length);
                    fs.Close();
                }
            }
        }

        bool isSpeedTest = false;
        Thread threadSpeedTest = null;
        private void ButSpeedTest_Click(object sender, EventArgs e)
        {
            if (!isSpeedTest)
            {
                if (dgvIpList.Rows.Count == 0)
                {
                    MessageBox.Show("请先导入IP。", "IP列表为空", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                List<DataGridViewRow> ls = new List<DataGridViewRow>();
                foreach (DataGridViewRow dgvr in dgvIpList.Rows)
                {
                    if (Convert.ToBoolean(dgvr.Cells["Col_Check"].Value))
                    {
                        ls.Add(dgvr);
                    }
                }
                if (ls.Count == 0)
                {
                    MessageBox.Show("请勾选需要测试IP。", "选择测试IP", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                int rowIndex = 0;
                foreach (DataGridViewRow dgvr in ls.ToArray())
                {
                    dgvIpList.Rows.Remove(dgvr);
                    dgvIpList.Rows.Insert(rowIndex, dgvr);
                    rowIndex++;
                }
                dgvIpList.Rows[0].Cells[0].Selected = true;

                string dlFile = tbDlFile.Text.Trim();
                if (string.IsNullOrEmpty(dlFile))
                {
                    LinkTestUrl1_LinkClicked(null, null);
                    dlFile = tbDlFile.Text;

                }
                if (!dlFile.StartsWith("http://"))
                {
                    Match result = Regex.Match(groupBox4.Text, @"\((?<host>[^\)]+)\)");
                    if (result.Success)
                    {
                        dlFile = "http://" + result.Groups["host"].Value + dlFile;
                        tbDlFile.Text = dlFile;
                    }
                }
                isSpeedTest = true;
                butSpeedTest.Text = "停止测速";
                ckbTelecom1.Enabled = ckbTelecom2.Enabled = ckbTelecom3.Enabled = ckbTelecom4.Enabled = linkExportIP.Enabled = linkImportIP1.Enabled = linkImportIP2.Enabled = tbDlFile.Enabled = linkTestUrl1.Enabled = linkTestUrl2.Enabled = linkTestUrl3.Enabled = false;
                Col_IP.SortMode = Col_ASN.SortMode = Col_TTL.SortMode = Col_RoundtripTime.SortMode = Col_Speed.SortMode = DataGridViewColumnSortMode.NotSortable;
                Col_Check.ReadOnly = true;
                threadSpeedTest = new Thread(new ThreadStart(() =>
                {
                    SpeedTest(ls, dlFile);
                }))
                {
                    IsBackground = true
                };
                threadSpeedTest.Start();
            }
            else
            {
                if (threadSpeedTest != null && threadSpeedTest.IsAlive) threadSpeedTest.Abort();
                foreach (DataGridViewRow dgvr in dgvIpList.Rows)
                {
                    if (dgvr.Cells["Col_Speed"].Value != null && dgvr.Cells["Col_Speed"].Value.ToString() == "正在测试")
                    {
                        dgvr.Cells["Col_Speed"].Value = null;
                        break;
                    }
                }
                butSpeedTest.Text = "开始测速";
                ckbTelecom1.Enabled = ckbTelecom2.Enabled = ckbTelecom3.Enabled = ckbTelecom4.Enabled = linkExportIP.Enabled = linkImportIP1.Enabled = linkImportIP2.Enabled = tbDlFile.Enabled = linkTestUrl1.Enabled = linkTestUrl2.Enabled = linkTestUrl3.Enabled = true;
                Col_IP.SortMode = Col_ASN.SortMode = Col_TTL.SortMode = Col_RoundtripTime.SortMode = Col_Speed.SortMode = DataGridViewColumnSortMode.Automatic;
                Col_Check.ReadOnly = false;
                isSpeedTest = false;
            }
        }

        private void SpeedTest(List<DataGridViewRow> ls, string dlFile)
        {
            string[] headers = new string[] { "Range: bytes=0-209715199" }; //200M
            //string[] headers = new string[] { "Range: bytes=0-1048575" }; //1M
            Stopwatch sw = new Stopwatch();
            foreach (DataGridViewRow dgvr in ls)
            {
                string ip = dgvr.Cells["Col_IP"].Value.ToString();
                dgvr.Cells["Col_TTL"].Value = null;
                dgvr.Cells["Col_RoundtripTime"].Value = null;
                dgvr.Cells["Col_Speed"].Value = "正在测试";
                dgvr.Cells["Col_Speed"].Style.ForeColor = Color.Empty;
                dgvr.Tag = null;

                using (Ping p1 = new Ping())
                {
                    try
                    {
                        PingReply reply = p1.Send(ip);
                        if (reply.Status == IPStatus.Success)
                        {
                            dgvr.Cells["Col_TTL"].Value = Convert.ToInt32(reply.Options.Ttl);
                            dgvr.Cells["Col_RoundtripTime"].Value = Convert.ToInt32(reply.RoundtripTime);
                        }
                    }
                    catch { }
                }
                sw.Restart();
                SocketPackage socketPackage = ClassWeb.HttpRequest(dlFile, "GET", null, null, true, false, false, null, null, headers, null, null, null, null, 0, null, 15000, 60000, 1, ip, true);
                sw.Stop();
                dgvr.Tag = string.IsNullOrEmpty(socketPackage.Err) ? socketPackage.Headers : socketPackage.Err;
                if (socketPackage.Headers.StartsWith("HTTP/1.1 206"))
                {
                    dgvr.Cells["Col_Speed"].Value = Math.Round((double)(socketPackage.Buffer.Length) / sw.ElapsedMilliseconds * 1000 * 8 / 1024 / 1024, 2, MidpointRounding.AwayFromZero);
                }
                else
                {
                    dgvr.Cells["Col_Speed"].Value = (double)0;
                    dgvr.Cells["Col_Speed"].Style.ForeColor = Color.Red;
                }
            }
            this.Invoke(new Action(() =>
            {
                butSpeedTest.Text = "开始测速";
                ckbTelecom1.Enabled = ckbTelecom2.Enabled = ckbTelecom3.Enabled = ckbTelecom4.Enabled = linkExportIP.Enabled = linkImportIP1.Enabled = linkImportIP2.Enabled = tbDlFile.Enabled = linkTestUrl1.Enabled = linkTestUrl2.Enabled = linkTestUrl3.Enabled = true;
                Col_IP.SortMode = Col_ASN.SortMode = Col_Speed.SortMode = Col_TTL.SortMode = Col_RoundtripTime.SortMode = DataGridViewColumnSortMode.Automatic;
                Col_Check.ReadOnly = false;
            }));
            isSpeedTest = false;
        }


        //选项卡-域名
        private void DgvHosts_DefaultValuesNeeded(object sender, DataGridViewRowEventArgs e)
        {
            e.Row.Cells["Col_Enable"].Value = true;
        }

        private void LinkXbox360DomainName_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string[] dn = new string[] { "download.xbox.com", "download.xbox.com.edgesuite.net", "xbox-ecn102.vo.msecnd.net" };
            foreach (string domain in dn)
            {
                DataRow[] rows = dtDomain.Select("Domain='" + domain + "'");
                if (rows.Length >= 1)
                {
                    rows[0]["Enable"] = true;
                    rows[0]["IPv4"] = Properties.Settings.Default.LocalIP;
                    rows[0]["Remark"] = "Xbox360主机下载域名";
                }
                else
                {
                    DataRow dr = dtDomain.NewRow();
                    dr["Enable"] = true;
                    dr["Domain"] = domain;
                    dr["IPv4"] = Properties.Settings.Default.LocalIP;
                    dr["Remark"] = "Xbox360主机下载域名";
                    dtDomain.Rows.Add(dr);
                }
            }
        }

        private void ButDomainSave_Click(object sender, EventArgs e)
        {
            dtDomain.AcceptChanges();
            if (dtDomain.Rows.Count >= 1)
                dtDomain.WriteXml(domainPath);
            else if (File.Exists(domainPath))
                File.Delete(domainPath);
            AddDomain();

            if (bServiceFlag && Properties.Settings.Default.MicrosoftStore)
            {
                ModifyHostsFile(true);
            }
        }

        private void ButDomainReset_Click(object sender, EventArgs e)
        {
            dtDomain.RejectChanges();
        }

        private void LinkDomainClear_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            for (int i = dgvHosts.Rows.Count - 2; i >= 0; i--)
            {
                dgvHosts.Rows.RemoveAt(i);
            }
        }

        private void AddDomain()
        {
            dicDomain.Clear();
            foreach (DataRow dr in dtDomain.Rows)
            {
                string domain = dr["Domain"].ToString().Trim().ToLower();
                if (Convert.ToBoolean(dr["Enable"]) && !string.IsNullOrEmpty(domain))
                {
                    if (IPAddress.TryParse(dr["IPv4"].ToString(), out IPAddress ip))
                    {
                        string[] ips = ip.ToString().Split('.');
                        Byte[] ipByte = new byte[4] { byte.Parse(ips[0]), byte.Parse(ips[1]), byte.Parse(ips[2]), byte.Parse(ips[3]) };
                        dicDomain.TryAdd(domain, ipByte);
                    }
                }
            }
        }

        //选项卡-硬盘
        private void ButScan_Click(object sender, EventArgs e)
        {
            dgvDevice.Rows.Clear();
            butEnabelPc.Enabled = butEnabelXbox.Enabled = false;
            List<DataGridViewRow> list = new List<DataGridViewRow>();

            ManagementClass mc = new ManagementClass("Win32_DiskDrive");
            ManagementObjectCollection moc = mc.GetInstances();
            foreach (ManagementObject mo in moc)
            {
                string sDeviceID = mo.Properties["DeviceID"].Value.ToString();
                string mbr = ClassMbr.ByteToHexString(ClassMbr.ReadMBR(sDeviceID));
                if (string.Equals(mbr.Substring(0, 892), ClassMbr.MBR))
                {
                    string mode = mbr.Substring(1020);
                    DataGridViewRow dgvr = new DataGridViewRow();
                    dgvr.CreateCells(dgvDevice);
                    dgvr.Height = 22;
                    dgvr.Resizable = DataGridViewTriState.False;
                    dgvr.Tag = mode;
                    dgvr.Cells[0].Value = sDeviceID;
                    dgvr.Cells[1].Value = mo.Properties["Model"].Value;
                    dgvr.Cells[2].Value = mo.Properties["InterfaceType"].Value;
                    dgvr.Cells[3].Value = ClassMbr.ConvertBytes(Convert.ToUInt64(mo.Properties["Size"].Value));
                    if (mode == "99CC")
                        dgvr.Cells[4].Value = "Xbox 模式";
                    else if (mode == "55AA")
                        dgvr.Cells[4].Value = "PC 模式";
                    list.Add(dgvr);
                }
            }
            if (list.Count >= 1)
            {
                dgvDevice.Rows.AddRange(list.ToArray());
                dgvDevice.ClearSelection();
            }
        }

        private void DgvDevice_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == -1) return;
            string mode = dgvDevice.Rows[e.RowIndex].Tag.ToString();
            if (mode == "99CC")
            {
                butEnabelPc.Enabled = true;
                butEnabelXbox.Enabled = false;
            }
            else if (mode == "55AA")
            {
                butEnabelPc.Enabled = false;
                butEnabelXbox.Enabled = true;
            }
        }

        private void ButEnabelPc_Click(object sender, EventArgs e)
        {
            if (dgvDevice.SelectedRows.Count != 1) return;
            if (Environment.OSVersion.Version.Major < 10)
            {
                MessageBox.Show("低于Win10操作系统转换后会蓝屏，请升级操作系统。", "操作系统版本过低", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            string sDeviceID = dgvDevice.SelectedRows[0].Cells["Col_DeviceID"].Value.ToString();
            string mode = dgvDevice.SelectedRows[0].Tag.ToString();
            string mbr = ClassMbr.ByteToHexString(ClassMbr.ReadMBR(sDeviceID));
            if (mode == "99CC" && mbr.Substring(0, 892) == ClassMbr.MBR && mbr.Substring(1020) == mode)
            {
                string newMBR = mbr.Substring(0, 1020) + "55AA";
                if (ClassMbr.WriteMBR(sDeviceID, ClassMbr.HexToByte(newMBR)))
                {
                    dgvDevice.SelectedRows[0].Tag = "55AA";
                    dgvDevice.SelectedRows[0].Cells["Col_Mode"].Value = "PC 模式";
                    dgvDevice.ClearSelection();
                    butEnabelPc.Enabled = false;
                    using (Process p = new Process())
                    {
                        p.StartInfo.FileName = "diskpart.exe";
                        p.StartInfo.RedirectStandardInput = true;
                        p.StartInfo.CreateNoWindow = true;
                        p.StartInfo.UseShellExecute = false;
                        p.Start();
                        p.StandardInput.WriteLine("rescan");
                        p.StandardInput.WriteLine("exit");
                        p.Close();
                    }
                    MessageBox.Show("成功切换PC模式。", "切换PC模式", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                }
            }
        }

        private void ButEnabelXbox_Click(object sender, EventArgs e)
        {
            if (dgvDevice.SelectedRows.Count != 1) return;
            string sDeviceID = dgvDevice.SelectedRows[0].Cells["Col_DeviceID"].Value.ToString();
            string mode = dgvDevice.SelectedRows[0].Tag.ToString();
            string mbr = ClassMbr.ByteToHexString(ClassMbr.ReadMBR(sDeviceID));
            if (mode == "55AA" && mbr.Substring(0, 892) == ClassMbr.MBR && mbr.Substring(1020) == mode)
            {
                string newMBR = mbr.Substring(0, 1020) + "99CC";
                if (ClassMbr.WriteMBR(sDeviceID, ClassMbr.HexToByte(newMBR)))
                {
                    dgvDevice.SelectedRows[0].Tag = "99CC";
                    dgvDevice.SelectedRows[0].Cells["Col_Mode"].Value = "Xbox 模式";
                    dgvDevice.ClearSelection();
                    butEnabelXbox.Enabled = false;
                    MessageBox.Show("成功切换Xbox模式。", "切换Xbox模式", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                }
            }
        }

        private void Tb_Enter(object sender, EventArgs e)
        {
            BeginInvoke((Action)delegate
            {
                (sender as TextBox).SelectAll();
            });
        }

        private void ButDownload_Click(object sender, EventArgs e)
        {
            string url = tbDownloadUrl.Text.Trim();
            if (string.IsNullOrEmpty(url)) return;
            if (!Regex.IsMatch(url, @"^https?\:\/\/"))
            {
                if (!url.StartsWith("/")) url = "/" + url;
                url = "http://assets1.xboxlive.cn" + url;
                tbDownloadUrl.Text = url;
            }

            tbFilePath.Text = string.Empty;
            byte[] bFileBuffer = null;
            SocketPackage socketPackage = ClassWeb.HttpRequest(url, "GET", null, null, true, false, false, null, null, new string[] { "Range: bytes=0-4095" }, null, null, null, null, 0, null);
            if (!string.IsNullOrEmpty(socketPackage.Err))
            {
                MessageBox.Show("下载失败，错误信息：" + socketPackage.Err, "下载失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                bFileBuffer = socketPackage.Buffer;
            }
            Analysis(bFileBuffer);
        }

        private void ButOpenFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Title = "Open an Xbox Package"
            };
            if (ofd.ShowDialog() != DialogResult.OK)
                return;

            string sFilePath = ofd.FileName;
            tbDownloadUrl.Text = "";
            tbFilePath.Text = sFilePath;

            FileStream fs = null;
            try
            {
                fs = new FileStream(sFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            if (fs != null)
            {
                int len = fs.Length >= 49152 ? 49152 : (int)fs.Length;
                byte[] bFileBuffer = new byte[len];
                fs.Read(bFileBuffer, 0, len);
                fs.Close();
                Analysis(bFileBuffer);
            }
        }

        private void Analysis(byte[] bFileBuffer)
        {
            tbContentId.Text = tbProductID.Text = tbBuildID.Text = tbFileTimeCreated.Text = tbDriveSize.Text = tbPackageVersion.Text = string.Empty;
            linkCopyContentID.Enabled = linkRename.Enabled = false;
            if (bFileBuffer.Length >= 4096)
            {
                using (MemoryStream ms = new MemoryStream(bFileBuffer))
                {
                    BinaryReader br = null;
                    try
                    {
                        br = new BinaryReader(ms);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    if (br != null)
                    {
                        br.BaseStream.Position = 0x200;
                        if (Encoding.Default.GetString(br.ReadBytes(0x8)) == "msft-xvd")
                        {
                            br.BaseStream.Position = 0x210;
                            tbFileTimeCreated.Text = DateTime.FromFileTime(BitConverter.ToInt64(br.ReadBytes(0x8), 0)).ToString();

                            br.BaseStream.Position = 0x218;
                            tbDriveSize.Text = ClassMbr.ConvertBytes(BitConverter.ToUInt64(br.ReadBytes(0x8), 0)).ToString();

                            br.BaseStream.Position = 0x220;
                            tbContentId.Text = (new Guid(br.ReadBytes(0x10))).ToString();

                            br.BaseStream.Position = 0x39C;
                            tbProductID.Text = (new Guid(br.ReadBytes(0x10))).ToString();

                            br.BaseStream.Position = 0x3AC;
                            tbBuildID.Text = (new Guid(br.ReadBytes(0x10))).ToString();

                            br.BaseStream.Position = 0x3BC;
                            ushort PackageVersion1 = BitConverter.ToUInt16(br.ReadBytes(0x2), 0);
                            br.BaseStream.Position = 0x3BE;
                            ushort PackageVersion2 = BitConverter.ToUInt16(br.ReadBytes(0x2), 0);
                            br.BaseStream.Position = 0x3C0;
                            ushort PackageVersion3 = BitConverter.ToUInt16(br.ReadBytes(0x2), 0);
                            br.BaseStream.Position = 0x3C2;
                            ushort PackageVersion4 = BitConverter.ToUInt16(br.ReadBytes(0x2), 0);
                            tbPackageVersion.Text = PackageVersion4 + "." + PackageVersion3 + "." + PackageVersion2 + "." + PackageVersion1;
                            linkCopyContentID.Enabled = true;
                            if (!string.IsNullOrEmpty(tbFilePath.Text) && Path.GetFileName(tbFilePath.Text).ToUpperInvariant() != tbContentId.Text.ToUpperInvariant()) linkRename.Enabled = true;
                        }
                        else
                        {
                            MessageBox.Show("不是有效文件", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        br.Close();
                    }
                }
            }
            else
            {
                MessageBox.Show("不是有效文件", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LinkCopyContentID_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string sContentID = tbContentId.Text;
            if (!string.IsNullOrEmpty(sContentID))
            {
                Clipboard.SetDataObject(sContentID.ToUpper());
            }
        }

        private void LinkRename_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (MessageBox.Show(string.Format("是否确认重命名本地文件？\n\n修改前文件名：{0}\n修改后文件名：{1}", Path.GetFileName(tbFilePath.Text), tbContentId.Text.ToUpperInvariant()), "重命名本地文件", MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            {
                FileInfo fi = new FileInfo(tbFilePath.Text);
                try
                {
                    fi.MoveTo(Path.GetDirectoryName(tbFilePath.Text) + "\\" + tbContentId.Text.ToUpperInvariant());
                }
                catch (Exception ex)
                {
                    MessageBox.Show("重命名本地文件失败，错误信息：" + ex.Message, "重命名本地文件", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                linkRename.Enabled = false;
            }
        }

        //选项卡-下载
        private void LinkSniffer1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            tbSnifferUrl.Text = "https://www.microsoft.com/store/productId/BRRC2BP0G9P0";
        }

        private void LinkSniffer2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            tbSnifferUrl.Text = "https://www.microsoft.com/zh-hk/p/forza-horizon-5/9nnx1vvr3knq?activetab=pivot:overviewtab";
        }

        private void LinkSniffer3_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            tbSnifferUrl.Text = "C2KDNLT2H7DM";
        }

        private void LinkSniffer4_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            FormSniffer dialog = new FormSniffer();
            dialog.ShowDialog();
            dialog.Dispose();
            if (!string.IsNullOrEmpty(dialog.productid))
            {
                tbSnifferUrl.Text = "https://www.microsoft.com/store/productId/" + dialog.productid;
                cbSnifferMarket.SelectedIndex = cbSnifferMarket.Items.Count - 1;
            }
        }

        private void ButSniffer_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(tbSnifferUrl.Text)) return;
            Match result = Regex.Match(tbSnifferUrl.Text, @"/(?<productId>[a-zA-Z0-9]{12})$|/(?<productId>[a-zA-Z0-9]{12})\?|^(?<productId>[a-zA-Z0-9]{12})$");
            if (result.Success)
            {
                pbSniffer.Image = pbSniffer.InitialImage;
                tbSnifferTitle.Clear();
                tbSnifferPrice.Clear();
                tbSnifferDescription.Clear();
                lvSniffer.Items.Clear();
                butSniffer.Enabled = false;
                Market market = (Market)cbSnifferMarket.SelectedItem;
                string productId = result.Groups["productId"].Value.ToUpperInvariant();
                ThreadPool.QueueUserWorkItem(delegate { Sniffer(market, productId); });
            }
            else
            {
                MessageBox.Show("无效 URL/ProductId", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Sniffer(Market market, string productId)
        {
            string url = "https://displaycatalog.mp.microsoft.com/v7.0/products/" + productId + "/?fieldsTemplate=InstallAgent&market=" + market.code + "&languages=" + market.lang + ",neutral";
            SocketPackage socketPackage = ClassWeb.HttpRequest(url, "GET", null, null, true, false, true, null, "application/json", new string[] { "MS-CV: q5E5dXQLOUmztXqT.44.1.3" }, "Install Service", null, null, null, 0, null);
            if (string.IsNullOrEmpty(socketPackage.Err))
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                var json = js.Deserialize<ClassSniffer.Sniffer>(socketPackage.Html);
                if (json != null && json.Product != null && json.Product.LocalizedProperties != null)
                {
                    string title = string.Empty, description = string.Empty;
                    List<ListViewItem> ls = new List<ListViewItem>();
                    var localizedPropertie = json.Product.LocalizedProperties;
                    if (localizedPropertie.Count >= 1)
                    {
                        title = localizedPropertie[0].ProductTitle;
                        description = localizedPropertie[0].ProductDescription;
                        string imageUri = string.Empty;
                        int imageMax = 0;
                        foreach (var image in localizedPropertie[0].Images)
                        {
                            if (image.Width == image.Height && image.Width > imageMax)
                            {
                                imageMax = image.Width;
                                imageUri = image.Uri;
                            }
                        }
                        if (!string.IsNullOrEmpty(imageUri))
                        {
                            if (imageUri.StartsWith("//")) imageUri = "http:" + imageUri;
                            pbSniffer.Load(imageUri);
                        }
                    }
                    foreach (var displaySkuAvailabilitie in json.Product.DisplaySkuAvailabilities)
                    {
                        if (displaySkuAvailabilitie.Sku.SkuType == "full" && displaySkuAvailabilitie.Sku.Properties.Packages != null)
                        {
                            foreach (var Packages in displaySkuAvailabilitie.Sku.Properties.Packages)
                            {
                                List<ClassSniffer.PlatformDependencies> platformDependencie = Packages.PlatformDependencies;
                                List<ClassSniffer.PackageDownloadUris> packageDownloadUri = Packages.PackageDownloadUris;
                                if (platformDependencie != null && packageDownloadUri != null && Packages.PlatformDependencies.Count >= 1 && packageDownloadUri.Count >= 1)
                                {
                                    ListViewItem listViewItem = null;
                                    switch (platformDependencie[0].PlatformName)
                                    {
                                        case "Windows.Xbox":
                                            if (packageDownloadUri[0].Uri.EndsWith("_xs.xvc"))
                                                listViewItem = new ListViewItem(new string[] { "Xbox Series X/S", market.name, ClassMbr.ConvertBytes(Packages.MaxDownloadSizeInBytes), packageDownloadUri[0].Uri });
                                            else
                                                listViewItem = new ListViewItem(new string[] { "Xbox One 系列", market.name, ClassMbr.ConvertBytes(Packages.MaxDownloadSizeInBytes), packageDownloadUri[0].Uri });
                                            break;
                                        case "Windows.Desktop":
                                            listViewItem = new ListViewItem(new string[] { "Win10 微软商店", market.name, ClassMbr.ConvertBytes(Packages.MaxDownloadSizeInBytes), packageDownloadUri[0].Uri });
                                            break;
                                    }
                                    if (listViewItem != null) ls.Add(listViewItem);
                                }
                            }
                            break;
                        }
                    }
                    string CurrencyCode = json.Product.DisplaySkuAvailabilities[0].Availabilities[0].OrderManagementData.Price.CurrencyCode;
                    double MSRP = json.Product.DisplaySkuAvailabilities[0].Availabilities[0].OrderManagementData.Price.MSRP;
                    double ListPrice = json.Product.DisplaySkuAvailabilities[0].Availabilities[0].OrderManagementData.Price.ListPrice;
                    double ListPrice_1 = json.Product.DisplaySkuAvailabilities[0].Availabilities.Count >= 2 ? json.Product.DisplaySkuAvailabilities[0].Availabilities[1].OrderManagementData.Price.ListPrice : 0;
                    double WholesalePrice = json.Product.DisplaySkuAvailabilities[0].Availabilities[0].OrderManagementData.Price.WholesalePrice;
                    this.Invoke(new Action(() =>
                    {
                        tbSnifferTitle.Text = title;
                        if (MSRP > 0)
                        {
                            StringBuilder sb = new StringBuilder();
                            sb.Append(string.Format("币种: {0}, 建议零售价: {1}", CurrencyCode, String.Format("{0:N}", MSRP)));
                            if (ListPrice != MSRP) sb.Append(string.Format(", 折扣{0}%: {1}", Math.Round(ListPrice / MSRP * 100, 0, MidpointRounding.AwayFromZero), String.Format("{0:N}", ListPrice)));
                            if (ListPrice_1 > 0) sb.Append(string.Format(", 金会员折扣{0}%: {1}", Math.Round(ListPrice_1 / MSRP * 100, 0, MidpointRounding.AwayFromZero), String.Format("{0:N}", ListPrice_1)));
                            if (WholesalePrice > 0) sb.Append(string.Format(", 批发价: {0}", String.Format("{0:N}", WholesalePrice)));
                            tbSnifferPrice.Text = sb.ToString();
                        }
                        tbSnifferDescription.Text = description;
                        if (ls.Count >= 1) lvSniffer.Items.AddRange(ls.ToArray());
                        butSniffer.Enabled = true;
                    }));
                }
                else
                {
                    this.Invoke(new Action(() =>
                    {
                        MessageBox.Show("无效 URL/ProductId", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        butSniffer.Enabled = true;
                    }));
                }
            }
        }

        private void LvSniffer_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (lvSniffer.SelectedItems.Count == 1)
                {
                    tsmCopyUrl2.Enabled = Regex.IsMatch(lvSniffer.SelectedItems[0].SubItems[3].Text, @"\.xboxlive\.com");
                    contextMenuStrip2.Show(MousePosition.X, MousePosition.Y);
                }
            }
        }

        private void TsmCopyUrl1_Click(object sender, EventArgs e)
        {
            Clipboard.SetDataObject(lvSniffer.SelectedItems[0].SubItems[3].Text);
        }

        private void TsmCopyUrl2_Click(object sender, EventArgs e)
        {
            string url = lvSniffer.SelectedItems[0].SubItems[3].Text;
            url = url.Replace(".xboxlive.com", ".xboxlive.cn");
            Clipboard.SetDataObject(url);
        }

        //选项卡-工具
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x219)
            {
                switch (m.WParam.ToInt32())
                {
                    case 0x8000: //U盘插入
                    case 0x8004: //U盘卸载
                        LinkRefreshDrive_LinkClicked(null, null);
                        break;
                    default:
                        break;
                }
            }
            base.WndProc(ref m);
        }

        private void LinkRefreshDrive_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            cbDrive.Items.Clear();
            DriveInfo[] driverList = Array.FindAll(DriveInfo.GetDrives(), a => a.DriveType == DriveType.Removable);
            if (driverList.Length >= 1)
            {
                cbDrive.Items.AddRange(driverList);
                cbDrive.SelectedIndex = 0;
            }
            else
            {
                labelStatusDrive.Text = "当前U盘状态：没有找到U盘";
            }
        }

        private void CbDrive_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbDrive.Items.Count >= 1)
            {
                string driverName = cbDrive.Text;
                DriveInfo driveInfo = new DriveInfo(driverName);
                if (driveInfo.DriveType == DriveType.Removable)
                {
                    if (driveInfo.IsReady && driveInfo.DriveFormat == "NTFS")
                    {
                        List<string> listStatus = new List<string>();
                        if (File.Exists(driverName + "$ConsoleGen8Lock"))
                            listStatus.Add(rbXboxOne.Text + " 回国");
                        else if (File.Exists(driverName + "$ConsoleGen8"))
                            listStatus.Add(rbXboxOne.Text + " 出国");
                        if (File.Exists(driverName + "$ConsoleGen9Lock"))
                            listStatus.Add(rbXboxSeries.Text + " 回国");
                        else if (File.Exists(driverName + "$ConsoleGen9"))
                            listStatus.Add(rbXboxSeries.Text + " 出国");
                        if (listStatus.Count >= 1)
                            labelStatusDrive.Text = "当前U盘状态：" + string.Join(", ", listStatus.ToArray());
                        else
                            labelStatusDrive.Text = "当前U盘状态：未转换";
                    }
                    else
                    {
                        labelStatusDrive.Text = "当前U盘状态：不是NTFS格式";
                    }
                }
            }
        }

        private void ButConsoleRegionUnlock_Click(object sender, EventArgs e)
        {
            ConsoleRegion(true);
        }

        private void ButConsoleRegionLock_Click(object sender, EventArgs e)
        {
            ConsoleRegion(false);
        }

        private void ConsoleRegion(bool unlock)
        {
            if (cbDrive.Items.Count == 0)
            {
                MessageBox.Show("请插入U盘。", "没有找到U盘", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            labelStatusDrive.Text = "当前U盘状态：制作中，请稍候...";
            string driverName = cbDrive.Text;
            DriveInfo driveInfo = new DriveInfo(driverName);
            if (driveInfo.DriveType == DriveType.Removable)
            {
                if (!driveInfo.IsReady || driveInfo.DriveFormat != "NTFS")
                {
                    string show, caption, cmd;
                    if (driveInfo.IsReady && driveInfo.DriveFormat == "FAT32")
                    {
                        show = "当前U盘格式 " + driveInfo.DriveFormat + "，是否把U盘转换为 NTFS 格式？\n\n注意，如果U盘有重要数据请先备份!\n\n当前U盘位置： " + driverName + "，容量：" + ClassMbr.ConvertBytes(Convert.ToUInt64(driveInfo.TotalSize)) + "\n取消转换请按\"否(N)\"";
                        caption = "转换U盘格式";
                        cmd = "convert " + Regex.Replace(driverName, @"\\$", "") + " /fs:ntfs /x";
                    }
                    else
                    {
                        show = "当前U盘格式 " + (driveInfo.IsReady ? driveInfo.DriveFormat : "RAW") + "，是否把U盘格式化为 NTFS？\n\n警告，格式化将删除U盘中的所有文件!\n警告，格式化将删除U盘中的所有文件!\n警告，格式化将删除U盘中的所有文件!\n\n当前U盘位置： " + driverName + "，容量：" + (driveInfo.IsReady ? ClassMbr.ConvertBytes(Convert.ToUInt64(driveInfo.TotalSize)) : "未知") + "\n取消格式化请按\"否(N)\"";
                        caption = "格式化U盘";
                        cmd = "format " + Regex.Replace(driverName, @"\\$", "") + " /fs:ntfs /q";
                    }
                    if (MessageBox.Show(show, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                    {
                        string outputString;
                        using (Process p = new Process())
                        {
                            p.StartInfo.FileName = "cmd.exe";
                            p.StartInfo.UseShellExecute = false;
                            p.StartInfo.RedirectStandardInput = true;
                            p.StartInfo.RedirectStandardError = true;
                            p.StartInfo.RedirectStandardOutput = true;
                            p.StartInfo.CreateNoWindow = true;
                            p.Start();

                            p.StandardInput.WriteLine(cmd);
                            p.StandardInput.WriteLine("exit");

                            p.StandardInput.Close();
                            outputString = p.StandardOutput.ReadToEnd();
                            p.WaitForExit();
                            p.Close();
                        }
                    }
                }
                if (driveInfo.IsReady && driveInfo.DriveFormat == "NTFS")
                {
                    if (File.Exists(driverName + "$ConsoleGen8"))
                        File.Delete(driverName + "$ConsoleGen8");
                    if (File.Exists(driverName + "$ConsoleGen9"))
                        File.Delete(driverName + "$ConsoleGen9");
                    if (File.Exists(driverName + "$ConsoleGen8Lock"))
                        File.Delete(driverName + "$ConsoleGen8Lock");
                    if (File.Exists(driverName + "$ConsoleGen9Lock"))
                        File.Delete(driverName + "$ConsoleGen9Lock");
                    if (rbXboxOne.Checked)
                    {
                        using (File.Create(driverName + (unlock ? "$ConsoleGen8" : "$ConsoleGen8Lock"))) { }
                    }
                    if (rbXboxSeries.Checked)
                    {
                        using (File.Create(driverName + (unlock ? "$ConsoleGen9" : "$ConsoleGen9Lock"))) { }
                    }
                }
                else
                {
                    MessageBox.Show("U盘不是NTFS格式，请重新格式化NTFS格式后再转换。", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                CbDrive_SelectedIndexChanged(null, null);
            }
            else
            {
                labelStatusDrive.Text = "当前U盘状态：" + driverName + " 设备不存在";
            }

        }





        private void Dgv_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            DataGridView dgv = (DataGridView)sender;
            Rectangle rectangle = new Rectangle(e.RowBounds.Location.X, e.RowBounds.Location.Y, dgv.RowHeadersWidth - 1, e.RowBounds.Height);
            TextRenderer.DrawText(e.Graphics, (e.RowIndex + 1).ToString(), dgv.RowHeadersDefaultCellStyle.Font, rectangle, dgv.RowHeadersDefaultCellStyle.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }

        delegate void CallbackTextBox(TextBox tb, string str);
        public void SetTextBox(TextBox tb, string str)
        {
            if (tb.InvokeRequired)
            {
                CallbackTextBox d = new CallbackTextBox(SetTextBox);
                Invoke(d, new object[] { tb, str });
            }
            else tb.Text = str;
        }

        delegate void CallbackSaveLog(string status, string content, string ip, int argb);
        public void SaveLog(string status, string content, string ip, int argb = 0)
        {
            if (lvLog.InvokeRequired)
            {
                CallbackSaveLog d = new CallbackSaveLog(SaveLog);
                Invoke(d, new object[] { status, content, ip, argb });
            }
            else
            {
                ListViewItem listViewItem = new ListViewItem(new string[] { status, content, ip, string.Format("{0:T}", DateTime.Now) });
                if (argb >= 1) listViewItem.ForeColor = Color.FromArgb(argb);
                lvLog.Items.Insert(0, listViewItem);
            }
        }

        class ListViewNF : ListView
        {
            public ListViewNF()
            {
                // 开启双缓冲
                this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

                // Enable the OnNotifyMessage event so we get a chance to filter out 
                // Windows messages before they get to the form's WndProc
                this.SetStyle(ControlStyles.EnableNotifyMessage, true);
            }

            protected override void OnNotifyMessage(Message m)
            {
                //Filter out the WM_ERASEBKGND message
                if (m.Msg != 0x14)
                {
                    base.OnNotifyMessage(m);
                }
            }
        }
    }
}