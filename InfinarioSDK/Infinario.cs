
using System.Collections;
using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Reflection;
using Infinario.MiniJSON;
using System.Xml;
using System.Threading.Tasks;
using System.Resources;

namespace Infinario {
	public delegate void CommandFn(Command command);
	#region Interfaces
	public interface IInfinarioApi{
		/// <summary>
		/// Identifies the current player with a name (corresponding to the field 'registered' in the customer's profile in Infinario). You can use this method to:
		/// 	* identify an anonymous player/customer
		/// 	* switch players/customer for which the subsequent tracking events apply.
		/// 
		/// Note on anonymous players/customers: 
		/// 	Whenever you start tracking and Infinario SDK fails to load the current player's identity from your local cache, an anonymous player is created and all events are tracked for him. Once you call Identify, 
		/// 	this player will keep all his events from back when he was anonymous (and be merged with an existing player if their name parameters match.)
		/// </summary>
		/// <param name="name">Any string by which you wish to identify the player ().</param>
		/// <param name="properties">An optional dictionary of (new) properties to add to this player.</param>
		void Identify (String name, object properties);

		/// <summary>
		/// Tracks an event for the current player.
		/// </summary>
		/// <param name="type">Event type - you choose the name.</param>
		/// <param name="properties">Optional properties (a dictionary).</param>
		/// <param name="time">Optional timestamp denoting when the event took place. If you want to be 100% sure about compatibility, use Infinario.Command.Epoch() as a way of obtaining the timestamp. If this parameter is ommited, timestamp when command created is used.</param>
		void Track (String type, object properties, long time);		
		void Track (String type, long time);
		void Track (String type, object properties);

		/// <summary>
		/// Updates the current player's properties.
		/// </summary>
		/// <param name="properties">A dictionary of properties you wish to update.</param>
		void Update (object properties);
	}

	#endregion

	/// <summary>
	/// Abstract class representing an Infinario API command.
	/// </summary>
	public abstract class Command {	
		public string Cookie;
		public string Registered;

		public static long Epoch() {
			var t0 = DateTime.UtcNow;
			var tEpoch = new DateTime (1970, 1, 1, 0, 0, 0);
			return (long)Math.Truncate (t0.Subtract (tEpoch).TotalSeconds);
		}
		
		public abstract String Endpoint {
			get ;
		}

		public abstract object JsonPayload {
			get ;
		}

		public virtual object JsonSerialize {
			get { 
				return JsonPayload;
			}
		}

		public override String ToString() {
			return "curl -X \'POST\' https://api.infinario.com/" + Endpoint + "  -H \"Content-type: application/json\" -d \'" +
				Json.Serialize (JsonPayload) + "\'";
		}

		public object BulkSerialization() {
			return new Dictionary<String,object> () {
				{ "name",this.Endpoint },
				{ "data",this.JsonSerialize }
			};
		}

		public object BulkRepresentation() {
			return new Dictionary<String,object> () {
				{ "name",this.Endpoint },
				{ "data",this.JsonPayload }
			};
		}
		
		public String SerializeToJson() {
			return Json.Serialize (BulkRepresentation());
		} 

		protected Dictionary<string,object> GetIdsDict() {
			var idsDict = new Dictionary<string,object> {{ "cookie", Cookie}};
			if(!String.IsNullOrEmpty(Registered)) {
				idsDict.Add ("registered",Registered);
			}
			return idsDict;
		}
	}
	
	public class CustomerCommand : Command {
		private const string CUSTOMERS_API_ROUTE = "crm/customers";

		private String CompanyToken;
		private object Properties;

		public CustomerCommand (String company, String cookie, String registered, object properties) {
			this.CompanyToken = company;
			this.Cookie = cookie;
			this.Registered = registered;
			this.Properties = properties;
		}

		public override String Endpoint {
			get { 
				return CUSTOMERS_API_ROUTE;
			}
		}

		public override object JsonPayload {
			get { 
					var dict = new Dictionary<object,object>() {
						{"ids",  GetIdsDict()},
						{"company_id",  this.CompanyToken}
					};
					if (this.Properties != null){
						dict.Add("properties",this.Properties);
					}
					return dict;
				}
		}
	}
	
	public class EventCommand : Command {
		private const string EVENTS_API_ROUTE = "crm/events";

		private String Type;
		private long Time;
		private String Company;
		private object Properties;
		
