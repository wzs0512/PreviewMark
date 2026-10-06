using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace PreviewMark
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                try
                {
                    string action = args[0].Trim().ToLowerInvariant();
                    if (action == "--scan")
                    {
                        ScanResult scan = WatermarkEngine.Scan();
                        Console.WriteLine("Found watermark paint function at shell32 RVA 0x{0:X}.", scan.Rva);
                        Console.WriteLine("Current Windows build: {0}", WindowsInfo.Build);
                        return 0;
                    }
                    if (action == "--apply")
                    {
                        Console.WriteLine(WatermarkEngine.Apply());
                        return 0;
                    }
                    if (action == "--restore")
                    {
                        Console.WriteLine(WatermarkEngine.Restore());
                        return 0;
                    }
                    if (action == "--self-check")
                    {
                        Console.WriteLine(WatermarkEngine.SelfCheck());
                        return 0;
                    }
                    Console.Error.WriteLine("Usage: PreviewMark.Cli.exe [--scan | --self-check | --apply | --restore]");
                    return 2;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    return 1;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly Label status;
        private readonly Button scanButton;
        private readonly Button applyButton;
        private readonly Button restoreButton;

        internal MainForm()
        {
            Text = "PreviewMark · 预览版水印开关";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = true;
            ClientSize = new Size(570, 330);
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            Label title = new Label();
            title.Text = "Windows 预览版桌面水印";
            title.Font = new Font(Font.FontFamily, 16F, FontStyle.Bold, GraphicsUnit.Point);
            title.Location = new Point(26, 22);
            title.Size = new Size(510, 34);
            Controls.Add(title);

            Label description = new Label();
            description.Text = "只处理 Insider 预览版的桌面构建水印。设置仅在当前 Explorer 会话内生效，重启 Explorer 或注销后自动恢复。";
            description.Location = new Point(28, 69);
            description.Size = new Size(510, 48);
            Controls.Add(description);

            Label build = new Label();
            build.Text = "当前系统：" + WindowsInfo.Build;
            build.Location = new Point(28, 127);
            build.Size = new Size(510, 24);
            Controls.Add(build);

            status = new Label();
            status.BorderStyle = BorderStyle.FixedSingle;
            status.BackColor = SystemColors.ControlLightLight;
            status.Location = new Point(28, 160);
            status.Size = new Size(510, 70);
            status.Padding = new Padding(9);
            status.Text = "尚未检查。点击“检查”确认此系统版本是否受支持。";
            Controls.Add(status);

            scanButton = new Button();
            scanButton.Text = "检查";
            scanButton.Location = new Point(28, 246);
            scanButton.Size = new Size(108, 36);
            scanButton.Click += delegate { RunAction("scan"); };
            Controls.Add(scanButton);

            applyButton = new Button();
            applyButton.Text = "隐藏本次水印";
            applyButton.Location = new Point(151, 246);
            applyButton.Size = new Size(150, 36);
            applyButton.Click += delegate { RunAction("apply"); };
            Controls.Add(applyButton);

            restoreButton = new Button();
            restoreButton.Text = "恢复";
            restoreButton.Location = new Point(316, 246);
            restoreButton.Size = new Size(108, 36);
            restoreButton.Click += delegate { RunAction("restore"); };
            Controls.Add(restoreButton);

            Label footer = new Label();
            footer.Text = "不处理“激活 Windows”提示；不修改系统文件或设置开机任务。";
            footer.ForeColor = SystemColors.GrayText;
            footer.Location = new Point(28, 294);
            footer.Size = new Size(510, 22);
            Controls.Add(footer);
        }

        private void RunAction(string action)
        {
            scanButton.Enabled = false;
            applyButton.Enabled = false;
            restoreButton.Enabled = false;
            try
            {
                if (action == "scan")
                {
                    ScanResult scan = WatermarkEngine.Scan();
                    status.Text = String.Format("支持此系统构建。目标函数位于 shell32.dll + 0x{0:X}。\r\n检查不会更改系统。", scan.Rva);
                }
                else if (action == "apply")
                {
                    status.Text = WatermarkEngine.Apply();
                }
                else
                {
                    status.Text = WatermarkEngine.Restore();
                }
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
            }
            finally
            {
                scanButton.Enabled = true;
                applyButton.Enabled = true;
                restoreButton.Enabled = true;
            }
        }
    }

    internal sealed class ScanResult
    {
        internal uint Rva;
        internal int FileOffset;
        internal byte OriginalByte;
    }

    internal sealed class PeSection
    {
        internal uint VirtualAddress;
        internal uint VirtualSize;
        internal uint RawSize;
        internal uint RawOffset;
        internal uint Characteristics;
    }

    internal sealed class FunctionCandidate
    {
        internal uint FunctionRva;
    }

    internal sealed class ExplorerTarget
    {
        internal int ProcessId;
        internal long ProcessStartTicks;
        internal long ModuleBase;
        internal int ModuleSize;
    }

    internal sealed class PatchState
    {
        internal int ProcessId;
        internal long ProcessStartTicks;
        internal uint Rva;
        internal byte OriginalByte;
    }

    internal static class WindowsInfo
    {
        internal static string Build
        {
            get
            {
                try
                {
                    using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                    {
                        string name = Convert.ToString(key.GetValue("ProductName", "Windows"));
                        string version = Convert.ToString(key.GetValue("DisplayVersion", ""));
                        string build = Convert.ToString(key.GetValue("CurrentBuild", ""));
                        string ubr = Convert.ToString(key.GetValue("UBR", ""));
                        return String.Format("{0} {1} (build {2}.{3}, {4})", name, version, build, ubr,
                            Environment.Is64BitOperatingSystem ? "x64" : "x86");
                    }
                }
                catch
                {
                    return Environment.OSVersion.Version.ToString();
                }
            }
        }
    }

    internal static class WatermarkEngine
    {
        private const byte ReturnInstruction = 0xC3;
        private const uint ProcessVmOperation = 0x0008;
        private const uint ProcessVmRead = 0x0010;
        private const uint ProcessVmWrite = 0x0020;
        private const uint ProcessQueryInformation = 0x0400;
        private const uint PageExecuteReadWrite = 0x40;
        private const uint RedrawInvalidate = 0x0001;
        private const uint RedrawErase = 0x0004;
        private const uint RedrawAllChildren = 0x0080;
        private const uint RedrawUpdateNow = 0x0100;

        private static string WindowsDirectory
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.Windows); }
        }

        private static string Shell32Path
        {
            get { return Path.Combine(WindowsDirectory, "System32\\shell32.dll"); }
        }

        private static string StatePath
        {
            get
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PreviewMark");
                return Path.Combine(folder, "session.ini");
            }
        }

        internal static ScanResult Scan()
        {
            if (!Environment.Is64BitOperatingSystem || IntPtr.Size != 8)
                throw new InvalidOperationException("当前版本仅支持 x64 Windows。");

            byte[] image = File.ReadAllBytes(Shell32Path);
            return FindWatermarkFunction(image);
        }

        internal static string Apply()
        {
            ScanResult scan = Scan();
            ExplorerTarget target = FindExplorerTarget(scan.Rva);
            if ((ulong)scan.Rva >= (ulong)target.ModuleSize)
                throw new InvalidOperationException("扫描到的函数不在 Explorer 已加载的 shell32.dll 范围内，已停止。");

            using (SafeProcessHandle handle = OpenExplorer(target))
            {
                PatchState oldState = ReadState();
                if (oldState != null)
                    HandleOldState(oldState);

                byte current = ReadByte(handle.Value, checked(target.ModuleBase + scan.Rva));
                if (current != scan.OriginalByte)
                    throw new InvalidOperationException("Explorer 中的 shell32.dll 与磁盘版本不一致，无法安全匹配目标函数。");
                if (current == ReturnInstruction)
                    throw new InvalidOperationException("目标函数已经以 RET 指令开头；没有新的更改可供此工具恢复。");

                PatchState state = new PatchState();
                state.ProcessId = target.ProcessId;
                state.ProcessStartTicks = target.ProcessStartTicks;
                state.Rva = scan.Rva;
                state.OriginalByte = current;
                SaveState(state);

                try
                {
                    WriteByte(handle.Value, checked(target.ModuleBase + scan.Rva), ReturnInstruction);
                    if (ReadByte(handle.Value, checked(target.ModuleBase + scan.Rva)) != ReturnInstruction)
                        throw new InvalidOperationException("写入后校验失败；请点击“恢复”或重新登录 Windows。");
                }
                catch
                {
                    try
                    {
                        if (ReadByte(handle.Value, checked(target.ModuleBase + scan.Rva)) == ReturnInstruction)
                            WriteByte(handle.Value, checked(target.ModuleBase + scan.Rva), current);
                        DeleteState();
                    }
                    catch { }
                    throw;
                }
            }

            RefreshDesktop();
            return "已隐藏预览版桌面水印。此设置只影响当前 Explorer 会话；可点击“恢复”，或重启 Explorer 后自动恢复。";
        }

        internal static string SelfCheck()
        {
            ScanResult scan = Scan();
            IntPtr memory = NativeMethods.VirtualAlloc(IntPtr.Zero, new UIntPtr(4096), 0x3000, PageExecuteReadWrite);
            if (memory == IntPtr.Zero)
                throw new InvalidOperationException("无法分配临时自检内存。Windows 错误：" + Marshal.GetLastWin32Error());

            const byte original = 0x90;
            Marshal.WriteByte(memory, original);
            ExplorerTarget target = new ExplorerTarget();
            target.ProcessId = Process.GetCurrentProcess().Id;
            target.ProcessStartTicks = Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks;

            try
            {
                using (SafeProcessHandle handle = OpenExplorer(target))
                {
                    if (ReadByte(handle.Value, memory.ToInt64()) != original)
                        throw new InvalidOperationException("自检内存的初始值校验失败。");
                    WriteByte(handle.Value, memory.ToInt64(), ReturnInstruction);
                    if (ReadByte(handle.Value, memory.ToInt64()) != ReturnInstruction)
                        throw new InvalidOperationException("自检写入校验失败。");
                    WriteByte(handle.Value, memory.ToInt64(), original);
                    if (ReadByte(handle.Value, memory.ToInt64()) != original)
                        throw new InvalidOperationException("自检恢复校验失败。");
                }
            }
            finally
            {
                NativeMethods.VirtualFree(memory, UIntPtr.Zero, 0x8000);
            }

            return String.Format("自检通过：构建扫描定位到 RVA 0x{0:X}；临时内存的一字节写入和恢复均通过。Explorer 未被修改。", scan.Rva);
        }

        internal static string Restore()
        {
            PatchState state = ReadState();
            if (state == null)
                return "没有找到当前会话的备份。若 Explorer 已重启，原补丁已自动消失。";

            ExplorerTarget target = FindStateTarget(state);
            if (target == null)
                return "Explorer 已重启或目标已卸载；内存补丁已随旧会话消失。";

            using (SafeProcessHandle handle = OpenExplorer(target))
            {
                long address = checked(target.ModuleBase + state.Rva);
                byte current = ReadByte(handle.Value, address);
                if (current == state.OriginalByte)
                {
                    DeleteState();
                    return "当前状态已是原样，无需恢复。";
                }
                if (current != ReturnInstruction)
                    throw new InvalidOperationException("目标字节与本工具记录不符。为避免覆盖其他修改，已停止恢复并保留备份记录。");

                WriteByte(handle.Value, address, state.OriginalByte);
                if (ReadByte(handle.Value, address) != state.OriginalByte)
                    throw new InvalidOperationException("恢复后校验失败。请重新登录 Windows 以还原 Explorer。");
            }

            DeleteState();
            RefreshDesktop();
            return "已恢复 Explorer 中的原始字节，预览版水印会在桌面重绘后重新出现。";
        }

        private static ScanResult FindWatermarkFunction(byte[] image)
        {
            List<PeSection> sections = ReadSections(image);
            uint iatRva = FindSetTextColorIatRva(image, sections);
            if (iatRva == 0)
                throw new InvalidOperationException("shell32.dll 未导入 GDI32!SetTextColor；此构建暂不支持。");

            List<FunctionCandidate> candidates = new List<FunctionCandidate>();
            foreach (PeSection section in sections)
            {
                if ((section.Characteristics & 0x20000000) == 0)
                    continue;

                long start = section.RawOffset;
                long end = Math.Min((long)section.RawOffset + section.RawSize, image.Length);
                if (start < 0 || end < start + 6)
                    continue;

                for (long pos = start; pos <= end - 6; pos++)
                {
                    if (image[pos] != 0xFF || image[pos + 1] != 0x15)
                        continue;

                    int displacement = BitConverter.ToInt32(image, checked((int)pos + 2));
                    uint callRva = checked(section.VirtualAddress + (uint)(pos - start));
                    long computedTarget = (long)callRva + 6L + displacement;
                    if (computedTarget != iatRva || !HasWhiteColorArgument(image, checked((int)pos)))
                        continue;

                    uint? functionRva = FindFunctionStart(image, sections, callRva);
                    if (!functionRva.HasValue)
                        continue;

                    bool seen = false;
                    foreach (FunctionCandidate candidate in candidates)
                    {
                        if (candidate.FunctionRva == functionRva.Value)
                        {
                            seen = true;
                            break;
                        }
                    }
                    if (!seen)
                    {
                        FunctionCandidate candidate = new FunctionCandidate();
                        candidate.FunctionRva = functionRva.Value;
                        candidates.Add(candidate);
                    }
                }
            }

            if (candidates.Count == 0)
                throw new InvalidOperationException("未能在 shell32.dll 中找到预览水印绘制函数。此构建可能使用了不同实现。");
            if (candidates.Count != 1)
                throw new InvalidOperationException(String.Format("扫描结果不唯一（{0} 个候选函数），为避免修改错误代码，已停止。", candidates.Count));

            uint rva = candidates[0].FunctionRva;
            int fileOffset = RvaToFileOffset(rva, sections, image.Length);
            if (fileOffset < 0)
                throw new InvalidOperationException("目标函数地址无效，已停止。");

            ScanResult result = new ScanResult();
            result.Rva = rva;
            result.FileOffset = fileOffset;
            result.OriginalByte = image[fileOffset];
            return result;
        }

        private static List<PeSection> ReadSections(byte[] image)
        {
            if (image == null || image.Length < 0x40 || image[0] != (byte)'M' || image[1] != (byte)'Z')
                throw new InvalidOperationException("shell32.dll 不是有效的 Windows PE 文件。");

            uint peOffsetValue = ReadU32(image, 0x3C);
            if (peOffsetValue > image.Length - 24)
                throw new InvalidOperationException("PE 标头损坏，已停止扫描。");
            int peOffset = checked((int)peOffsetValue);
            if (ReadU32(image, peOffset) != 0x00004550)
                throw new InvalidOperationException("PE 标记无效，已停止扫描。");

            int sectionCount = ReadU16(image, peOffset + 6);
            int optionalSize = ReadU16(image, peOffset + 20);
            int optional = peOffset + 24;
            if (optionalSize < 144 || optional + optionalSize > image.Length || ReadU16(image, optional) != 0x20B)
                throw new InvalidOperationException("此工具需要有效的 x64 PE32+ shell32.dll。");

            int table = optional + optionalSize;
            if ((long)table + (long)sectionCount * 40 > image.Length)
                throw new InvalidOperationException("PE 节表不完整，已停止扫描。");

            List<PeSection> sections = new List<PeSection>();
            for (int i = 0; i < sectionCount; i++)
            {
                int offset = table + i * 40;
                PeSection section = new PeSection();
                section.VirtualSize = ReadU32(image, offset + 8);
                section.VirtualAddress = ReadU32(image, offset + 12);
                section.RawSize = ReadU32(image, offset + 16);
                section.RawOffset = ReadU32(image, offset + 20);
                section.Characteristics = ReadU32(image, offset + 36);
                if ((ulong)section.RawOffset + section.RawSize > (ulong)image.Length)
                    section.RawSize = section.RawOffset < image.Length ? (uint)(image.Length - section.RawOffset) : 0;
                sections.Add(section);
            }
            return sections;
        }

        private static uint FindSetTextColorIatRva(byte[] image, List<PeSection> sections)
        {
            uint peOffset = ReadU32(image, 0x3C);
            int optional = checked((int)peOffset) + 24;
            uint importRva = ReadU32(image, optional + 120);
            if (importRva == 0)
                return 0;
            int descriptor = RvaToFileOffset(importRva, sections, image.Length);
            if (descriptor < 0)
                return 0;

            for (int count = 0; count < 2048 && descriptor + 20 <= image.Length; count++, descriptor += 20)
            {
                uint originalThunk = ReadU32(image, descriptor);
                uint nameRva = ReadU32(image, descriptor + 12);
                uint firstThunk = ReadU32(image, descriptor + 16);
                if (originalThunk == 0 && nameRva == 0 && firstThunk == 0)
                    break;

                int nameOffset = RvaToFileOffset(nameRva, sections, image.Length);
                if (nameOffset < 0)
                    continue;
                string dllName = ReadAsciiZ(image, nameOffset);
                if (!String.Equals(dllName, "GDI32.dll", StringComparison.OrdinalIgnoreCase) &&
                    !String.Equals(dllName, "GDI32FULL.dll", StringComparison.OrdinalIgnoreCase))
                    continue;

                uint thunkRva = originalThunk == 0 ? firstThunk : originalThunk;
                int thunkOffset = RvaToFileOffset(thunkRva, sections, image.Length);
                if (thunkOffset < 0)
                    continue;
                for (int index = 0; index < 8192 && thunkOffset + index * 8 + 8 <= image.Length; index++)
                {
                    ulong value = ReadU64(image, thunkOffset + index * 8);
                    if (value == 0)
                        break;
                    if ((value & 0x8000000000000000UL) != 0)
                        continue;
                    if (value > UInt32.MaxValue)
                        continue;
                    int importByName = RvaToFileOffset((uint)value, sections, image.Length);
                    if (importByName < 0 || importByName + 2 >= image.Length)
                        continue;
                    string function = ReadAsciiZ(image, importByName + 2);
                    if (function == "SetTextColor")
                    {
                        ulong slot = (ulong)firstThunk + (ulong)index * 8UL;
                        return slot <= UInt32.MaxValue ? (uint)slot : 0;
                    }
                }
            }
            return 0;
        }

        private static bool HasWhiteColorArgument(byte[] image, int callOffset)
        {
            int begin = Math.Max(0, callOffset - 40);
            byte[] edx = new byte[] { 0xBA, 0xFF, 0xFF, 0xFF, 0x00 };
            byte[] r8d = new byte[] { 0x41, 0xB8, 0xFF, 0xFF, 0xFF, 0x00 };
            return ContainsPattern(image, begin, callOffset, edx) || ContainsPattern(image, begin, callOffset, r8d);
        }

        private static bool ContainsPattern(byte[] image, int begin, int end, byte[] pattern)
        {
            for (int i = begin; i <= end - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (image[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                    return true;
            }
            return false;
        }

        private static uint? FindFunctionStart(byte[] image, List<PeSection> sections, uint callRva)
        {
            uint peOffset = ReadU32(image, 0x3C);
            int optional = checked((int)peOffset) + 24;
            uint exceptionRva = ReadU32(image, optional + 136);
            uint exceptionSize = ReadU32(image, optional + 140);
            if (exceptionRva == 0 || exceptionSize < 12)
                return null;

            int exceptionOffset = RvaToFileOffset(exceptionRva, sections, image.Length);
            if (exceptionOffset < 0)
                return null;
            uint count = Math.Min(exceptionSize / 12, (uint)((image.Length - exceptionOffset) / 12));
            for (uint i = 0; i < count; i++)
            {
                int entry = checked(exceptionOffset + (int)i * 12);
                uint begin = ReadU32(image, entry);
                uint end = ReadU32(image, entry + 4);
                if (begin <= callRva && callRva < end)
                    return begin;
            }
            return null;
        }

        private static int RvaToFileOffset(uint rva, List<PeSection> sections, int imageLength)
        {
            foreach (PeSection section in sections)
            {
                ulong span = Math.Max(section.VirtualSize, section.RawSize);
                ulong start = section.VirtualAddress;
                if ((ulong)rva < start || (ulong)rva >= start + span)
                    continue;
                ulong delta = (ulong)rva - start;
                if (delta >= section.RawSize)
                    return -1;
                ulong offset = (ulong)section.RawOffset + delta;
                if (offset >= (ulong)imageLength)
                    return -1;
                return (int)offset;
            }
            return -1;
        }

        private static string ReadAsciiZ(byte[] bytes, int offset)
        {
            if (offset < 0 || offset >= bytes.Length)
                return String.Empty;
            int end = offset;
            while (end < bytes.Length && bytes[end] != 0)
                end++;
            return Encoding.ASCII.GetString(bytes, offset, end - offset);
        }

        private static ushort ReadU16(byte[] bytes, int offset)
        {
            if (offset < 0 || offset + 2 > bytes.Length)
                throw new InvalidOperationException("PE 数据不完整。");
            return BitConverter.ToUInt16(bytes, offset);
        }

        private static uint ReadU32(byte[] bytes, int offset)
        {
            if (offset < 0 || offset + 4 > bytes.Length)
                throw new InvalidOperationException("PE 数据不完整。");
            return BitConverter.ToUInt32(bytes, offset);
        }

        private static ulong ReadU64(byte[] bytes, int offset)
        {
            if (offset < 0 || offset + 8 > bytes.Length)
                throw new InvalidOperationException("PE 数据不完整。");
            return BitConverter.ToUInt64(bytes, offset);
        }

        private static ExplorerTarget FindExplorerTarget(uint rva)
        {
            int session = Process.GetCurrentProcess().SessionId;
            Process[] processes = Process.GetProcessesByName("explorer");
            List<ExplorerTarget> matches = new List<ExplorerTarget>();
            try
            {
                foreach (Process process in processes)
                {
                    try
                    {
                        ExplorerTarget target = FindTargetInProcess(process, rva, session);
                        if (target != null)
                            matches.Add(target);
                    }
                    catch
                    {
                        // A stale or inaccessible Explorer process is not a target.
                    }
                }
            }
            finally
            {
                foreach (Process process in processes)
                    process.Dispose();
            }

            if (matches.Count == 0)
                throw new InvalidOperationException("当前登录会话中没有找到加载 shell32.dll 的 Explorer。");
            if (matches.Count != 1)
                throw new InvalidOperationException("当前会话中找到多个 Explorer 目标进程；为避免修改错误进程，已停止。");
            return matches[0];
        }

        private static ExplorerTarget FindStateTarget(PatchState state)
        {
            Process process;
            try
            {
                process = Process.GetProcessById(state.ProcessId);
            }
            catch (ArgumentException)
            {
                DeleteState();
                return null;
            }

            using (process)
            {
                try
                {
                    if (process.StartTime.ToUniversalTime().Ticks != state.ProcessStartTicks ||
                        process.SessionId != Process.GetCurrentProcess().SessionId)
                    {
                        DeleteState();
                        return null;
                    }
                    ExplorerTarget target = FindTargetInProcess(process, state.Rva, process.SessionId);
                    if (target == null)
                        DeleteState();
                    return target;
                }
                catch (InvalidOperationException)
                {
                    // The original process exited while its metadata was being read.
                    DeleteState();
                    return null;
                }
            }
        }

        private static ExplorerTarget FindTargetInProcess(Process process, uint rva, int session)
        {
            if (process.SessionId != session)
                return null;
            foreach (ProcessModule module in process.Modules)
            {
                if (!String.Equals(module.ModuleName, "shell32.dll", StringComparison.OrdinalIgnoreCase))
                    continue;
                string modulePath = Path.GetFullPath(module.FileName);
                if (!String.Equals(modulePath, Path.GetFullPath(Shell32Path), StringComparison.OrdinalIgnoreCase))
                    continue;
                if ((ulong)rva >= (ulong)module.ModuleMemorySize)
                    return null;

                ExplorerTarget target = new ExplorerTarget();
                target.ProcessId = process.Id;
                target.ProcessStartTicks = process.StartTime.ToUniversalTime().Ticks;
                target.ModuleBase = module.BaseAddress.ToInt64();
                target.ModuleSize = module.ModuleMemorySize;
                return target;
            }
            return null;
        }

        private static SafeProcessHandle OpenExplorer(ExplorerTarget target)
        {
            IntPtr handle = NativeMethods.OpenProcess(
                ProcessVmOperation | ProcessVmRead | ProcessVmWrite | ProcessQueryInformation,
                false, target.ProcessId);
            if (handle == IntPtr.Zero)
                throw new InvalidOperationException("无法打开 Explorer 进程。请确认该进程属于当前账户并重试。Windows 错误：" + Marshal.GetLastWin32Error());
            SafeProcessHandle safeHandle = new SafeProcessHandle(handle);
            try
            {
                if (NativeMethods.GetProcessId(handle) != (uint)target.ProcessId)
                    throw new InvalidOperationException("Explorer 进程标识已变化，已停止以避免修改错误进程。");
                using (Process process = Process.GetProcessById(target.ProcessId))
                {
                    if (process.StartTime.ToUniversalTime().Ticks != target.ProcessStartTicks)
                        throw new InvalidOperationException("Explorer 已重启，目标进程已变化。");
                }
                return safeHandle;
            }
            catch
            {
                safeHandle.Dispose();
                throw;
            }
        }

        private static byte ReadByte(IntPtr process, long address)
        {
            byte[] data = new byte[1];
            UIntPtr read;
            if (!NativeMethods.ReadProcessMemory(process, new IntPtr(address), data, new UIntPtr(1), out read) || read.ToUInt64() != 1)
                throw new InvalidOperationException("无法读取 Explorer 的目标代码字节。Windows 错误：" + Marshal.GetLastWin32Error());
            return data[0];
        }

        private static void WriteByte(IntPtr process, long address, byte value)
        {
            IntPtr location = new IntPtr(address);
            uint oldProtection;
            if (!NativeMethods.VirtualProtectEx(process, location, new UIntPtr(1), PageExecuteReadWrite, out oldProtection))
                throw new InvalidOperationException("无法临时更改 Explorer 内存页保护。Windows 错误：" + Marshal.GetLastWin32Error());

            Exception writeError = null;
            try
            {
                UIntPtr written;
                if (!NativeMethods.WriteProcessMemory(process, location, new byte[] { value }, new UIntPtr(1), out written) || written.ToUInt64() != 1)
                    writeError = new InvalidOperationException("无法写入 Explorer 目标字节。Windows 错误：" + Marshal.GetLastWin32Error());
                else
                    NativeMethods.FlushInstructionCache(process, location, new UIntPtr(1));
            }
            finally
            {
                uint ignored;
                if (!NativeMethods.VirtualProtectEx(process, location, new UIntPtr(1), oldProtection, out ignored) && writeError == null)
                    writeError = new InvalidOperationException("代码写入成功，但无法恢复原内存页保护。请重新登录 Windows。Windows 错误：" + Marshal.GetLastWin32Error());
            }

            if (writeError != null)
                throw writeError;
        }

        private static void HandleOldState(PatchState oldState)
        {
            ExplorerTarget target = FindStateTarget(oldState);
            if (target == null)
                return;

            using (SafeProcessHandle handle = OpenExplorer(target))
            {
                byte current = ReadByte(handle.Value, checked(target.ModuleBase + oldState.Rva));
                if (current == ReturnInstruction)
                    throw new InvalidOperationException("已有同一会话的水印补丁记录；请先点击“恢复”。");
                if (current != oldState.OriginalByte)
                    throw new InvalidOperationException("已有补丁备份，但 Explorer 目标字节已改变。为保留恢复记录，已停止应用。");
                DeleteState();
            }
        }

        private static PatchState ReadState()
        {
            if (!File.Exists(StatePath))
                return null;
            string[] lines = File.ReadAllLines(StatePath);
            if (lines.Length != 4)
                throw new InvalidOperationException("恢复备份文件格式无效。请勿手动删除；重新登录 Windows 会清除本次内存补丁。");

            PatchState state = new PatchState();
            if (!Int32.TryParse(lines[0], out state.ProcessId) ||
                !Int64.TryParse(lines[1], out state.ProcessStartTicks) ||
                !UInt32.TryParse(lines[2], System.Globalization.NumberStyles.HexNumber, null, out state.Rva))
                throw new InvalidOperationException("恢复备份文件内容无效。请重新登录 Windows 后再试。");
            byte original;
            if (!Byte.TryParse(lines[3], System.Globalization.NumberStyles.HexNumber, null, out original))
                throw new InvalidOperationException("恢复备份字节无效。请重新登录 Windows 后再试。");
            state.OriginalByte = original;
            return state;
        }

        private static void SaveState(PatchState state)
        {
            string folder = Path.GetDirectoryName(StatePath);
            Directory.CreateDirectory(folder);
            string temporary = StatePath + ".tmp";
            string[] lines = new string[]
            {
                state.ProcessId.ToString(),
                state.ProcessStartTicks.ToString(),
                state.Rva.ToString("X"),
                state.OriginalByte.ToString("X2")
            };
            File.WriteAllLines(temporary, lines, Encoding.ASCII);
            if (File.Exists(StatePath))
                File.Delete(StatePath);
            File.Move(temporary, StatePath);
        }

        private static void DeleteState()
        {
            if (File.Exists(StatePath))
                File.Delete(StatePath);
            string temporary = StatePath + ".tmp";
            if (File.Exists(temporary))
                File.Delete(temporary);
        }

        private static void RefreshDesktop()
        {
            uint flags = RedrawInvalidate | RedrawErase | RedrawAllChildren | RedrawUpdateNow;
            IntPtr shell = NativeMethods.GetShellWindow();
            if (shell != IntPtr.Zero)
                NativeMethods.RedrawWindow(shell, IntPtr.Zero, IntPtr.Zero, flags);
            NativeMethods.RedrawWindow(NativeMethods.GetDesktopWindow(), IntPtr.Zero, IntPtr.Zero, flags);
        }
    }

    internal sealed class SafeProcessHandle : IDisposable
    {
        internal readonly IntPtr Value;

        internal SafeProcessHandle(IntPtr value)
        {
            Value = value;
        }

        public void Dispose()
        {
            if (Value != IntPtr.Zero)
                NativeMethods.CloseHandle(Value);
        }
    }

    internal static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint GetProcessId(IntPtr process);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocationType, uint protection);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool VirtualFree(IntPtr address, UIntPtr size, uint freeType);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ReadProcessMemory(IntPtr process, IntPtr address, [Out] byte[] buffer, UIntPtr size, out UIntPtr bytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool WriteProcessMemory(IntPtr process, IntPtr address, byte[] buffer, UIntPtr size, out UIntPtr bytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool VirtualProtectEx(IntPtr process, IntPtr address, UIntPtr size, uint newProtection, out uint oldProtection);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr handle);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        internal static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RedrawWindow(IntPtr window, IntPtr updateRectangle, IntPtr updateRegion, uint flags);
    }
}
