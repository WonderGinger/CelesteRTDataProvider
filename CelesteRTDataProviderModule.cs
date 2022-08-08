/*THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.*/

using System;
using System.Threading.Tasks;
using System.Collections;
using FMOD.Studio;
using System.Collections.Generic;
using MonoMod.Utils;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Text;
using System.Runtime.Serialization;

namespace Celeste.Mod.CelesteRTDataProvider
{

    // New in 0.2.0: Copied and pasted stackoverflow code for actually usable json array
    // Source: https://stackoverflow.com/q/4861138

    [Serializable]
    public class Json<K, V> : ISerializable
    {
        Dictionary<K, V> dict = new Dictionary<K, V>();

        public Json() { }

        protected Json(SerializationInfo info, StreamingContext context)
        {
            throw new NotImplementedException();
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            foreach (K key in dict.Keys)
            {
                info.AddValue(key.ToString(), dict[key]);
            }
        }

        public void Add(K key, V value)
        {
            dict.Add(key, value);
        }

        public V this[K index]
        {
            set { dict[index] = value; }
            get { return dict[index]; }
        }
    }

    public class CelesteRTDataProviderModule : EverestModule
    {
        public static CelesteRTDataProviderModule Instance { get; private set; }

        public override Type SettingsType => typeof(CelesteRTDataProviderModuleSettings);
        public static CelesteRTDataProviderModuleSettings Settings => (CelesteRTDataProviderModuleSettings)Instance._Settings;

        public override Type SessionType => typeof(CelesteRTDataProviderModuleSession);
        public static CelesteRTDataProviderModuleSession Session => (CelesteRTDataProviderModuleSession)Instance._Session;

        public websocketServer server = new websocketServer();
        public Task serverInstance = null;

        // Still have to have the lists as lists, unfortunately :p
        public List<int> roomBerries = new List<int>();
        public List<int> berryTrain = new List<int>();
        
        public Json<String, Object> gameFeed = new Json<String, Object>();

        public CelesteRTDataProviderModule()
        {
            Instance = this;
        }

        public static String Serialize(Object data)
        {
            var serializer = new DataContractJsonSerializer(data.GetType());
            var ms = new MemoryStream();
            serializer.WriteObject(ms, data);
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        string intListToArray(List<int> list)
        {
            return $"[{string.Join(",", list)}]";
        }

        public void clientUpdate()
        {
            // Send JSON with new updated stats to all connected clients
            try
            {
                server.sendMessage(Serialize(gameFeed));
                server.sendMessage("fuck shit fuck");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CelesteRTDataMod] [clientUpdate] [error] an error has occured with clientUpdate: {ex}");
            }
        }

        private void handlePlayerDeath(Player player)
        {
            gameFeed["deathCount"] = Int32.Parse(gameFeed["deathCount"].ToString()) + 1;
            clientUpdate();  
        }

        private IEnumerator onStrawberryCollectRoutine(On.Celeste.Strawberry.orig_CollectRoutine orig, Strawberry self, int collectIndex)
        {
            // Handle new strawberries.
            // Source: https://github.com/EverestAPI/CelesteCollabUtils2/blob/master/Entities/StrawberryHooks.cs

            gameFeed["strawberryCount"] = Int32.Parse(gameFeed["strawberryCount"].ToString())+1;
            clientUpdate();

            IEnumerator origEnum = orig(self, collectIndex);
            while (origEnum.MoveNext())
            {
                yield return origEnum.Current;
            }
        }

        private void Leader_GainFollower(On.Celeste.Leader.orig_GainFollower orig, Leader self, Follower follower)
        {
            switch (follower.Entity)
            {
                case Strawberry:
                    int id = 0;
                    DynamicData strawbDyn = new DynamicData((follower.Entity as Strawberry));
                    if (strawbDyn.Get<bool>("isGhostBerry"))
                    {
                        id = 0;
                    }
                    if (!strawbDyn.Get<bool>("Golden") && !strawbDyn.Get<bool>("Moon") && !strawbDyn.Get<bool>("isGhostBerry"))
                    {
                        id = 1;
                    }
                    if (strawbDyn.Get<bool>("Moon"))
                    {
                        id = 2;
                    }
                    if (strawbDyn.Get<bool>("Golden"))
                    {
                        id = 3;
                    }
                    addListing(id, berryTrain, "berryTrain");
                    clientUpdate();
                    break;
            }
            orig(self, follower);
        }