		public EventCommand (String company, String cookie, String registered, String type, object properties, long time) {
			this.Company = company;
			this.Cookie = cookie;
			this.Registered = registered;
			this.Properties = properties;
			this.Type = type;
			this.Time = time;
		}
		
		public override String Endpoint {
			get { 
				return EVENTS_API_ROUTE;
			}
		}

		public override object JsonSerialize {
			get { 
				var dict = new Dictionary<object,object>(){
					{"customer_ids", GetIdsDict()},
					{"company_id",  this.Company}, 
					{"type",  this.Type}, 
					{"timestamp",  this.Time}
				};

				if (this.Properties != null) {
					dict.Add ("properties",this.Properties);
				}
				return dict;
			}
		}

		public override object JsonPayload {
			get { 
				var dict = new Dictionary<object,object>(){
					{"customer_ids", GetIdsDict()},
					{"company_id", this.Company}, 
					{"type", this.Type}, 
					{"age", Epoch () - this.Time},
				};
				if (this.Properties != null) {
					dict.Add ("properties", this.Properties);
				}
				return dict;
			}
		}
	}
	
	#region Session management
	public class InfinarioSessionData {
		public long StartTsp = long.MinValue; //tsp of the last session start
		public long LastSeenTsp=long.MinValue;
		public String CompanyToken=String.Empty;
		public String Platform=String.Empty;
		public String Device=String.Empty;
		public String Cookie=String.Empty;
		public String Registered = String.Empty;
		public bool IsValid = false;

		public InfinarioSessionData(long lastStartTsp, long lastSeenTsp, String companyToken, String platform, string device, string cookie, string registered) {
			StartTsp = lastStartTsp;
			LastSeenTsp = lastSeenTsp;
			CompanyToken = companyToken;
			Platform = platform;
			Device = device;
			Cookie = cookie;
			Registered = registered;
		}

		public InfinarioSessionData(string json){
			try{
				Dictionary<string,object> obj = (Dictionary<string,object>)Json.Deserialize(json);
				StartTsp = (long)obj ["lastSessionStartTsp"];
				LastSeenTsp =  (long)obj["lastSeenTsp"];
				CompanyToken = obj ["companyToken"].ToString();
				Platform = obj ["os_version"].ToString();
				Device = obj ["device_model"].ToString();
				Cookie = obj ["cookieId"].ToString();
				if(obj.ContainsKey("registeredId")){
					Registered = obj ["registeredId"].ToString();
				}
				IsValid = true;
			}catch(Exception e) {
				var s ="";
				foreach (var stack in e.StackTrace){
					s+=(stack.ToString());
				}
				IsValid = false;
			}
		}

		public Dictionary<string,object> ToDict() {
			var dict= new Dictionary<string,object>{
				{"lastSessionStartTsp", StartTsp},
				{"lastSeenTsp", LastSeenTsp},
				{"companyToken", CompanyToken},
				{"os_version",Platform},
				{"device_model",Device},
				{"cookieId",Cookie}
			};
			if (Registered != null) {
				dict.Add ("registeredId",Registered);
			}
			return dict;
		}

		public override string ToString ()
		{
			return Json.Serialize (this.ToDict());
		}


	}

	public interface ISessionPersistenceAdapter {
		void SaveSession(InfinarioSessionData data);
		InfinarioSessionData LoadSession ();
	}

	public class SessionPrefPersistenceAdapter : ISessionPersistenceAdapter{
		private const string SESSION_PREF = "infinario_session";

		public void SaveSession(InfinarioSessionData data) {
			PlayerPrefs.SetString(SESSION_PREF,Json.Serialize(data.ToDict()));
            
		}

		public InfinarioSessionData LoadSession() {
			var sessionStr = PlayerPrefs.GetString (SESSION_PREF);		
			return (String.IsNullOrEmpty(sessionStr) ? null : new InfinarioSessionData (sessionStr));
		}
	}
	
	public enum SessionExpirationReason { NotExpired, NotStarted, Timeout, Logout }

	/// <summary>
	/// Represents a session.
	/// </summary>
	public class InfinarioSession {		
		public const long MAX_SESSION_TIMEOUT = 1*5;
		public const long TIMEOUTED_SESSION_OFFSET = 30;
		
