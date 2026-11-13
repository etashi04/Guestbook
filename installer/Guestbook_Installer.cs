// Based on the shared Korean patch installer layout. Patch data is embedded.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("이계의 숙박부 한국어 패치")]
[assembly: AssemblyProduct("이계의 숙박부 한국어 패치")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

internal static class Program
{
    internal const string GameName = "이계의 숙박부";
    internal const string Version = "v1.0.0";
    internal const string MarkerDir = "_Guestbook_KR_Backup_v1.0.0";

    [STAThread]
    static int Main(string[] args)
    {
        try { return RunMain(args); }
        catch (Exception e) { Console.Error.WriteLine(e.ToString()); return 1; }
    }
    static int RunMain(string[] args)
    {
        if (args.Length == 2 && (args[0] == "/install" || args[0] == "/restore" || args[0] == "/verify"))
        {
            try { PatchEngine.Run(args[1], args[0], delegate(string s) { Console.WriteLine(s); }); return 0; }
            catch (Exception e) { Console.Error.WriteLine(e.Message); return 1; }
        }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (var form = new InstallerForm())
        {
            Application.Run(form);
        }
        return 0;
    }

    sealed class InstallerForm : Form
    {
        readonly TextBox path = new TextBox();
        readonly Label status = new Label();
        readonly Button install = new Button(), restore = new Button(), browse = new Button();
        bool busy;
        public InstallerForm() : this(DisplayScale()) { }
        // The entire layout, including pixel-unit fonts, is scaled exactly once.
        // WinForms must not capture a 192-DPI baseline before controls are added.
        internal InstallerForm(float displayScale)
        {
            SuspendLayout(); AutoScaleMode = AutoScaleMode.None;
            Text = GameName + " 한국어 패치 " + Version;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("InstallerIcon")) Icon = new Icon(stream);
            ClientSize = new Size(640, 285); FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(245, 245, 245); Font = new Font("Malgun Gothic", 9F);
            var title = new Label { Text = GameName + "\r\n비공식 한국어 패치", Left = 28, Top = 22, Width = 420, Height = 58, Font = new Font("Malgun Gothic", 15, FontStyle.Bold) };
            Image logoImage;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("InstallerLogo"))
            using (var original = Image.FromStream(stream)) logoImage = new Bitmap(original);
            var logo = new PictureBox { Left = 476, Top = 19, Width = 128, Height = 60, Image = logoImage, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            var info = new Label { Text = "지원 게임: Steam 빌드 26645119 · 원본 파일은 자동 백업됩니다.", Left = 28, Top = 91, Width = 585, Height = 24 };
            var folder = new Label { Text = "게임 설치 폴더", Left = 28, Top = 125, Width = 150, Height = 22 };
            path.AutoSize = false; path.SetBounds(28, 148, 475, 25); path.Text = GuessPath();
            browse.Text = "찾아보기"; browse.SetBounds(513, 146, 100, 29);
            browse.Click += delegate { using (var dialog = new FolderBrowserDialog()) { dialog.SelectedPath = path.Text; if (dialog.ShowDialog(this) == DialogResult.OK) path.Text = dialog.SelectedPath; } };
            install.Text = "한국어 패치 설치"; install.SetBounds(28, 197, 178, 38); install.Font = new Font(Font, FontStyle.Bold);
            restore.Text = "원본 복구"; restore.SetBounds(218, 197, 128, 38); restore.Font = new Font(Font, FontStyle.Bold);
            install.Click += delegate { Run("/install"); }; restore.Click += delegate { Run("/restore"); };
            status.SetBounds(28, 245, 585, 26); status.TextAlign = ContentAlignment.MiddleLeft; status.AutoEllipsis = true;
            Controls.AddRange(new Control[] { title, logo, info, folder, path, browse, install, restore, status });
            foreach (Control control in Controls)
            {
                Rectangle design = control.Bounds; Font designFont = control.Font;
                control.Font = new Font(designFont.FontFamily, designFont.SizeInPoints * (96F / 72F) * displayScale, designFont.Style, GraphicsUnit.Pixel);
                control.SetBounds(Pixel(design.X, displayScale), Pixel(design.Y, displayScale), Pixel(design.Width, displayScale), Pixel(design.Height, displayScale));
            }
            Font = new Font("Malgun Gothic", 12F * displayScale, FontStyle.Regular, GraphicsUnit.Pixel);
            ClientSize = new Size(Pixel(640, displayScale), Pixel(285, displayScale));
            ResumeLayout(false); PerformLayout();
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (busy) { e.Cancel = true; status.Text = "작업을 마칠 때까지 잠시 기다려 주세요."; } };
        }
        static int Pixel(int value, float scale) { return (int)Math.Round(value * scale, MidpointRounding.AwayFromZero); }
        static float DisplayScale()
        {
            using (var graphics = Graphics.FromHwnd(IntPtr.Zero)) return Math.Max(1F, graphics.DpiX / 96F);
        }
        void Run(string mode)
        {
            string target = path.Text;
            if (string.IsNullOrWhiteSpace(target)) { status.Text = "Guestbook.exe가 있는 게임 폴더를 선택하세요."; return; }
            busy = true; path.Enabled = browse.Enabled = install.Enabled = restore.Enabled = false;
            var worker = new BackgroundWorker { WorkerReportsProgress = true };
            worker.DoWork += delegate { PatchEngine.Run(target, mode, delegate(string s) { worker.ReportProgress(0, s); }); };
            worker.ProgressChanged += delegate(object sender, ProgressChangedEventArgs e) { status.Text = (string)e.UserState; };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                busy = false; path.Enabled = browse.Enabled = install.Enabled = restore.Enabled = true;
                if (e.Error != null) { status.Text = "작업 실패 · 안내 내용을 확인하세요."; MessageBox.Show(this, e.Error.Message, "작업 실패", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                else { status.Text = mode == "/install" ? "설치 완료 · 게임의 일본어 언어 설정을 사용하세요." : "원본 복구가 완료되었습니다."; MessageBox.Show(this, status.Text, "완료", MessageBoxButtons.OK, MessageBoxIcon.Information); }
                worker.Dispose();
            };
            worker.RunWorkerAsync();
        }
        static string GuessPath()
        {
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] keys = { @"HKEY_CURRENT_USER\Software\Valve\Steam", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam" };
            foreach (string key in keys) foreach (string value in new[] { "SteamPath", "InstallPath" })
            {
                try { var found = Registry.GetValue(key, value, null) as string; if (!string.IsNullOrEmpty(found)) roots.Add(found); }
                catch (System.Security.SecurityException) { }
                catch (UnauthorizedAccessException) { }
            }
            roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
            var libraries = new HashSet<string>(roots, StringComparer.OrdinalIgnoreCase);
            foreach (string root in roots)
            {
                string vdf = Path.Combine(root, @"steamapps\libraryfolders.vdf");
                if (!File.Exists(vdf)) continue;
                foreach (Match match in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\"")) libraries.Add(match.Groups[1].Value.Replace(@"\\", @"\"));
            }
            foreach (string root in libraries)
            {
                string manifest = Path.Combine(root, @"steamapps\appmanifest_4249520.acf");
                if (!File.Exists(manifest)) continue;
                Match match = Regex.Match(File.ReadAllText(manifest), "\"installdir\"\\s+\"([^\"]+)\"");
                if (!match.Success) continue;
                string candidate = Path.GetFullPath(Path.Combine(root, "steamapps", "common", match.Groups[1].Value));
                if (File.Exists(Path.Combine(candidate, "Guestbook.exe"))) return candidate;
            }
            return "";
        }
    }
}

internal static class PatchEngine
{
    internal sealed class Entry
    {
        internal string Path, Original, Patched;
        internal Entry(string path, string original, string patched) { Path = path; Original = original; Patched = patched; }
    }
    // Generated from the verified, frozen build 26645119 inputs.
    internal static readonly Entry[] Files = BuildManifest.Files;
    sealed class Locked : IDisposable
    {
        internal Entry Entry; internal FileStream Stream; internal string Before, Snapshot; internal bool Attempted;
        public void Dispose() { Stream.Dispose(); }
    }
    static string Digest(Stream stream)
    {
        stream.Position = 0;
        using (var sha = SHA256.Create()) { string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); stream.Position = 0; return hash; }
    }
    internal static string Hash(string file) { using (var stream = File.OpenRead(file)) return Digest(stream); }
    static string Inside(string root, string relative)
    {
        string result = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!result.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("잘못된 패치 경로입니다.");
        // Refuse links inside the target so backup/copy cannot leave the selected folder.
        for (string at = result; at.Length > root.Length; at = Path.GetDirectoryName(at))
            if ((File.Exists(at) || Directory.Exists(at)) && (File.GetAttributes(at) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("게임 폴더 안의 연결 경로는 지원하지 않습니다: " + relative);
        return result;
    }
    static void Save(Stream source, string file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)); source.Position = 0;
        using (var output = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { source.CopyTo(output); output.Flush(true); }
        source.Position = 0;
    }
    static void Replace(Stream destination, string file, string expected)
    {
        using (var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (Digest(input) != expected) throw new InvalidDataException("설치 데이터 검증에 실패했습니다.");
            destination.Position = 0; destination.SetLength(input.Length); input.CopyTo(destination);
            ((FileStream)destination).Flush(true);
            if (Digest(destination) != expected) throw new IOException("복사 결과가 일치하지 않습니다.");
        }
    }
    static void Extract(string output)
    {
        using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
        using (var zip = new ZipArchive(resource, ZipArchiveMode.Read))
        {
            if (zip.Entries.Count != Files.Length) throw new InvalidDataException("내장 패치 구성이 올바르지 않습니다.");
            foreach (Entry file in Files)
            {
                var item = zip.GetEntry(file.Path); if (item == null) throw new InvalidDataException("내장 파일이 없습니다: " + file.Path);
                string destination = Inside(output, file.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                using (var source = item.Open()) using (var target = File.Create(destination)) source.CopyTo(target);
                if (Hash(destination) != file.Patched) throw new InvalidDataException("내장 패치가 손상되었습니다: " + file.Path);
            }
        }
    }
    static void DeleteOwnedTree(string parent, string directory)
    {
        string full = Path.GetFullPath(directory);
        if (!full.StartsWith(Path.GetFullPath(parent).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("정리 경로가 잘못되었습니다.");
        if (!Directory.Exists(full)) return;
        foreach (string child in Directory.GetFileSystemEntries(full))
        {
            if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) throw new IOException("연결 경로는 정리하지 않습니다.");
            if (Directory.Exists(child)) DeleteOwnedTree(full, child); else File.Delete(child);
        }
        Directory.Delete(full);
    }
    internal static void Run(string selected, string mode, Action<string> progress)
    {
        bool restore = mode == "/restore", verify = mode == "/verify";
        if (string.IsNullOrWhiteSpace(selected)) throw new InvalidOperationException("게임 설치 폴더를 선택하세요.");
        string root = Path.GetFullPath(selected.Trim()).TrimEnd('\\');
        if (!Directory.Exists(root) || !File.Exists(Inside(root, "Guestbook.exe"))) throw new InvalidOperationException("선택한 폴더에 Guestbook.exe가 없습니다.");
        foreach (var process in Process.GetProcessesByName("Guestbook")) { process.Dispose(); throw new InvalidOperationException("게임을 종료한 뒤 다시 실행하세요."); }
        progress("지원 버전과 파일 상태를 확인하고 있습니다.");
        string steamApps = Path.GetDirectoryName(Path.GetDirectoryName(root));
        string steamManifest = Path.Combine(steamApps, "appmanifest_4249520.acf");
        if (File.Exists(steamManifest))
        {
            Match build = Regex.Match(File.ReadAllText(steamManifest), "\"buildid\"\\s+\"(\\d+)\"");
            if (!build.Success || build.Groups[1].Value != "26645119") throw new InvalidOperationException("이 설치기는 Steam 빌드 26645119용입니다. 게임 업데이트 후에는 대응 패치를 사용하세요.");
        }
        foreach (Entry guard in BuildManifest.Guards)
            if (!File.Exists(Inside(root, guard.Path)) || Hash(Inside(root, guard.Path)) != guard.Original)
                throw new InvalidOperationException("지원 버전이 아니거나 파일이 변경되었습니다: " + guard.Path + "\r\nSteam 빌드 26645119의 원본 파일이 필요합니다.");
        string backup = Inside(root, Program.MarkerDir);
        string temp = Path.Combine(Path.GetTempPath(), "GuestbookPatch_" + Guid.NewGuid().ToString("N"));
        var locked = new List<Locked>();
        bool retainTransaction = false;
        try
        {
            foreach (Entry entry in Files)
            {
                string file = Inside(root, entry.Path);
                var item = new Locked { Entry = entry };
                try { item.Stream = new FileStream(file, FileMode.Open, verify ? FileAccess.Read : FileAccess.ReadWrite, FileShare.None); }
                catch (Exception e) { throw new IOException("파일을 열 수 없습니다: " + entry.Path + "\r\n게임과 파일 사용 프로그램을 종료하고 설치 폴더의 쓰기 권한을 확인하세요.", e); }
                locked.Add(item); item.Before = Digest(item.Stream);
                if (item.Before != entry.Original && item.Before != entry.Patched) throw new InvalidOperationException("알 수 없는 변경을 발견했습니다: " + entry.Path + "\r\nSteam 파일 무결성 검사로 원본을 복구한 뒤 설치하세요.");
            }
            if (verify) { progress("지원 버전 및 패치 대상 4개 파일 검사 통과."); return; }
            bool needsChange = locked.Exists(delegate(Locked f) { return f.Before != (restore ? f.Entry.Original : f.Entry.Patched); });
            if (restore && !needsChange) { progress("이미 원본 상태입니다."); return; }
            if (Directory.Exists(backup))
            {
                foreach (Entry entry in Files)
                    if (!File.Exists(Inside(backup, entry.Path)) || Hash(Inside(backup, entry.Path)) != entry.Original)
                        throw new InvalidDataException("원본 백업이 없거나 손상되었습니다. 백업을 보존하고 Steam 파일 무결성 검사를 이용하세요.");
            }
            else
            {
                if (restore || locked.Exists(delegate(Locked f) { return f.Before != f.Entry.Original; }))
                    throw new InvalidOperationException("복구에 필요한 원본 백업이 없습니다. Steam 파일 무결성 검사로 원본을 복구한 뒤 설치하세요.");
                string pending = Inside(root, Program.MarkerDir + ".pending-" + Guid.NewGuid().ToString("N"));
                try
                {
                    progress("원본 파일을 백업하고 있습니다.");
                    foreach (Locked item in locked)
                    {
                        string saved = Inside(pending, item.Entry.Path); Save(item.Stream, saved);
                        if (Hash(saved) != item.Entry.Original) throw new IOException("원본 백업 검증 실패.");
                    }
                    File.WriteAllText(Path.Combine(pending, "백업정보.txt"), "이계의 숙박부 한국어 패치 v1.0.0\r\nSteam build 26645119\r\n이 폴더는 원본 복구에 필요합니다.\r\n", Encoding.UTF8);
                    Directory.Move(pending, backup);
                }
                finally { if (Directory.Exists(pending)) DeleteOwnedTree(root, pending); }
            }
            if (!needsChange) { progress("이미 같은 한국어 패치가 설치되어 있습니다."); return; }
            Directory.CreateDirectory(temp);
            if (!restore) { progress("내장 패치 데이터를 검사하고 있습니다."); Extract(Inside(temp, "payload")); }
            foreach (Locked item in locked)
            {
                item.Snapshot = Inside(temp, "before/" + item.Entry.Path); Save(item.Stream, item.Snapshot);
                if (Hash(item.Snapshot) != item.Before) throw new IOException("작업 직전 상태 백업에 실패했습니다.");
            }
            int written = 0;
            foreach (Locked item in locked)
            {
                string expected = restore ? item.Entry.Original : item.Entry.Patched;
                if (item.Before == expected) continue;
                progress((restore ? "원본 복구 중" : "한국어 패치 설치 중") + " (" + (++written) + "/4)");
                item.Attempted = true;
                Replace(item.Stream, Inside(restore ? backup : Inside(temp, "payload"), item.Entry.Path), expected);
                #if TESTING
                if (Environment.GetEnvironmentVariable("GUESTBOOK_TEST_FAIL_AFTER") == written.ToString()) throw new IOException("검증용 복사 실패");
                #endif
            }
            progress(restore ? "원본 복구 완료." : "한국어 패치 설치 완료.");
        }
        catch (Exception failure)
        {
            var rollbackErrors = new List<string>();
            for (int i = locked.Count - 1; i >= 0; --i)
            {
                Locked item = locked[i]; if (!item.Attempted) continue;
                try { Replace(item.Stream, item.Snapshot, item.Before); }
                catch { rollbackErrors.Add(item.Entry.Path); }
            }
            if (rollbackErrors.Count != 0)
            {
                retainTransaction = true;
                throw new IOException(failure.Message + "\r\n자동 되돌리기에 실패한 파일: " + string.Join(", ", rollbackErrors.ToArray()) + "\r\n작업 직전 백업 보존 위치: " + temp, failure);
            }
            throw;
        }
        finally
        {
            foreach (Locked item in locked) item.Dispose();
            if (!retainTransaction && Directory.Exists(temp)) { try { DeleteOwnedTree(Path.GetTempPath(), temp); } catch { /* A temporary cleanup failure must not invalidate a completed install. */ } }
        }
    }
}
