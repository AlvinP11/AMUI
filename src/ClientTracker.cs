using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AMUI
{
    enum ClientState { Starting, Joining, InGame, InMenus, Disconnected }

    sealed class RobloxClient
    {
        public int Pid;
        public DateTime StartTimeUtc;
        public long MemoryBytes;
        public IntPtr WindowHandle;

        public string LogPath;
        public long LogPosition;

        public long UserId;
        public long PlaceId;
        public long UniverseId;
        public ClientState State;
        public int DisconnectCode;
    }

    // Finds the running Roblox clients and works out who is playing what by
    // reading each client's log file in %LocalAppData%\Roblox\logs.
    sealed class ClientTracker
    {
        public const string ProcessName = "RobloxPlayerBeta";

        public static readonly string LogFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Roblox\logs");

        // Each pattern is anchored to the line's own [FLog::...] tag, so text a
        // game prints into the log can't pose as one of these lines.
        //
        // The account comes only from the game-join report. Logs also contain
        // an "rbxuid=" from Roblox's browser-tracker cookie, but every client
        // on the PC shares that cookie, so it often names a different account.
        static readonly Regex LaunchingLine = new Regex(
            @"^[^\[]*\[FLog::SingleSurfaceApp\] (launchUGCGameInternal|initiateTeleport)", RegexOptions.Compiled);
        static readonly Regex JoiningLine = new Regex(
            @"^[^\[]*\[FLog::Output\] ! Joining game '[^']*' place (\d+)", RegexOptions.Compiled);
        static readonly Regex JoinedLine = new Regex(
            @"^[^\[]*\[FLog::GameJoinLoadTime\] Report game_join_loadtime:.*?\bplaceid:(\d+).*?\buniverseid:(\d+).*?\buserid:(\d+)",
            RegexOptions.Compiled);
        static readonly Regex LeftLine = new Regex(
            @"^[^\[]*\[FLog::SingleSurfaceApp\] leaveUGCGameInternal", RegexOptions.Compiled);
        static readonly Regex DisconnectLine = new Regex(
            @"^[^\[]*\[FLog::Network\] Disconnection Notification\. Reason: (\d+)", RegexOptions.Compiled);

        const int ReadChunkSize = 1 << 20;

        readonly Dictionary<int, RobloxClient> _clients = new Dictionary<int, RobloxClient>();

        // Oldest first.
        public List<RobloxClient> Clients
        {
            get
            {
                List<RobloxClient> list = new List<RobloxClient>(_clients.Values);
                list.Sort(delegate(RobloxClient a, RobloxClient b) { return a.StartTimeUtc.CompareTo(b.StartTimeUtc); });
                return list;
            }
        }

        public static int ProcessCount()
        {
            Process[] processes = Process.GetProcessesByName(ProcessName);
            foreach (Process p in processes)
                p.Dispose();
            return processes.Length;
        }

        public void Refresh()
        {
            HashSet<int> alive = new HashSet<int>();
            foreach (Process p in Process.GetProcessesByName(ProcessName))
            {
                using (p)
                {
                    alive.Add(p.Id);
                    RobloxClient c;
                    if (!_clients.TryGetValue(p.Id, out c))
                    {
                        c = new RobloxClient();
                        c.Pid = p.Id;
                        c.StartTimeUtc = GetStartTimeUtc(p);
                        _clients[p.Id] = c;
                    }
                    try
                    {
                        c.MemoryBytes = p.WorkingSet64;
                        c.WindowHandle = p.MainWindowHandle;
                    }
                    catch (Exception)
                    {
                        // The client exited while we were looking at it.
                    }
                }
            }

            foreach (int pid in new List<int>(_clients.Keys))
            {
                if (!alive.Contains(pid))
                    _clients.Remove(pid);
            }

            // Oldest first, so earlier clients get first pick of the log files.
            foreach (RobloxClient c in Clients)
            {
                if (c.LogPath == null)
                    c.LogPath = FindLog(c);
                if (c.LogPath != null)
                    ReadNewLines(c);
            }
        }

        static DateTime GetStartTimeUtc(Process p)
        {
            try { return p.StartTime.ToUniversalTime(); }
            catch (Exception) { return DateTime.UtcNow; }
        }

        // Logs don't contain the process ID, but a client creates its log a
        // few seconds after it starts, so match them up by time.
        string FindLog(RobloxClient c)
        {
            if (!Directory.Exists(LogFolder))
                return null;

            HashSet<string> taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (RobloxClient other in _clients.Values)
            {
                if (other.LogPath != null)
                    taken.Add(other.LogPath);
            }

            string best = null;
            double bestGap = double.MaxValue;
            foreach (string path in Directory.GetFiles(LogFolder, "*_Player_*.log"))
            {
                if (path.IndexOf("CrashHandler", StringComparison.OrdinalIgnoreCase) >= 0 || taken.Contains(path))
                    continue;
                double gap = (File.GetCreationTimeUtc(path) - c.StartTimeUtc).TotalSeconds;
                if (gap < -5 || gap > 120)
                    continue;
                if (Math.Abs(gap) < bestGap)
                {
                    best = path;
                    bestGap = Math.Abs(gap);
                }
            }
            return best;
        }

        // Reads whatever the client wrote since last time. Only whole lines are
        // consumed; a half-written last line is picked up on the next pass.
        static void ReadNewLines(RobloxClient c)
        {
            try
            {
                using (FileStream fs = new FileStream(c.LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (fs.Length < c.LogPosition)
                        c.LogPosition = 0;

                    byte[] buffer = new byte[ReadChunkSize];
                    while (c.LogPosition < fs.Length)
                    {
                        fs.Seek(c.LogPosition, SeekOrigin.Begin);
                        int read = fs.Read(buffer, 0, buffer.Length);
                        if (read <= 0)
                            break;

                        int lastNewline = Array.LastIndexOf(buffer, (byte)'\n', read - 1);
                        if (lastNewline < 0)
                        {
                            if (read == buffer.Length)
                            {
                                c.LogPosition += read; // Absurdly long line, skip it.
                                continue;
                            }
                            break;
                        }

                        string text = Encoding.UTF8.GetString(buffer, 0, lastNewline + 1);
                        c.LogPosition += lastNewline + 1;
                        foreach (string line in text.Split('\n'))
                            ParseLine(c, line);
                    }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        static void ParseLine(RobloxClient c, string line)
        {
            if (line.IndexOf("[FLog::", StringComparison.Ordinal) < 0)
                return;

            Match m;
            if (LaunchingLine.IsMatch(line))
            {
                c.State = ClientState.Joining;
            }
            else if (line.IndexOf("! Joining game", StringComparison.Ordinal) >= 0 && (m = JoiningLine.Match(line)).Success)
            {
                long place = ParseId(m.Groups[1].Value, 0);
                if (place != c.PlaceId)
                    c.UniverseId = 0;
                c.PlaceId = place;
                c.State = ClientState.Joining;
            }
            else if (line.IndexOf("game_join_loadtime", StringComparison.Ordinal) >= 0 && (m = JoinedLine.Match(line)).Success)
            {
                c.PlaceId = ParseId(m.Groups[1].Value, c.PlaceId);
                c.UniverseId = ParseId(m.Groups[2].Value, c.UniverseId);
                c.UserId = ParseId(m.Groups[3].Value, c.UserId);
                c.State = ClientState.InGame;
                c.DisconnectCode = 0;
            }
            else if (LeftLine.IsMatch(line))
            {
                c.State = ClientState.InMenus;
                c.PlaceId = 0;
                c.UniverseId = 0;
                c.DisconnectCode = 0;
            }
            else if (c.State == ClientState.InGame && (m = DisconnectLine.Match(line)).Success)
            {
                // Leaving normally logs leaveUGCGameInternal first, so getting
                // here while still in a game means the client was kicked or
                // lost its connection.
                c.State = ClientState.Disconnected;
                c.DisconnectCode = (int)ParseId(m.Groups[1].Value, 0);
            }
        }

        static long ParseId(string digits, long fallback)
        {
            long value;
            return long.TryParse(digits, out value) ? value : fallback;
        }

        public static string DescribeDisconnect(int code)
        {
            switch (code)
            {
                case 267: return "Kicked from the game";
                case 273: return "Account joined from another device";
                case 277: return "Lost connection";
                case 279: return "Couldn't connect";
                default: return "Disconnected";
            }
        }
    }
}
