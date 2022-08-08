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
using System.Collections.Generic;
using MonoMod.Utils;
using Newtonsoft.Json;

namespace Celeste.Mod.CelesteRTDataProvider
{

    public class CelesteRTDataProviderModule : EverestModule
    {
        public static CelesteRTDataProviderModule Instance { get; private set; }

        public override Type SettingsType => typeof(CelesteRTDataProviderModuleSettings);
        public static CelesteRTDataProviderModuleSettings Settings => (CelesteRTDataProviderModuleSettings)Instance._Settings;

        public override Type SessionType => typeof(CelesteRTDataProviderModuleSession);
        public static CelesteRTDataProviderModuleSession Session => (CelesteRTDataProviderModuleSession)Instance._Session;

        public websocketServer server = new websocketServer();
        public Task serverInstance = null;
        
        public Dictionary<String, Object> gameFeed = new Dictionary<String, Object>();
        public bool updateRoomBerries = true;

        public CelesteRTDataProviderModule()
        {
            Instance = this;
        }

        public void clientUpdate()
        {
            // Send JSON with new updated stats to all connected clients
            // New in v0.2.0: use Newtonsoft.Json module to convert to string.
            try
            {
                server.sendMessage(JsonConvert.SerializeObject(gameFeed));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CelesteRTDataMod] [clientUpdate] [error] an error has occured with clientUpdate: {ex}");
            }
        }

        private void handlePlayerDeath(Player player)
        {

            gameFeed["deathCount"] = Int32.Parse(gameFeed["deathCount"].ToString()) + 1;
            if (updateRoomBerries)
            {
                updateRoomBerries = false;
            }
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
                    int id = 1;
                    DynamicData strawbDyn = new DynamicData((follower.Entity as Strawberry));
                    if (!strawbDyn.Get<bool>("Golden") && !strawbDyn.Get<bool>("Moon") && !strawbDyn.Get<bool>("isGhostBerry"))
                    {
                        id = 2;
                    }
                    if (strawbDyn.Get<bool>("Moon"))
                    {
                        id = 3;
                    }
                    if (strawbDyn.Get<bool>("Golden"))
                    {
                        id = 4;
                    }
                    addListing(id, "berryTrain");
                    clientUpdate();
                    break;
            }
            orig(self, follower);
        }

        public void addListing(int id, string name)
        {
            List<int> list = (List<int>)gameFeed[name];
            list.Add(id);
            gameFeed[name] = list;
        }

        public void removeListing(int id, string name)
        {
            try
            {
                List<int> list = (List<int>)gameFeed[name];
                list.Remove(id);
                gameFeed[name] = list;
            } catch
            {
                Console.WriteLine("err: removeListing failed. Continuing...");
            }
            
        }

        private void Leader_LoseFollower(On.Celeste.Leader.orig_LoseFollower orig, Leader self, Follower follower)
        {
            switch (follower.Entity)
            {
                case Strawberry:
                    int id = 1;
                    DynamicData strawbDyn = new DynamicData((follower.Entity as Strawberry));
                    if (!strawbDyn.Get<bool>("Golden") && !strawbDyn.Get<bool>("Moon") && !strawbDyn.Get<bool>("isGhostBerry"))
                    {
                        id = 2;
                    }
                    if (strawbDyn.Get<bool>("Moon"))
                    {
                        id = 3;
                    }
                    if (strawbDyn.Get<bool>("Golden"))
                    {
                        id = 4;
                    }
                    removeListing(id, "berryTrain");
                    clientUpdate();
                    break;
            }
            orig(self, follower);
        }

        private void Leader_LoseFollowers(On.Celeste.Leader.orig_LoseFollowers orig, Leader self)
        {
            gameFeed["berryTrain"] = new List<int>();
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
            if (updateRoomBerries)
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
                addListing(Int32.Parse(id), "roomBerries");
                clientUpdate();
            }
            orig(self, scene);
        }

        private void Level_OnTransitionTo(Level level, LevelData next, Microsoft.Xna.Framework.Vector2 direction)
        {
            gameFeed["roomBerries"] = new List<int>();
            updateRoomBerries = true;
            clientUpdate();
        }
        private void resetFeed()
        {
            // Init all variables
            gameFeed["strawberryCount"] = 0;
            gameFeed["deathCount"] = 0;
            gameFeed["roomBerries"] = new List<int>();
            gameFeed["berryTrain"] = new List<int>();
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
            Everest.Events.Level.OnTransitionTo += Level_OnTransitionTo;
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
            Everest.Events.Level.OnTransitionTo -= Level_OnTransitionTo;
            serverInstance.Dispose();
        }
    }
}