using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Klangstudio.Properties;
using System.Management;

using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;
using System.Security;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Security.Principal;
using WMPLib;

namespace Klangstudio

{
    public partial class Main : Form
    {
        Size formSize;
        String play;

        public PictureBox lastPic; // last selected button to play

        string AppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\" + System.AppDomain.CurrentDomain.FriendlyName.Substring(0, System.AppDomain.CurrentDomain.FriendlyName.IndexOf("."));

        public Main()
        {
            InitializeComponent();
        }

        [System.Flags]
        public enum PlaySoundFlags : int
        {
            SND_SYNC = 0x0000,
            SND_ASYNC = 0x0001,
            SND_NODEFAULT = 0x0002,
            SND_LOOP = 0x0008,
            SND_NOSTOP = 0x0010,
            SND_NOWAIT = 0x00002000,
            SND_FILENAME = 0x00020000,
            SND_RESOURCE = 0x00040004
        }

        private string GetFilename(string hreflink)
        {
            Uri uri = new Uri(hreflink);

            string filename = System.IO.Path.GetFileName(uri.LocalPath);

            return filename;
        }

        WebClient wc = new WebClient();

        string ftpUserID = "app-ID-Daten";
        string ftpUserPw = "app-PW";

        int filesToDownload = 0;
        // Download a file asynchronously in the desktop path, show the download progress and save it with the original filename.
        private void DownloadFile(string url, string path)
        {
            //string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            // string url = "https://www.7-zip.org/a/7z1805-x64.exe";
            string filename = GetFilename(url);

            using (wc)
            {
                wc.Credentials = new NetworkCredential(ftpUserID, ftpUserPW);
                wc.DownloadProgressChanged += Wc_DownloadProgressChanged;
                wc.DownloadFileCompleted += Wc_DownloadFileCompleted;
                downloadingTryTimes = 1; // times retry to download the file
                wc.DownloadFileAsync(new Uri(url), path);
            }
        }

        private void CancelDownload(WebClient wc)
        {
            wc.CancelAsync();
        }

        int lastBytesReceived = 0;
        private void Wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            pbDownloading.Value = pbDownloading.Value + ((int)e.BytesReceived - lastBytesReceived) / 1000 > pbDownloading.Maximum ? pbDownloading.Maximum : pbDownloading.Value + ((int)e.BytesReceived - lastBytesReceived) / 1000;
            lastBytesReceived = (int)e.BytesReceived;
            // Console.WriteLine(e.BytesReceived +"/"+ fz + "B "+ e.ProgressPercentage + "% | " + e.BytesReceived + " bytes out of " + e.TotalBytesToReceive + " bytes retrieven.");
            // 50% | 5000 bytes out of 10000 bytes retrieven.
        }


        private string FileExt(string fn)
        {
            return fn.Substring(fn.Length - 3, 3);
        }

        int downloadingTryTimes = 0;
        bool downloadError = false;
        private void Wc_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            bool downloadedSuccessfully = false;
            Console.WriteLine(pbDownloading.Value + "/" + filesToDownload);
            if (e.Cancelled)
            {
                // MessageBox.Show("The download has been cancelled");
                return;
            }

            if (e.Error != null) // We have an error! Retry a few times, then abort.
            {
                // MessageBox.Show("An error ocurred while trying to download file");
                return;
            }
            // MessageBox.Show("File succesfully downloaded");
            if (urlToDownload.Count() == 0)
            {
                return;
            }
            Console.WriteLine(pathToMove[0]);
            try
            {
                // at application start
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);