		private static ISessionPersistenceAdapter SessionPersistenceAdapter = new SessionPrefPersistenceAdapter();
		private static CommandFn ScheduleCommandFunc;
		public static string CompanyToken;

		private InfinarioSessionData SessionData;

		public string Cookie { 
			get { return this.SessionData.Cookie; }
		}

		public string Registered { 
			get { return this.SessionData.Registered; }
		}

		#region Static methods		
		private static void ScheduleCommand(Command cmd) {
			ScheduleCommandFunc(cmd);
		}

		public static InfinarioSession InitializeSession(string companyToken, CommandFn scheduleCommandFunc) {
			ScheduleCommandFunc = scheduleCommandFunc;
			CompanyToken = companyToken;

			var recoveredSessionData = SessionPersistenceAdapter.LoadSession ();
			
			// case 1: non-existent or corrupted session
			if (recoveredSessionData == null || !recoveredSessionData.IsValid) {
				return new InfinarioSession(NewSession());
			}
			
			//case 2: timeout or a different company
			if (recoveredSessionData.LastSeenTsp + MAX_SESSION_TIMEOUT < Command.Epoch () ||
				!recoveredSessionData.CompanyToken.Equals(companyToken)) {
				EndSession(recoveredSessionData, SessionExpirationReason.Timeout);
				return new InfinarioSession(NewSession(recoveredSessionData.Cookie));
			}
			
			//case 3: recover old session
			return new InfinarioSession (recoveredSessionData);
		}

		/// <summary>
		/// Ends a session specified by session data and issues a command.
		/// </summary>
		/// <param name="data">Valid session data.</param>
		/// <param name="scheduleCommandFunc">Scheduling function.</param>
		private static void EndSession (InfinarioSessionData data, SessionExpirationReason expirationReason) {
			var duration = data.LastSeenTsp - data.StartTsp;
            var eventProps = InfinarioSessionHelper.getDeviceProperties();
            eventProps.Add("duration", duration);

            if (!String.IsNullOrEmpty(InfinarioSessionHelper.App_Version))
            {
                eventProps.Add("app_version", InfinarioSessionHelper.App_Version);
            }

			EventCommand sessionEndCommand = new EventCommand (data.CompanyToken,
			                                                   data.Cookie, data.Registered,
			                                                   "session_end",
					                                           eventProps,
			                                                   data.StartTsp + duration);
		    ScheduleCommand(sessionEndCommand);
		}

        public static void sessionStart(string cookieId = null, string registeredId = null){
            string cookie = (String.IsNullOrEmpty(cookieId) ? InfinarioSessionHelper.GenerateGUID () : cookieId);
			InfinarioSessionData data = new InfinarioSessionData (Command.Epoch(),
			                                                      Command.Epoch(),
			                                                      CompanyToken, 
			                                                      InfinarioSessionHelper.GetPlatform(),
			                                                      InfinarioSessionHelper.GetDevice(), 
			                                                      cookie, registeredId);
			ScheduleCommand (GetSessionStartCommand (data));
        }

		private static InfinarioSessionData NewSession (string cookieId = null, string registeredId = null) {
			string cookie = (String.IsNullOrEmpty(cookieId) ? InfinarioSessionHelper.GenerateGUID () : cookieId);
			InfinarioSessionData data = new InfinarioSessionData (Command.Epoch(),
			                                                      Command.Epoch(),
			                                                      CompanyToken, 
			                                                      InfinarioSessionHelper.GetPlatform(),
			                                                      InfinarioSessionHelper.GetDevice(), 
			                                                      cookie, registeredId);
			ScheduleCommand (GetSessionStartCommand (data));
			return data;
		}

		private static EventCommand GetSessionStartCommand(InfinarioSessionData data) {
            var eventProps = InfinarioSessionHelper.getDeviceProperties();
            if (!String.IsNullOrEmpty(InfinarioSessionHelper.App_Version))
            {
                eventProps.Add("app_version", InfinarioSessionHelper.App_Version);
            }
			return new EventCommand (data.CompanyToken,
                                     data.Cookie, data.Registered,
                                     "session_start",
                                     eventProps,
                                     data.StartTsp);
		}	
		#endregion
				
		private InfinarioSession(InfinarioSessionData sessionData) {
			this.SessionData = sessionData;
			Persist ();		
		}
			
		private void Persist() {
			InfinarioSession.SessionPersistenceAdapter.SaveSession (this.SessionData);
		}
				
