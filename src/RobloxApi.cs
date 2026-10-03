using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace AMUI
{
    sealed class RobloxUser
    {
        public long Id;
        public string Name;
        public string DisplayName;
        public Image Headshot;
    }

    sealed class RobloxGame
    {
        public long UniverseId;
        public string Name;
        public long RootPlaceId;
        public Image Icon;
    }

    // Looks up names, avatars and game info from Roblox's public web API. No
    // login or cookies; it only ever sends user IDs and game IDs.
    static class RobloxApi
    {
        static readonly object Gate = new object();
        static readonly Dictionary<long, RobloxUser> Users = new Dictionary<long, RobloxUser>();
        static readonly Dictionary<long, RobloxGame> Games = new Dictionary<long, RobloxGame>();

        // Raised on a background thread whenever a lookup finishes.
        public static event Action Changed;

        // Returns what we know so far and starts a lookup the first time an ID
        // is seen. Fields fill in later; listen to Changed.
        public static RobloxUser GetUser(long userId)
        {
            RobloxUser user;
            lock (Gate)
            {
                if (Users.TryGetValue(userId, out user))
                    return user;
                user = new RobloxUser();
                user.Id = userId;
                Users[userId] = user;
            }
            Task.Run(delegate { FetchUser(user); });
            return user;
        }

        public static RobloxGame GetGame(long universeId)
        {
            RobloxGame game;
            lock (Gate)
            {
                if (Games.TryGetValue(universeId, out game))
                    return game;
                game = new RobloxGame();
                game.UniverseId = universeId;
                Games[universeId] = game;
            }
            Task.Run(delegate { FetchGame(game); });
            return game;
        }

        static void FetchUser(RobloxUser user)
        {
            try
            {
                object info = Web.GetJson("https://users.roblox.com/v1/users/" + user.Id);
                user.Name = Json.Str(info, "name");
                user.DisplayName = Json.Str(info, "displayName");
                RaiseChanged();

                user.Headshot = FetchThumbnail("https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds=" + user.Id +
                    "&size=150x150&format=Png&isCircular=false");
                RaiseChanged();
            }
            catch (Exception)
            {
                ForgetLater(Users, user.Id);
            }
        }

        static void FetchGame(RobloxGame game)
        {
            try
            {
                object info = Json.First(Web.GetJson("https://games.roblox.com/v1/games?universeIds=" + game.UniverseId));
                game.Name = Json.Str(info, "name");
                game.RootPlaceId = Json.Long(info, "rootPlaceId");
                RaiseChanged();

                game.Icon = FetchThumbnail("https://thumbnails.roblox.com/v1/games/icons?universeIds=" + game.UniverseId +
                    "&size=150x150&format=Png&isCircular=false");
                RaiseChanged();
            }
            catch (Exception)
            {
                ForgetLater(Games, game.UniverseId);
            }
        }

        // Thumbnails can come back "Pending" while Roblox renders them.
        static Image FetchThumbnail(string url)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                object entry = Json.First(Web.GetJson(url));
                string state = Json.Str(entry, "state");
                if (state == "Completed")
                    return Web.GetImage(Json.Str(entry, "imageUrl"));
                if (state != "Pending")
                    return null;
                Thread.Sleep(1500);
            }
            return null;
        }

        // A failed lookup is retried the next time the ID is asked for, but
        // not straight away.
        static void ForgetLater<T>(Dictionary<long, T> cache, long id)
        {
            Task.Delay(15000).ContinueWith(delegate
            {
                lock (Gate) { cache.Remove(id); }
            });
        }

        static void RaiseChanged()
        {
            Action handler = Changed;
            if (handler != null)
                handler();
        }
    }

    static class Web
    {
        public static object GetJson(string url)
        {
            using (WebClient client = NewClient())
                return new JavaScriptSerializer().DeserializeObject(client.DownloadString(url));
        }

        public static Image GetImage(string url)
        {
            byte[] bytes;
            using (WebClient client = NewClient())
                bytes = client.DownloadData(url);
            using (MemoryStream stream = new MemoryStream(bytes))
            using (Image image = Image.FromStream(stream))
                return new Bitmap(image);
        }

        static WebClient NewClient()
        {
            WebClient client = new WebClient();
            client.Encoding = Encoding.UTF8;
            client.Headers[HttpRequestHeader.UserAgent] = "AMUI/" + Program.Version + " (+" + Program.RepoUrl + ")";
            return client;
        }
    }

    static class Json
    {
        public static object Get(object obj, string key)
        {
            IDictionary<string, object> dict = obj as IDictionary<string, object>;
            object value;
            return dict != null && dict.TryGetValue(key, out value) ? value : null;
        }

        public static string Str(object obj, string key)
        {
            object value = Get(obj, key);
            return value == null ? null : value.ToString();
        }

        public static long Long(object obj, string key)
        {
            object value = Get(obj, key);
            return value == null ? 0 : Convert.ToInt64(value);
        }

        // First item of a {"data": [...]} response.
        public static object First(object response)
        {
            IList data = Get(response, "data") as IList;
            if (data == null || data.Count == 0)
                throw new InvalidDataException("Empty response");
            return data[0];
        }
    }

    sealed class UpdateInfo
    {
        public string Tag;
        public Version Latest;
        public string Url;
        public bool IsNewer { get { return Latest > new Version(Program.Version); } }
    }

    static class UpdateChecker
    {
        public static UpdateInfo GetLatest()
        {
            object release = Web.GetJson("https://api.github.com/repos/AlvinP11/AMUI/releases/latest");
            UpdateInfo info = new UpdateInfo();
            info.Tag = Json.Str(release, "tag_name");
            info.Latest = new Version(info.Tag.TrimStart('v', 'V'));
            info.Url = Json.Str(release, "html_url");
            return info;
        }
    }
}