                // if it is .exe then remane it
                if (File.Exists(pathToMove[0]))
                {

                    if (GetFilename(pathToMove[0]) != "Klangstudio.exe")
                    {
                        File.Delete(pathToMove[0]);
                    }
                    else
                    {
                        if (File.Exists(pathToMove[0] + "_"))
                        {
                            File.Delete(pathToMove[0] + "_");
                        }
                        if (File.Exists(pathToDownload[0]))
                        {
                            downloadedSuccessfully = true;



                            // if not admin - elevate
                            if (!isAdmin)
                            {
                                var info = new ProcessStartInfo(Application.ExecutablePath) { Verb = "runas" };
                                Process.Start(info);
                                Environment.Exit(0);
                                return; // exit
                            }
                            if (isAdmin)
                            {
                                // move the .exe
                                File.Move(pathToDownload[0], pathToMove[0] + "_");
                            }
                        }
                        //show restart button
                        btnRestart.Visible = true;
                    }
                }
                if (File.Exists(pathToDownload[0]))
                {
                    if (FileExt(GetFilename(pathToDownload[0])) == "exe")
                    {
                        if (!isAdmin)
                        {
                            var info = new ProcessStartInfo(Application.ExecutablePath) { Verb = "runas" };
                            Process.Start(info);
                            Environment.Exit(0);
                            return; // exit
                        }
                        if (isAdmin)
                        {
                            // move the .exe
                            downloadedSuccessfully = true;
                            File.Move(pathToDownload[0], pathToMove[0]);
                        }
                    }
                    else
                    {
                        downloadedSuccessfully = true;
                        File.Move(pathToDownload[0], pathToMove[0]);
                    }
                }
            }
            catch (IOException iox)
            {
                Console.WriteLine(iox.Message);
            }

            if (downloadedSuccessfully || downloadingTryTimes == 10) // retry to download a file for 10 times
            {
                urlToDownload.RemoveAt(0);
                pathToDownload.RemoveAt(0);
                pathToMove.RemoveAt(0);
            }
            else
            {
                Console.WriteLine("try to download {0} for {1} more time.", urlToDownload[0], downloadingTryTimes++);
            }
            if (!downloadedSuccessfully && downloadingTryTimes == 10)
            {
                downloadError = true;
            }


            lastBytesReceived = 0;
            timDownload.Start();
        }

        private void DownloadingIsFinished()
        {
            if (downloadError)
            {
                lblDownloading.Text = lblDownloading.AccessibleDescription;
            }
            else
            {
                lblDownloading.Visible = false;
            }
            pbDownloading.Visible = false;
            if (!File.Exists(@"C:\Program Files\7-Zip\7z.exe"))
            {
                CallApp(AppDomain.CurrentDomain.BaseDirectory + "7z1805-x64.exe", "", "", "");
            }

        }

        private void CompareAndDownload(string url, string path, string toPath)
        {
            // get the local time
            DateTime localTime = Directory.GetCreationTimeUtc(path);
            DateTime localUpdateTime = Directory.GetLastWriteTimeUtc(path);
            // get the newest date
            if (localTime < localUpdateTime)
            {
                localTime = localUpdateTime;
            }

            // get the remore time
            DateTime remoteTime = GetFTPTimeStamp(url);
            // copare the times
            if (remoteTime.ToUniversalTime() > localTime.AddMinutes(1))
            {
                //download and replace file!!
                Console.WriteLine("downloading:" + path);
                // add file size to the prog bar .max
                int fz = +GetFTPFZ(url);
                if (fz > 0)
                {
                    pbDownloading.Maximum = pbDownloading.Maximum + fz / 1000;
                    urlToDownload.Add(url);
                    pathToDownload.Add(toPath);
                    pathToMove.Add(path);
                    NumberOfDownloadingFiles++;
                }
            }
        }

        long NumberOfDownloadingFiles = 0;

        List<String> urlToDownload = new List<String>();
        List<String> pathToDownload = new List<String>();
        List<String> pathToMove = new List<String>();

        private void CheckForUpdate()
        {
            string suburl = "wav/";
            string subpath = AppDataPath + "\\wav";

            lblDownloading.Visible = true;
            pbDownloading.Visible = true;
            //string fp = AppDataPath + "\\wav"; // file with path
            string fp = subpath; // file with path
                                 // create \wav\tmp if not exists

            if (!Directory.Exists(fp))
            {
                Directory.CreateDirectory(fp);
            }
            string tfp = fp + "\\tmp"; // temp file path

            //  create \wav\tmp if not exists
            if (!Directory.Exists(tfp))
            {
                Directory.CreateDirectory(tfp);
            }
            else
            {
                System.IO.DirectoryInfo di = new DirectoryInfo(tfp);

                foreach (FileInfo file in di.GetFiles())
                {// del *.*
                    file.Delete();
                }
            }

            fp += "\\";
            tfp += "\\";
            string fn;
            //string ftpurl = "ftp://www.web-example.com/wav/" ;
            string ftpurl = "ftp://www.web-example.com/" + suburl;

            // comparing the wav files
            for (int i = 0; i < pb.Count(); i++)
            {
                fn = pb[i].AccessibleDescription + ".7z.001";
                //compare           url , local folder, save in temp folder
                CompareAndDownload(ftpurl + fn, fp + fn, tfp + fn);
            }
            fp = AppDomain.CurrentDomain.BaseDirectory;
            //tfp = fp + "\\tmp";
            ftpurl = "ftp://www.web-example.com/";
            // if file not exist the download it
            fn = "7z1805-x64.exe"; CompareAndDownload(ftpurl + fn, fp + fn, tfp + fn);
            fn = "Klangstudio.exe"; CompareAndDownload(ftpurl + fn, fp + fn, tfp + fn);
            fn = "rkks_update.exe"; CompareAndDownload(ftpurl + fn, fp + fn, tfp + fn);
            fn = "Teufel.exe"; CompareAndDownload(ftpurl + fn, fp + fn, tfp + fn);
            fn = "Speaker.exe"; CompareAndDownload(ftpurl + fn, fp + fn, tfp + fn);

            if (urlToDownload.Count() > 0)
            {
                timDownload.Start();
            }
            else
            {
                DownloadingIsFinished();
            }


        }

        private PictureBox[] pb = new PictureBox[32];

        private void Form1_Load(object sender, EventArgs e)
        {
            // Set window location
            if (Settings.Default.WindowLocation != null)
            {
                //this.Location = Settings.Default.WindowLocation;
            }

            tcAudio.Appearance = TabAppearance.FlatButtons;
            //tcAudio.ItemSize = new Size(0, 1);
            tcAudio.SizeMode = TabSizeMode.Fixed;
            tcAudio.DrawMode = TabDrawMode.OwnerDrawFixed;


            // Set window size
            if (Settings.Default.WindowSize != null)
            {
                this.Size = Settings.Default.WindowSize;
            }
            formSize = this.Size;

            DetectDevices();

            play = "Stop";

            // slelct default audio device
            //cbDefaultAD.Items.Add("HS");
            cbDefaultAD.Items.Add("SPK");
            cbDefaultAD.Items.Add("Teufel");
            cbDefaultAD.SelectedIndex = cbDefaultAD.FindStringExact("Teufel");

            // load the saved volume
            Console.WriteLine("s2:{0}", Settings.Default.s2);
            // if (Settings.Default.s2 != 50)
            {
                s0.AccessibleName = Settings.Default.s0.ToString();
                s1.AccessibleName = Settings.Default.s1.ToString();
                s2.AccessibleName = Settings.Default.s2.ToString();
                s3.AccessibleName = Settings.Default.s3.ToString();
                s4.AccessibleName = Settings.Default.s4.ToString();
                s5.AccessibleName = Settings.Default.s5.ToString();
                // s6.AccessibleName = Settings.Default.s6.ToString();
                s7.AccessibleName = Settings.Default.s7.ToString();
                s8.AccessibleName = Settings.Default.s8.ToString();
                s9.AccessibleName = Settings.Default.s9.ToString();
                s10.AccessibleName = Settings.Default.s10.ToString();
                s11.AccessibleName = Settings.Default.s11.ToString();
                s12.AccessibleName = Settings.Default.s12.ToString();

                h1.AccessibleName = Settings.Default.h1.ToString();
                h2.AccessibleName = Settings.Default.h2.ToString();
                h3.AccessibleName = Settings.Default.h3.ToString();
                h4.AccessibleName = Settings.Default.h4.ToString();
                h5.AccessibleName = Settings.Default.h5.ToString();
                h6.AccessibleName = Settings.Default.h6.ToString();
                h8.AccessibleName = Settings.Default.h7.ToString();
                h9.AccessibleName = Settings.Default.h8.ToString();
                // h9.AccessibleName = Settings.Default.h9.ToString();
                // h10.AccessibleName = Settings.Default.h10.ToString();
                // h11.AccessibleName = Settings.Default.h11.ToString();
                // h12.AccessibleName = Settings.Default.h12.ToString();

                u1.AccessibleName = Settings.Default.u1.ToString();
                u2.AccessibleName = Settings.Default.u2.ToString();
                u3.AccessibleName = Settings.Default.u3.ToString();
                u4.AccessibleName = Settings.Default.u4.ToString();
                u5.AccessibleName = Settings.Default.u5.ToString();
                // u6.AccessibleName = Settings.Default.u6.ToString();

                // page 2
                g1.AccessibleName = Settings.Default.g1.ToString();
                g2.AccessibleName = Settings.Default.g2.ToString();
                v1.AccessibleName = Settings.Default.v1.ToString();
                v2.AccessibleName = Settings.Default.v2.ToString();
                w1.AccessibleName = Settings.Default.w1.ToString();
                w2.AccessibleName = Settings.Default.w2.ToString();


            }

            pb[0] = s0;
            pb[1] = s1;
            pb[2] = s2;
            pb[3] = s3;
            pb[4] = s4;
            pb[5] = s5;
            pb[6] = s7;
            pb[7] = s8;
            pb[8] = s9;
            pb[9] = s10;
            pb[10] = s11;
            pb[11] = s12;
            pb[12] = h1;
            pb[13] = h2;
            pb[14] = h3;
            pb[15] = h4;
            pb[16] = h5;
            pb[17] = h6;
            pb[18] = h7;
            pb[19] = h8;
            pb[20] = h9;
            pb[21] = u1;
            pb[22] = u2;
            pb[23] = u3;
            pb[24] = u4;
            pb[25] = u5;
            // page 2
            pb[26] = g1;
            pb[27] = g2;
            pb[28] = v1;
            pb[29] = v2;
            pb[30] = w1;
            pb[31] = w2;


            // url, local folder
            CheckForUpdate(); // for short time

        }

        private void ProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            pbDownloading.Value = e.ProgressPercentage;
        }

        private void Completed(object sender, AsyncCompletedEventArgs e)
        {
            MessageBox.Show("Download completed!");
        }

        private void DownloadFileFTP(string LocalStoragePath, string RemoteFilePath)
        {
            string ftphost = "web-example.com";
            string ftpfullpath = "ftp://" + ftphost + RemoteFilePath;
            using (WebClient client = new WebClient())
            {
                client.Credentials = new NetworkCredential(ftpUserID, ftpUserPW);
                byte[] fileData = client.DownloadData(ftpfullpath);
                using (FileStream file = File.Create(LocalStoragePath))
                {
                    file.Write(fileData, 0, fileData.Length);
                    file.Close();
                }
            }
        }



        public static bool GetDateTimestampOnServer(Uri serverUri)
        {
            // The serverUri should start with the ftp:// scheme.
            if (serverUri.Scheme != Uri.UriSchemeFtp)
            {
                return false;
            }

            // Get the object used to communicate with the server.
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(serverUri);
            request.Method = WebRequestMethods.Ftp.GetDateTimestamp;
            FtpWebResponse response = (FtpWebResponse)request.GetResponse();
            Console.WriteLine("{0} {1}", serverUri, response.LastModified);

            // The output from this method will vary depending on the 
            // file specified and your regional settings. It is similar to:
            // ftp://contoso.com/Data.txt 4/15/2003 10:46:02 AM
            return true;
        }

        public static DateTime GetFTPTimeStamp(string url)
        {
            // Get the object used to communicate with the server.
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(url);

            request.Method = WebRequestMethods.Ftp.GetDateTimestamp;

            // the FTP site uses logon.
            request.Credentials = new NetworkCredential(ftpUserID, ftpUserPW);
            //--
            // The serverUri should start with the ftp:// scheme.
            Uri serverUri = new Uri(url);
            if (serverUri.Scheme != Uri.UriSchemeFtp)
            {
                return new DateTime(3000, 1, 1);
            }
            //--
            try
            {
                using (FtpWebResponse response =
                    (FtpWebResponse)request.GetResponse())
                {

                    // Return the size.
                    return response.LastModified;
                }
            }
            catch (Exception ex)
            {
                // If the file doesn't exist, return Jan 1, 3000.
                // Otherwise rethrow the error.
                if (ex.Message.Contains("File unavailable"))
                    return new DateTime(3000, 1, 1);
                return new DateTime(3000, 1, 1);
                throw;
            }
        }

        public static int GetFTPFZ(string url)
        {
            // Get the object used to communicate with the server.
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(url);
            request.Method = WebRequestMethods.Ftp.GetFileSize;

            // the FTP site uses logon.
            request.Credentials = new NetworkCredential(ftpUserID, ftpUserPW);

            try
            {
                using (FtpWebResponse response =
                    (FtpWebResponse)request.GetResponse())
                {
                    // Return the size.
                    return (int)response.ContentLength;
                }
            }
            catch (Exception ex)
            {
                // If the file doesn't exist, return Jan 1, 3000.
                // Otherwise rethrow the error.
                if (ex.Message.Contains("File unavailable"))
                    return 0;
                return 0;
                throw;
            }
        }

        public static void GetFTPFileSize(string url)
        {
            // Get the object used to communicate with the server.
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(url);
            request.Method = WebRequestMethods.Ftp.GetFileSize;

            // the FTP site uses logon.
            request.Credentials = new NetworkCredential(ftpUserID, ftpUserPW);
            try
            {
                FtpWebRequest ftpFileSizeRequest = (FtpWebRequest)WebRequest.Create("ftp://www.web-example.com/wav/1 Willkommen.7z.001");
                ftpFileSizeRequest.Method = WebRequestMethods.Ftp.GetFileSize;
                FtpWebResponse ftpResponse = (FtpWebResponse)ftpFileSizeRequest.GetResponse();
                Console.WriteLine(ftpResponse.ContentLength.ToString());
                StreamReader streamReader = new StreamReader(ftpResponse.GetResponseStream());
                string fileSizeString = streamReader.ReadToEnd();  // This is ALWAYS an empty string
                Console.WriteLine(fileSizeString);
                streamReader.Close();

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);

            }
        }








        // force terminiate app 
        public static bool KillProc(string proc)
        {
            Process[] GetPArry = Process.GetProcesses();
            foreach (Process testProcess in GetPArry)
            {
                string ProcessName = testProcess.ProcessName;

                ProcessName = ProcessName.ToLower();
                if (ProcessName.CompareTo(proc) == 0)
                {
                    testProcess.Kill();
                    return true;
                }
            }
            return false;
        }


        public static async void WaitUntilFileExists(string file, int waitSec, int waitInt, Action<bool> callback)
        {
            int wait = 0;
            bool result = false;
            do
            {
                if (!File.Exists(file))
                {
                    await Task.Delay(waitInt);
                }
                else
                {
                    result = true;
                }
                wait = wait + waitInt;
            } while (wait < waitSec);
            callback?.Invoke(result);
        }






        public static void CallApp(string path, string arg, string name, string pw)
        {
            Process myProcess = new Process();
            try
            {
                myProcess.StartInfo.UseShellExecute = false;
                myProcess.StartInfo.FileName = path;
                myProcess.StartInfo.Arguments = arg;
                myProcess.StartInfo.CreateNoWindow = true;
                if (name != "")
                    myProcess.StartInfo.UserName = name;
                if (pw != "")
                {
                    var secure = new SecureString();
                    foreach (char c in pw)
                    {
                        secure.AppendChar(c);
                    }
                    myProcess.StartInfo.Password = secure;
                }
                myProcess.Start();

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}