        public void addListing(int id, List<int> list, string name)
        {
            // Add <int> id to List<int> list.
            // Use <string> name to add to json feed.

            list.Add(id);
            gameFeed[name] = intListToArray(list);
        }

        public void removeListing(int id, List<int> list, string name)
        {
            // Remove <int> id from List<int> list.
            // Use <string> name to add to json feed.

            try
            {
                list.Remove(id);
                gameFeed[name] = intListToArray(list);
            } catch
            {
                Console.WriteLine("err: removeListing failed. Continuing...");
            }
            
        }

        public void resetListing(List<int> list, string name)
        {
            list = new List<int>();
            gameFeed[name] = intListToArray(list);   
        }

        private void Leader_LoseFollower(On.Celeste.Leader.orig_LoseFollower orig, Leader self, Follower follower)
        {
            switch (follower.Entity)
            {
                case Strawberry:
                    int id = 0;
                    DynamicData strawbDyn = new DynamicData((follower.Entity as Strawberry));
                    if (!strawbDyn.Get<bool>("Golden") && !strawbDyn.Get<bool>("Moon") && !strawbDyn.Get<bool>("isGhostBerry"))
                    {
                        id = 1;
                    }
                    if (strawbDyn.Get<bool>("Moon"))
                    {
                        id = 2;
                    }
                    if (strawbDyn.Get<bool>("Golden"))
                    {
                        id = 3;
                    }
                    removeListing(id, berryTrain, "berryTrain");
                    clientUpdate();
                    break;
            }
            orig(self, follower);
        }

        private void Leader_LoseFollowers(On.Celeste.Leader.orig_LoseFollowers orig, Leader self)
        {
            resetListing(berryTrain, "berryTrain");
            clientUpdate();

            orig(self);
        }


        private void AreaComplete_End(On.Celeste.AreaComplete.orig_End orig, AreaComplete self)
        {
            // Reset all stats on area completed
            resetFeed();
            clientUpdate();

            // Yield original functionality
            orig(self);
        }

        private void Strawberry_Added(On.Celeste.Strawberry.orig_Added orig, Strawberry self, Monocle.Scene scene)
        {
            DynamicData strawbDyn = new DynamicData(self);
            string id = "1";
            if (!strawbDyn.Get<bool>("Golden") && !strawbDyn.Get<bool>("Moon") && !strawbDyn.Get<bool>("isGhostBerry"))
            {
                id = "2";
            }
            if (strawbDyn.Get<bool>("Moon"))
            {
                id = "3";
            }
            if (strawbDyn.Get<bool>("Golden"))
            {
                id = "4";
            }
            if (strawbDyn.Get<bool>("Winged"))
            {
                id += "1";
            }
            addListing(Int32.Parse(id), roomBerries, "roomBerries");
            clientUpdate();
            orig(self, scene);
        }
        private void resetFeed()
        {
            // Init misc variables
            gameFeed["strawberryCount"] = 0;
            gameFeed["deathCount"] = 0;

            // Init room berries
            roomBerries = new List<int>();
            gameFeed["roomBerries"] = "[]";

            // Init berry train
            berryTrain = new List<int>();
            gameFeed["berryTrain"] = "[]";
        }

        public override void Load()
        {
            resetFeed();
            serverInstance = Task.Run(() => server.startServer(Settings.serverPort));
            Everest.Events.Player.OnDie += handlePlayerDeath;
            On.Celeste.Strawberry.CollectRoutine += onStrawberryCollectRoutine;
            On.Celeste.AreaComplete.End += AreaComplete_End;
            On.Celeste.Leader.GainFollower += Leader_GainFollower;
            On.Celeste.Leader.LoseFollower += Leader_LoseFollower;
            On.Celeste.Leader.LoseFollowers += Leader_LoseFollowers;
            On.Celeste.Strawberry.Added += Strawberry_Added;
        }

        public override void Unload()
        {
            Everest.Events.Player.OnDie -= handlePlayerDeath;
            On.Celeste.Strawberry.CollectRoutine -= onStrawberryCollectRoutine;
            On.Celeste.AreaComplete.End -= AreaComplete_End;
            On.Celeste.Leader.GainFollower -= Leader_GainFollower;
            On.Celeste.Leader.LoseFollower -= Leader_LoseFollower;
            On.Celeste.Leader.LoseFollowers -= Leader_LoseFollowers;
            On.Celeste.Strawberry.Added -= Strawberry_Added;
            serverInstance.Dispose();
        }
    }
}