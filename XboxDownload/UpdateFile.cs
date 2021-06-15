using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace XboxDownload
{
    class UpdateFile
    {
        public static string updateUrl = "https://github.com/skydevil88/XboxDownload/releases/download/v1";

        public static void Start(bool autoupdate)
        {
            SocketPackage socketPackage = ClassWeb.HttpRequest(updateUrl + "/XboxDownload.exe.md5", "GET", null, null, true, false, true, null, null, null, null, null, null, null, 0, null);
            string md5 = socketPackage.Html;
            if (!Regex.IsMatch(md5, @"^[A-Z0-9]{32}$"))
            {
                if (!autoupdate)
                {
                    Application.OpenForms[0].Invoke(new MethodInvoker(() => 
                    {
                        MessageBox.Show("检查更新出错，请稍候再试。", "软件更新", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
                return;
            }
            if (!string.Equals(md5, GetPathMD5(Application.ExecutablePath)))
            {
                bool isUpdate = false;
                Application.OpenForms[0].Invoke(new MethodInvoker(() => 
                {
                    isUpdate = MessageBox.Show("已检测到新版本, 是否立即更新?", "软件更新", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
                }));
                if (!isUpdate) return;

                string filename = Path.GetFileName(Application.ExecutablePath);
                Task[] tasks = new Task[3];
                tasks[0] = new Task(() =>
                {
                    SocketPackage sp = ClassWeb.HttpRequest(updateUrl + "/XboxDownload.exe", "GET", null, null, true, false, false, null, null, null, null, null, null, null, 0, null);
                    if (string.IsNullOrEmpty(sp.Err))
                    {
                        using (FileStream fs = new FileStream(filename + ".update", FileMode.Create, FileAccess.Write))
                        {
                            fs.Write(sp.Buffer, 0, sp.Buffer.Length);
                            fs.Flush();
                            fs.Close();
                        }
                    }
                });
                tasks[1] = new Task(() =>
                {
                    SocketPackage sp = ClassWeb.HttpRequest(updateUrl + "/ProductManual.pdf", "GET", null, null, true, false, false, null, null, null, null, null, null, null, 0, null);
                    if (string.IsNullOrEmpty(sp.Err))
                    {
                        using (FileStream fs = new FileStream(Application.StartupPath + "\\ProductManual.pdf", FileMode.Create, FileAccess.Write))
                        {
                            fs.Write(sp.Buffer, 0, sp.Buffer.Length);
                            fs.Flush();
                            fs.Close();
                        }
                    }
                });
                tasks[2] = new Task(() =>
                {
                    SocketPackage sp = ClassWeb.HttpRequest(updateUrl + "/IP.assets1.xboxlive.cn.txt", "GET", null, null, true, false, false, null, null, null, null, null, null, null, 0, null);
                    if (string.IsNullOrEmpty(sp.Err))
                    {
                        using (FileStream fs = new FileStream(Application.StartupPath + "\\IP.assets1.xboxlive.cn.txt", FileMode.Create, FileAccess.Write))
                        {
                            fs.Write(sp.Buffer, 0, sp.Buffer.Length);
                            fs.Flush();
                            fs.Close();
                        }
                    }
                });
                Array.ForEach(tasks, x => x.Start());
                Task.WaitAll(tasks);

                bool success = false;
                FileInfo fi = new FileInfo(filename + ".update");
                if (fi.Exists)
                {
                    if (string.Equals(md5, GetPathMD5(fi.FullName)))
                    {
                        success = true;
                        if (File.Exists(filename + ".md5"))
                        {
                            File.Delete(filename + ".md5");
                        }
                        using (FileStream fs = File.Create(".update.cmd"))
                        {
                            Byte[] byteArray = new UTF8Encoding(true).GetBytes("cd /d %~dp0\nchoice /t 3 /d y /n >nul\ntaskkill /pid " + Process.GetCurrentProcess().Id + " /f\nmove \"" + filename + ".update\" \"" + filename + "\"\n\"" + filename + "\"\ndel /a/f/q .update.cmd");
                            fs.Write(byteArray, 0, byteArray.Length);
                            fs.Close();
                        }
                        File.SetAttributes(".update.cmd", FileAttributes.Hidden);
                        using (Process p = new Process())
                        {
                            p.StartInfo.FileName = "cmd.exe";
                            p.StartInfo.UseShellExecute = false;
                            p.StartInfo.CreateNoWindow = true;
                            p.StartInfo.Arguments = "/c \"" + Directory.GetCurrentDirectory() + "\\.update.cmd\"";
                            p.Start();
                        }
                        Process.GetCurrentProcess().Kill();
                    }
                    else
                    {
                        fi.Delete();
                    }
                }
                if (!success)
                {
                    Application.OpenForms[0].Invoke(new MethodInvoker(() =>
                    {
                        MessageBox.Show("下载文件出错，请稍候再试。", "软件更新", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            }
            else if (!autoupdate)
            {
                Application.OpenForms[0].Invoke(new MethodInvoker(() => 
                {
                    MessageBox.Show("软件已经是最新版本。", "软件更新", MessageBoxButtons.OK, MessageBoxIcon.None);
                }));
            }
        }

        public static string GetPathMD5(string path)
        {
            using (MD5 md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(path))
                {
                    return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", string.Empty);
                }
            }
        }
    }
}