		private SessionExpirationReason TestExpiration() {
			if (SessionData.LastSeenTsp + MAX_SESSION_TIMEOUT > Command.Epoch()) {
				return SessionExpirationReason.NotExpired;
			}
			return SessionExpirationReason.Timeout;
		}

		/// <summary>
		/// If any unexpired session is running, its lifetime is extended. Otherwise, the old session is ended and new one is started.
		/// </summary>
	    public void KeepAlive(){
			var expirationStatus = this.TestExpiration ();
			if (expirationStatus != SessionExpirationReason.NotExpired) {
				EndSession (this.SessionData, expirationStatus);
				this.SessionData = NewSession (SessionData.Cookie, SessionData.Registered);
			}
			this.SessionData.LastSeenTsp = Command.Epoch ();
			Persist ();
		}
			  
		public void UpdateIdentity(string newRegistered) {
			bool registeredEqual = String.Equals (newRegistered, SessionData.Registered);
			bool newRegisteredSet = !String.IsNullOrEmpty (newRegistered);
			bool oldRegisteredSet = !String.IsNullOrEmpty (SessionData.Registered);

			if (newRegisteredSet) {
				bool newSession = oldRegisteredSet && !registeredEqual;
				string freshCookie = InfinarioSessionHelper.GenerateGUID ();

				if(newSession) {
					EndSession(SessionData, SessionExpirationReason.Logout);					
					this.SessionData = NewSession(freshCookie, newRegistered);
				} else if (oldRegisteredSet){
					this.SessionData.Cookie = freshCookie;
				}
			}

			if (newRegisteredSet){
				this.SessionData.Registered = newRegistered;
			}						
			Persist ();		
		}		
	}

	static class InfinarioSessionHelper {


        public static String App_Version = String.Empty;

		public static string GenerateGUID() {
			var random = new System.Random();                     		
			return System.Globalization.CultureInfo.InstalledUICulture.DisplayName         //Language
                + "-" + String.Format("{0:X}", Convert.ToInt32(Command.Epoch()))              //Time
                    + "-" + String.Format("{0:X}", Convert.ToInt32(5000))        //Time in game
                    + "-" + String.Format("{0:X}", random.Next(1000000000));
		}

        public static Dictionary<string, object> getDeviceProperties()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("sdk", SDKInformation.sdk_name);
            dict.Add("sdk_version", SDKInformation.sdk_version);
            dict.Add("os_name", GetPlatform());
            dict.Add("device_model", GetDevice());
            dict.Add("os_version", GetPlatform());
            return dict;
        }

        public static class SDKInformation
        {
            public static String sdk_version = "1.1.0";
            public static String sdk_name = "C# SDK";
        }
		
		public static string GetPlatform() {
            return System.Environment.OSVersion.Platform.ToString();
		}
		
		public static string GetDevice() {
            return "PC";
		}
	}
