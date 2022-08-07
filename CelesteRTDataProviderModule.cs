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

        public int deathCount = 0;
        public int strawberryCount = 0;
        public List<int> berryTrain = new List<int>();

        public CelesteRTDataProviderModule()
        {
            Instance = this;
        }

        public void clientUpdate()
        {
            // Send JSON with new updated stats to all connected clients
            try
            {
                Console.WriteLine($"ClientUpdate: Sending message to client: {{\"deathCount\": {deathCount}, \"strawberryCount\": {strawberryCount}, \"berryTrain\": [{string.Join(",", berryTrain)}]}}");
                server.sendMessage($"{{\"deathCount\": {deathCount}, \"strawberryCount\": {strawberryCount}, \"berryTrain\": [{string.Join(",", berryTrain)}]}}");
            }
            catch
            {
                Console.WriteLine("No client is connected, dumbass.");
                Console.WriteLine("Who the fuck are you trying to send these messages to???");
            }
        }

        private void handlePlayerDeath(Player player)
        {
            deathCount++;
            clientUpdate();  
        }

        private IEnumerator onStrawberryCollectRoutine(On.Celeste.Strawberry.orig_CollectRoutine orig, Strawberry self, int collectIndex)
        {
            // Handle new strawberries.
            // Source: https://github.com/EverestAPI/CelesteCollabUtils2/blob/master/Entities/StrawberryHooks.cs

            strawberryCount++;
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
                    string id = "0";
                    if (!(follower.Entity as Strawberry).Golden && !(follower.Entity as Strawberry).Moon)
                    {
                        id = "1";
                    } else if ((follower.Entity as Strawberry).Moon)
                    {
                        id = "2";
                    }
                    /*
                    if ((follower.Entity as Strawberry).Winged)
                    {
                        id += "2"; // Implement room overlay in v0.2.0
                    }*/
berryTrain.Add(Int32.Parse(id));
                    clientUpdate();
                    break;
            }
            orig(self, follower);
        }

        private void Leader_LoseFollower(On.Celeste.Leader.orig_LoseFollower orig, Leader self, Follower follower)
        {
            switch (follower.Entity)
            {
                case Strawberry:
                    string id = "0";
                    if (!(follower.Entity as Strawberry).Golden && !(follower.Entity as Strawberry).Moon)
                    {
                        id = "1";
                    }
                    else if ((follower.Entity as Strawberry).Moon)
                    {
                        id = "2";
                    }
                    /*
                    if ((follower.Entity as Strawberry).Winged)
                    {
                        id += "2"; // Implement room overlay in v0.2.0
                    }*/
                    berryTrain.Remove(Int32.Parse(id));
                    clientUpdate();
                    break;
            }
            orig(self, follower);
        }

        private void Leader_LoseFollowers(On.Celeste.Leader.orig_LoseFollowers orig, Leader self)
        {
            berryTrain = new List<int>();
            clientUpdate();
            orig(self);
        }


        private void AreaComplete_End(On.Celeste.AreaComplete.orig_End orig, AreaComplete self)
        {
            // Reset all stats on area completed
            strawberryCount = 0;
            deathCount = 0;
            berryTrain = new List<int>();
            clientUpdate();

            // Yield original functionality
            orig(self);
        }


        public override void Load()
        {
            
            serverInstance = Task.Run(() => server.startServer(Settings.serverPort));
            Everest.Events.Player.OnDie += handlePlayerDeath;
            On.Celeste.Strawberry.CollectRoutine += onStrawberryCollectRoutine;
            On.Celeste.AreaComplete.End += AreaComplete_End;
            On.Celeste.Leader.GainFollower += Leader_GainFollower;
            On.Celeste.Leader.LoseFollower += Leader_LoseFollower;
            On.Celeste.Leader.LoseFollowers += Leader_LoseFollowers;
        }

        public override void Unload()
        {
            Everest.Events.Player.OnDie -= handlePlayerDeath;
            On.Celeste.Strawberry.CollectRoutine -= onStrawberryCollectRoutine;
            On.Celeste.AreaComplete.End -= AreaComplete_End;
            On.Celeste.Leader.GainFollower -= Leader_GainFollower;
            On.Celeste.Leader.LoseFollower -= Leader_LoseFollower;
            On.Celeste.Leader.LoseFollowers -= Leader_LoseFollowers;
            serverInstance.Dispose();
        }
    }
}