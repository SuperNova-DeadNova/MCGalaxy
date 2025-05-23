/*
    Copyright 2012 MCForge
    
    Dual-licensed under the Educational Community License, Version 2.0 and
    the GNU General Public License, Version 3 (the "Licenses"); you may
    not use this file except in compliance with the Licenses. You may
    obtain a copy of the Licenses at
    
    http://www.opensource.org/licenses/ecl2.php
    http://www.gnu.org/licenses/gpl-3.0.html
    
    Unless required by applicable law or agreed to in writing,
    software distributed under the Licenses are distributed on an "AS IS"
    BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
    or implied. See the Licenses for the specific language governing
    permissions and limitations under the Licenses.
 */
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Cache;
using System.Text;
using MCGalaxy.Tasks;

namespace MCGalaxy.Network 
{
    /// <summary> Repeatedly sends a heartbeat request every certain interval to a web server. </summary>
    public abstract class Heartbeat 
    {
        /// <summary> The max number of retries attempted for a request </summary>
        public const int MAX_RETRIES = 3;
        
        /// <summary> List of all heartbeats to pump </summary>
        public static List<Heartbeat> Heartbeats = new List<Heartbeat>();
        
        
        /// <summary> The URL this heartbeat is sent to </summary
        public string URL;
        /// <summary> Salt used for verifying player names </summary>
        public string Salt = "";        
        /// <summary> Initialises data for this heartbeat </summary>
        protected abstract void Init();
        
        /// <summary> Gets the data to be sent for the next heartbeat </summary>
        protected abstract string GetHeartbeatData();
        /// <summary> Called when a heartbeat is about to be sent to the web server </summary>
        protected abstract void OnRequest(HttpWebRequest request);
        /// <summary> Called when a response is received from the web server </summary>
        protected abstract void OnResponse(WebResponse response);
        

        /// <summary> Pumps this heartbeat </summary>
        /// <remarks> i.e. Sends a heartbeat and then reads the response </remarks>
        public void Pump() {
            byte[] data = Encoding.ASCII.GetBytes(GetHeartbeatData());
            Exception lastEx = null;

            for (int i = 0; i < MAX_RETRIES; i++) {
                try {
                    HttpWebRequest req = HttpUtil.CreateRequest(URL);
                    req.Method      = "POST";
                    req.ContentType = "application/x-www-form-urlencoded";
                    req.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
                    req.Timeout     = 10000;
                    
                    OnRequest(req);
                    HttpUtil.SetRequestData(req, data);
                    WebResponse res = req.GetResponse();
                    OnResponse(res);
                    return;
                } catch (Exception ex) {
                    HttpUtil.DisposeErrorResponse(ex);
                    lastEx = ex;
                    continue;
                }
            }
            
            string hostUrl = new Uri(URL).Host;
            Logger.Log(LogType.Warning, "Failed to send heartbeat to {0} ({1})", hostUrl, lastEx.Message);
        }
        
        
        /// <summary> Adds the given heartbeat to the list of automatically pumped heartbeats </summary>
        public static void Register(Heartbeat beat) {
            beat.Init();
            beat.Salt = Server.GenerateSalt();
            Heartbeats.Add(beat);
        }
        
        /// <summary> Starts pumping heartbeats </summary>
        public static void Start() {
            OnBeat(null); // immedately call so URL is shown as soon as possible in console
            Server.Background.QueueRepeat(OnBeat, null, TimeSpan.FromSeconds(30));
        }
        
        static string lastUrls;
        /// <summary> Reloads list of heartbeats from Server.Config </summary>
        public static void ReloadDefault() {
            string urls = Server.Config.HeartbeatURL;
            // don't reload heartbeats unless absolutely have to
            if (urls == lastUrls) return;
            lastUrls    = urls;
            
            Heartbeats.Clear(); // TODO only reload default heartbeats
            foreach (string url in urls.SplitComma())
            {
                Heartbeat beat = new ClassiCubeBeat() { URL = url };
                Register(beat);
            }
        }
        
        static void OnBeat(SchedulerTask task) {
            foreach (Heartbeat beat in Heartbeats) { beat.Pump(); }
        }
    }
}