#endregion

	public class Infinario : IInfinarioApi {
        private const string INFINARIO_API_URI = "https://api.infinario.com/";
		private const string INFINARIO_API_BULK_ROUTE = "bulk";

		private const int MAX_BULK_REQUEST_SIZE = 49;

		public String CompanyToken;
		protected String Target;
        static private String app_version;

		protected static PersistentBulkCommandQueue CommandQueue = new PersistentBulkCommandQueue ("events",MAX_BULK_REQUEST_SIZE);
		private static InfinarioSession Session;

		#region Contructors
		/// <summary>
		/// Initializes and starts a new tracking session.
		/// </summary>
		/// <param name="companyToken">Your company token.</param>
		/// <param name="target">The base URI to the Infinario API (you usually do not have to set this parameter).</param>
		
		public Infinario(String companyToken){
			this.InfinarioInitialize (companyToken, INFINARIO_API_URI, null);
		}

        public Infinario(String companyToken, String appVersion)
        {
            this.InfinarioInitialize(companyToken, INFINARIO_API_URI, appVersion);
        }

		protected void InfinarioInitialize(String companyToken, String target, String app_version){

            if (app_version != null)
            {
                InfinarioSessionHelper.App_Version = app_version;
            }

			this.CompanyToken = companyToken;
			this.Target = target;

			Session = InfinarioSession.InitializeSession(companyToken,ScheduleCommand);
			this.StartSendLoop ();
		}

		#endregion

		#region Public API
		public void Identify(String name, object properties){
			Session.UpdateIdentity (name);
			ScheduleCommand (new CustomerCommand(InfinarioSession.CompanyToken,Session.Cookie, Session.Registered, properties));
		}

		public void Identify(String name){
			this.Identify (name, null);
		}

		public void Track(String EventType, object Properties, long Time){
			ScheduleCommand(new EventCommand(InfinarioSession.CompanyToken, Session.Cookie, Session.Registered, EventType, Properties, Time));
		}
		
		public void Track(String EventType, long Time){
			this.Track(EventType, null, Time == long.MinValue ? Command.Epoch() : Time);
		}
				
		public void Track(String EventType,object Properties){
			this.Track(EventType,Properties, Command.Epoch());
		}

		public void Track(String EventType){
			this.Track (EventType, null, Command.Epoch ());
		}

        public void TrackVirtualPayment(String currency, long amount, String itemName, String itemType)
        {
            Dictionary<string, object> properties = InfinarioSessionHelper.getDeviceProperties();
            properties.Add("currency", currency);
            properties.Add("amount", amount);
            properties.Add("item_name", itemName);
            properties.Add("item_type", itemType);
            this.Track("virtual_payment", properties);
        }
	
		public void Update(object properties){
			ScheduleCommand(new CustomerCommand(InfinarioSession.CompanyToken, Session.Cookie, Session.Registered, properties));
		}

        public void ClearDataStored()
        {
            PlayerPrefs.ClearDataStored();
        }

		/// <summary>
		/// Specifies how many bytes of information is the API allowed to store in the player prefs (approximate).
		/// </summary>
		/// <value>The max saved bytes.</value>
		private int MaxSavedBytes {
			get {
				return maxSavedBytes;
			}
			set {
				maxSavedBytes = value;
				CommandQueue.SetMaxQueueBytes(value);
			}
		}

		#endregion
		private int maxSavedBytes = 1024*1024;
	
		#region Schedule
		public virtual void ScheduleCommand(Command command){
			List<object> lst = new List<object> ();
			lst.Add (command.BulkSerialization());
			CommandQueue.MultiEnqueue(lst);
		}

		private void ScheduleCommand(object serializedObj) {
			CommandQueue.MultiEnqueue (new []{serializedObj});
		}

		#endregion

		private void StartSendLoop() {
			APISendLoop(this.Target,this);
		}

		#region API communication
		/// <summary>
		/// Periodically performs the following steps:
		///  0. keep alive the session
		// 	 1. collect the freshest command bulk
		//	 2. try-send bulk
		// 	 3. validate response
		// 	 4. parse response and enqueue for retry if needed
		// 	 5. wait for N seconds (N grows with the number of consecutive connection failures)
		/// </summary>
		/// <returns>The send loop enumerator.</returns>
		private void APISendLoop(string apiTarget, Infinario api) {

            Task.Run(async () =>
            {
                const int WAIT_FOR_DEFAULT = 3;
                var httpTarget = apiTarget + INFINARIO_API_BULK_ROUTE;
                int consecutiveFailedRequests = 0;

                while (true)
                {
                    //Save current heartbeat to prefs
                    Session.KeepAlive();
                    // Decide if we process retry commands or new commands in this round
                    List<object> commands = CommandQueue.BulkDequeue(true);


                    if (commands.Count > 0)
                    {

                        // 1B: Prepare the http components
                        var httpBody = Json.Serialize(new Dictionary<string, object> { { "commands", commands } });
                        byte[] httpBodyBytes = Encoding.UTF8.GetBytes(httpBody);
                        Dictionary<string, string> httpHeaders = new Dictionary<string, string> { { "Content-type", "application/json" } };

                        // 2. Send the bulk API request
                        //WWW req = new WWW(httpTarget, httpBodyBytes, httpHeaders); //TODO: we could add a timeout functionality

                        try
                        {
                            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(httpTarget);
                            request.Method = "POST";
                            request.Timeout = 10000;
                            request.ContentType = "application/json";
                            request.ContentLength = httpBodyBytes.Length;

                            byte[] byteArray = httpBodyBytes;

                            Stream postStream = request.GetRequestStream();
                            postStream.Write(byteArray, 0, byteArray.Length);
                            postStream.Close();

                            var response = (HttpWebResponse)request.GetResponse();
                            var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();

                            var responseBody = responseString;
                            Dictionary<string, object> apiResponse = (Dictionary<string, object>)Json.Deserialize(responseBody);
                            bool success = (bool)apiResponse["success"];
                            if (success)
                            {
                                consecutiveFailedRequests = 0;
                                Console.WriteLine("POST SUCCES commands: " + commands.Count);
                                // 4A: extract retry-commands and queue them back (if any)
                                var retryCommands = ExtractRetryCommands(apiResponse, commands);
                                CommandQueue.MultiDequeue(commands.Count); //remove every command from this request
                                CommandQueue.MultiPush(retryCommands);     //re-add failed commands with the highest priority
                            }
                            else
                            {
                                consecutiveFailedRequests++;
                            }
                        }
                        catch (System.Net.WebException e)
                        {
                            consecutiveFailedRequests++;
                        }
                    }

                    // 5. Detemine wait time and go idle.
                    float waitSeconds = (float)Math.Pow(WAIT_FOR_DEFAULT, Math.Sqrt(consecutiveFailedRequests + 1));
                    if (consecutiveFailedRequests == 0 && CommandQueue.ElementCount > 0)
                    {
                        waitSeconds = 0f;
                    }
                    waitSeconds = Math.Min(waitSeconds, InfinarioSession.MAX_SESSION_TIMEOUT - 3f);

                    await Task.Delay(((int)waitSeconds * 1000));
                }
            });

			
		}
	
		/// <summary>
		/// Walks through the API response and returns all commands that should be retried.
		/// </summary>
		/// <returns>A list of retry command objects.</returns>
		/// <param name="response">API response dictionary object.</param>
		/// <param name="sentCommands">Request dictionary object.</param>
		private static List<object> ExtractRetryCommands(Dictionary<string,object> response,List<object> sentCommands) {
			List<object> commandResponses = response ["results"] as List<object>;

			List<object> retryCommands = new List<object> ();
			int idx = 0;
			foreach (var cmdResponse in commandResponses) {
				var cmdResponseDict = (Dictionary<string,object>)cmdResponse;
				string status = (cmdResponseDict ["status"] as String).ToLower ();
				if (status.Equals ("retry")){
					retryCommands.Add (sentCommands[idx]);
				}
				idx++;
			}
			return retryCommands;
		}

		//protected MonoBehaviour _coroutineObject;
		//protected void StartCoroutine(IEnumerator coroutine){
			//if (_coroutineObject == null) {
			//	var go = new GameObject("Infinario Coroutines");
			//	UnityEngine.Object.DontDestroyOnLoad(go);
			//	_coroutineObject = go.AddComponent<MonoBehaviour>();
			//}		
			//coroutineObject.StartCoroutine (coroutine);
		//}

		#endregion
	}
	
	public class PersistentCommandQueue {
		const string PERSISTENT_QUEUE_KEY = "infinario_command_queue";
		private string QueueName;
		private int MaxQueueBytes;
        private object lockS = new object();
        

		public PersistentCommandQueue(int maxBytes=1024*1024,string name = PERSISTENT_QUEUE_KEY) {
                this.QueueName = name;
                this.SetMaxQueueBytes(maxBytes);
                this.lockS = new object();	
		}

		public void SetMaxQueueBytes(int bytes) {
            lock (lockS)
            {
                this.MaxQueueBytes = bytes;
                List<object> queue = this.GetQueue();
                List<object> newQueue = new List<object>();
                int sumBytes = 0;
                int i = 0;
                while (i < queue.Count)
                {
                    sumBytes += 2 * (Json.Serialize(queue.ElementAt(i))).Length;
                    if (sumBytes > bytes)
                    {
                        break;
                    }
                    newQueue.Add(queue.ElementAt(i));
                }
                this.SetQueue(newQueue);
            }			
		}

		public void MultiPush(IEnumerable<object> commands){
            lock (lockS)
            {
                var queue = this.GetQueue();
                foreach (var cmd in commands)
                {
                    queue.Add(cmd);
                }
                this.SetQueue(queue);
            }		
		}

		public List<object> MultiPop(int Elements=1, bool peek=false){
            lock (lockS)
            {
                var queue = this.GetQueue();
                List<object> results = new List<object>();
                for (int i = 0; i < Elements && queue.Count > 0; i++)
                {
                    results.Add(queue.Count - 1);
                    queue.RemoveAt(queue.Count - 1);
                }

                if (!peek)
                {
                    this.SetQueue(queue);
                }
                return queue;
            }			
		}

		public void MultiEnqueue(IEnumerable<object> commands) {
            lock (lockS)
            {
                var queue = this.GetQueue();
                foreach (var cmd in commands)
                {
                    queue.Add(cmd);
                }
                this.SetQueue(queue);
            }			
		}

		public List<object> MultiDequeue(int Elements=1,bool peek=false) {
            lock (lockS)
            {
                var queue = this.GetQueue();
                List<object> result = new List<object>();

                int queueSize = queue.Count;
                if (queueSize == 0)
                {
                    return new List<object>();
                }

                for (int i = 0; i < Math.Min(Elements, queueSize); i++)
                {
                    result.Add(queue.ElementAt(0));
                    queue.RemoveAt(0);
                }

                if (!peek)
                {
                    this.SetQueue(queue);
                }

                return result;
            }		
		}

		private List<object> GetQueue() {
            lock (lockS)
            {
                if (!PlayerPrefs.HasKey(this.QueueName))
                {
                    return new List<object>();
                }
                string serializedQueue = PlayerPrefs.GetString(this.QueueName);
                List<object> queue = Json.Deserialize(serializedQueue) as List<object>;
                return queue == null ? new List<object>() : queue;
            }			
		}

		private void SetQueue(List<object> queue) {
            lock (lockS)
            {
                String str = Json.Serialize(queue);
                if (str.Length * 2 <= this.MaxQueueBytes)
                {
                    PlayerPrefs.SetString(this.QueueName, str);
                    //PlayerPrefs_Save ();
                }
            }		
		}

		public void PushAll(IEnumerable<object> commands) {
            lock (lockS)
            {
                if (commands.Count() == 0)
                {
                    return;
                }

                List<object> queue = new List<object>(commands);
                List<object> oldQueue = this.GetQueue();
                foreach (var itm in oldQueue)
                {
                    queue.Add(itm);
                }
                this.SetQueue(queue);
            }			
		}

		public int ElementCount {
			get  { return GetQueue().Count; }
		}
	}

	public class PersistentBulkCommandQueue: PersistentCommandQueue {
		private int BulkSize;
		public PersistentBulkCommandQueue(string name, int bulkSize):base(1024*1024,name) {
			this.BulkSize = bulkSize;
		}

		public List<object> BulkDequeue(bool peek = true) {
			int elements = Math.Min(this.ElementCount, this.BulkSize);
			return this.MultiDequeue(elements,peek);
		}

	}

    public class PlayerPrefs
    {
        public static Dictionary<string, string> index;
        public static XmlDocument doc;

        public static string GetString(string str)
        {
            if (doc.SelectSingleNode("//" + str) == null)
            {
                return null;
            }
            else
            {
                return doc.SelectSingleNode("//" + str).InnerText;
            }

        }

        public static void SetString(string q, string str)
        {
            if (doc.SelectSingleNode("//" + q) == null)
            {
                XmlNode root = doc.SelectSingleNode("main_setting", null);

                XmlNode ndl = doc.CreateNode(XmlNodeType.Element, q, null);
                ndl.InnerText = str;
                root.AppendChild(ndl);
                doc.AppendChild(root);
            }
            else
            {
                
                doc.SelectSingleNode("//" + q).InnerText = str;
            }
            doc.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "infi.xml"));
        }

        public static void ClearDataStored()
        {
            doc.DocumentElement.RemoveAll();
            doc.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "infi.xml"));
        }

        public static Boolean HasKey(string str)
        {
            if (doc == null)
            {
                doc = new XmlDocument();

                if (!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "infi.xml")))
                {
                    XmlElement el = doc.CreateElement("main_setting");
                    doc.AppendChild(el);
                    doc.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "infi.xml"));
                }
                else
                {
                    try
                    {
                        doc.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "infi.xml"));
                    }
                    catch
                    {
                        XmlElement el = doc.CreateElement("main_setting");
                        doc.AppendChild(el);
                        doc.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "infi.xml"));
                    }
                   
                }               
            }

            if (doc.SelectSingleNode("//" + str) == null)
            {
                return false;
            } else{
                return true;
            }
        } 
    }
}
