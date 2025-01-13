using HarmonyLib;
using Il2Cpp;
using Newtonsoft.Json;
using MelonLoader;
using System.Reflection.Emit;
using UnityEngine.SceneManagement;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using Object = UnityEngine.Object;
using System.Text;
using UnityEngine;
using Il2CppEnviro;
using System.Reflection;
[assembly: MelonInfo(typeof(PPDS_DuckConnect.Core), "DuckConnect", "0.2.0", "Pladis", "https://github.com/pladisdev/PPDS-Mods")]
[assembly: MelonGame("Turbolento Games", "Placid Plastic Duck Simulator")]

namespace PPDS_DuckConnect
{
    public class DuckObject
    {
        public string DuckID { get; set; }
        public string DuckName { get; set; }
    }

    public class Core : MelonMod
    {
        public static Core _instance;
        private static GeneralManager _generalManager;
        private static readonly string _savePath = "./UserData/CustomNames.json";
        private static string _savecontent;
        public static bool NewDuck = false;
        public static Dictionary<string, string> _duckNames = new();
        public static Dictionary<string, int> _ducks = new();
        public static bool AutoName { get; set; }
        private MqttClient mqttClient;
        private static readonly string brokerIpAddress = "localhost";
        private static readonly int brokerPort = 1883;
        private static readonly string clientId = "duck_client";
        private static readonly string tabTopic = "ppds/tab";
        private static readonly string quackTopic = "ppds/quack";
        private static readonly string duckNameTopic = "ppds/duckname";
        private static readonly string spawnDuckTopic = "ppds/spawn";
        private static readonly string eraseNamesTopic = "ppds/erase";
        private static readonly string duckIDTopic = "ppds/duckid";

        public static DuckManager currentduck { get; set; }
        public string duckName = "";

        private string currentScene = "";
        private bool spawnDuck = false;

        public override void OnEarlyInitializeMelon()
        {
            _instance = this;
        }

        public static class CustomNameSettings //MelonSettings for the Mod
        {
            private const string SettingsCategory = "CustomNames";
            internal static MelonPreferences_Entry<bool> AutoName;

            internal static void RegisterSettings()
            {
                var category = MelonPreferences.CreateCategory(SettingsCategory, "CustomNames");
                AutoName = category.CreateEntry("AutoName", false, "Auto Name",
                    "Newly Spawning Ducks will be Auto Nammed via clipboard");
            }
        }

        public override void OnInitializeMelon()
        {
            CustomNameSettings.RegisterSettings();
            AutoName = CustomNameSettings.AutoName.Value;

            //Save File
            if (!File.Exists(_savePath))
            {
                File.Create(_savePath);
            }
            else
            {
                _savecontent = File.ReadAllText(_savePath);
                _duckNames = JsonConvert.DeserializeObject<Dictionary<string, string>>(_savecontent);
            }

            //Harmony Patching
            var harmony = new HarmonyLib.Harmony("Custom_Names");
            try
            {
                harmony.PatchAll(typeof(DuckTrainerPatch.GeneralManager_AddDuck));
                _instance.LoggerInstance.Msg("General Manager Add Patched!");

                harmony.PatchAll(typeof(DuckTrainerPatch.SpawnUpdate_Patch));
                _instance.LoggerInstance.Msg("Spawn Update Patched!");
            }
            catch (Exception e)
            {

                _instance.LoggerInstance.Msg(e);
            }

            try
            {
                mqttClient = new MqttClient(brokerIpAddress, brokerPort, false, null, null, MqttSslProtocols.None);
                mqttClient.MqttMsgPublishReceived += OnMqttMsgPublishReceived;
                mqttClient.Connect(clientId);

                mqttClient.Subscribe(new string[] { tabTopic }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
                mqttClient.Subscribe(new string[] { quackTopic }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
                mqttClient.Subscribe(new string[] { duckNameTopic }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
                mqttClient.Subscribe(new string[] { spawnDuckTopic }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
                mqttClient.Subscribe(new string[] { eraseNamesTopic }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
                _instance.LoggerInstance.Msg("MQTT Connected!");
            }
            catch (Exception e)
            {
                _instance.LoggerInstance.Msg(e);
            }
        }

        private static void SpawnDuck()
        {
            try
            {
                _generalManager.spawnCounter = 990;
            }
            catch (Exception e)
            {
                _instance.LoggerInstance.Error(e);
            }
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            _instance.LoggerInstance.Msg($"Scene Initialized: {sceneName}");
            //Hook GeneralManager
            currentScene = sceneName.Replace(" ", "");

            if (CheckScene()) { return; }
            _ducks.Clear();
            _generalManager = Object.FindObjectOfType<GeneralManager>();
            if (_generalManager == null)
            {
                _instance.LoggerInstance.Error("General Manager Didn't Hook!!");
            }

            _instance.LoggerInstance.Msg("General Manager Hooked!");
            currentScene = sceneName.Replace(" ", "");
        }

        private bool CheckScene()
        { 
            var intro = currentScene == "Intro";
            var BootStrap = currentScene == "Bootstrap";
            var loading = currentScene == "Loading";
            var sceneNull = currentScene == "";

            return intro || BootStrap || loading || sceneNull; // Check if Intro Scene is not loaded
        }

        public override void OnLateUpdate()
        {

            if (CheckScene()) { spawnDuck = false; return; }

            // Duck renaming can only occur OnLateUpdate, throws an error otherwise
            if (currentduck != null && duckName != "")
            {
                DuckRename(currentduck, duckName);
                currentduck = null;
                duckName = "";
            }

            if (!spawnDuck)
            {
                SpawnDuck();
                spawnDuck = true;
            }
            
        }

        private void OnMqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            try
            {
                // Check which MQTT topic the message was received on
                if (e.Topic == tabTopic)
                {
                    OnTabMessageReceived();
                }
                else if (e.Topic == quackTopic)
                {
                    OnQuackMessageReceieved();
                }
                else if (e.Topic == spawnDuckTopic)
                {
                    SpawnDuck();
                }
                else if (e.Topic == eraseNamesTopic)
                {
                }
                else if (e.Topic == duckNameTopic)
                {
                    string message = Encoding.UTF8.GetString(e.Message);
                    if (string.IsNullOrEmpty(message)) { return; }

                    currentduck = _generalManager.Ducks[_generalManager.CurrentDuck].GetComponent<DuckManager>();
                    duckName = message;
                }
                _instance.LoggerInstance.Msg(e.Topic);
            }
            catch (Exception ed)
            {
                _instance.LoggerInstance.Msg(ed);
            }
        }

        private void OnTabMessageReceived()
        {

            if (CheckScene()) { return; } // Check if Intro Scene is not loaded

            var oldduck = _generalManager.Ducks[_generalManager.CurrentDuck].GetComponent<DuckManager>();
            if (oldduck == null) { _instance.LoggerInstance.Error("Duck Not selected"); return; }

            var currentduck = oldduck;
            int SwitchFail = 0;

            // Since SelectRandomDuck can choose the same duck, this is used to attempt selecting another duck
            while (oldduck.duckID == currentduck.duckID && SwitchFail++ < 5)
            {
                _generalManager.SelectRandomDuck();

                System.Threading.Thread.Sleep(100);

                currentduck = _generalManager.Ducks[_generalManager.CurrentDuck].GetComponent<DuckManager>();

                if (currentduck == null) { _instance.LoggerInstance.Error("Duck Not selected"); return; }
            }

            if (oldduck.duckID == currentduck.duckID) { _instance.LoggerInstance.Msg("Duck still the same"); return; }

            _instance.LoggerInstance.Msg(currentduck.duckID);

            var name = GetName(currentduck.duckID);

            // Can probably just send null instead of ""
            if (name == null) name = "";

            // Create a new instance of the DuckIDObject and set the DuckID property
            var duckIDObject = new DuckObject { DuckID = currentduck.duckID, DuckName = name };

            // Serialize the object to a JSON string
            var json = JsonConvert.SerializeObject(duckIDObject);
            mqttClient.Publish(duckIDTopic, Encoding.UTF8.GetBytes(json));
        }

        private void OnQuackMessageReceieved()
        {
            try
            {
                _generalManager.Ducks[_generalManager.CurrentDuck].GetComponent<DuckManager>().PlaySound();
            }

            catch (Exception e)
            {
                _instance.LoggerInstance.Msg(e);
            }
        }

        public static void DuckRename(DuckManager duckManager = null, string duckName = null) //Void can be used for other mods
        {

            try
            {
                if (duckName == null)
                {
                    return;
                }
                if (duckManager == null)
                {
                    if (_generalManager == null) { _instance.LoggerInstance.Error("Something went wrong"); return; }
                    var currentduck = _generalManager.Ducks[_generalManager.CurrentDuck].GetComponent<DuckManager>();
                    if (currentduck == null) { _instance.LoggerInstance.Error("Duck Not selected"); return; }
                    duckManager = currentduck;
                }

                duckManager.NameChanged(duckManager.duckID, duckName);
                _instance.LoggerInstance.Msg("Duck: " + duckManager.duckID + " | Duck Name: " + duckName);
                Apply_Name(duckManager.duckID, duckName);

            }
            catch (Exception e)
            {
                _instance.LoggerInstance.Msg(e);
            }
        }

        public static string GetName(string DuckID) //Void can be used for other mods
        {
            if (_duckNames.TryGetValue(DuckID, out string duckname)) { return duckname; }
            return null;
        }

        public static void Apply_Name(string duckID, string newduckName) //Void can be used for other mods
        {
            if (_duckNames.ContainsKey(duckID))
            {
                _duckNames[duckID] = newduckName;
            }
            else
            {
                _duckNames.Add(duckID, newduckName);
            }
            _savecontent = JsonConvert.SerializeObject(_duckNames, Formatting.Indented);
            File.WriteAllText(_savePath, _savecontent);
        }

        private static void WeatherChange() //Forces Weather to Clear
        {
            var _enviroweathermodule = Object.FindObjectOfType<EnviroWeatherModule>();
            var basegm = SceneManager.GetActiveScene().name == "MainScene";
            var snowdlc = SceneManager.GetActiveScene().name == "dlc2Env";
            if (basegm)
            {
                _enviroweathermodule.ChangeWeather("Clear Sky");
            }
            else if (snowdlc)
            {
                _enviroweathermodule.ChangeWeather("Clear SkyWinter");
            }
            else
            {
                _instance.LoggerInstance.Error("Changing Weather is not support in Current Scene yet");
            }

        }

        private static void OpenDuck() //Opening Ducks out of the Presents. Christmas Event may remove
        {
            var gmsAudio =
                Traverse.Create(_generalManager).Field("seasonOpenContainerAudioSource").GetValue() as AudioSource;
            var gmsFX =
                Traverse.Create(_generalManager).Field("seasonOpenContainerFX").GetValue() as ParticleSystem;
            var gmsFloat = new[] { 0.5f, 0.75f, 1.25f, 1.5f };
            var lastduck = _generalManager.CurrentDuck;
            _generalManager.ChangeDuck(0);
            for (var i = 0; i <= _generalManager.Ducks.Count; i++)
            {
                _generalManager.ChangeDuck(i);
                if (i >= _generalManager.Ducks.Count)
                {
                    _generalManager.CurrentDuck = lastduck;
                    return;
                }

                var currentduck =
                    _generalManager.Ducks[_generalManager.CurrentDuck].GetComponent<DuckManager>();
                if (!currentduck.IsInSeasonContainer) { continue; }
                _generalManager.AddDuck(currentduck, currentduck.duckID, true, false);
                currentduck.OnSeasonClick();
                if (gmsAudio != null && gmsFX != null)
                {
                    gmsAudio.pitch = gmsFloat[UnityEngine.Random.Range(0, gmsFloat.Length)];
                    gmsFX.transform.position = currentduck.transform.position;
                    gmsFX.Play();
                    gmsAudio.Play();
                }

                _instance.LoggerInstance.Msg("Opened " + currentduck);
            }
        }

    }

    public abstract class DuckTrainerPatch //Duck Respawn Patch for DuckManager
    {

        //Harmony Patches
        [HarmonyPatch(typeof(GeneralManager), "AddDuck")]
        public class GeneralManager_AddDuck
        {
            //TODO: Fix Add 2
            static void Postfix(ref GeneralManager __instance, DuckManager duckManager, string duckID, bool unlock = true, bool addToList = true)
            {
                int count = 0;
                if (Core._ducks.ContainsKey(duckID))
                {
                    count = Core._ducks[duckID];
                    Core._ducks[duckID]++;
                }
                else
                {
                    Core._ducks[duckID] = 1;
                }
                string modifiedDuckID = duckID;
                if (count > 0)
                {
                    modifiedDuckID += "_" + count;
                }
                duckManager.duckID = modifiedDuckID;
                if (Core.NewDuck && Core.AutoName)
                {
                    Core._instance.LoggerInstance.Msg("New Duck!");
                    Core.NewDuck = false;
                    Core.currentduck = duckManager;
                }
                else
                {
                    var duckNames = Core.GetName(modifiedDuckID);
                    duckManager.NameChanged(modifiedDuckID, duckNames);
                }
            }
        }

        [HarmonyPatch(typeof(GeneralManager), "SpawnUpdate")]
        public class SpawnUpdate_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
            {
                var code = new List<CodeInstruction>(instructions);

                int insertionIndex = -1;
                Label returnDuck = il.DefineLabel();
                for (int i = 0; i < code.Count - 1; i++)
                {
                    if (code[i].opcode == OpCodes.Stfld && code[i + 1].opcode == OpCodes.Ldarg_0)
                    {
                        insertionIndex = i;
                        code[i].labels.Add(returnDuck);
                        break;
                    }
                }

                var instructionsToInsert = new List<CodeInstruction>();
                //
                // CustomNames.NewDuck = True
                //
                instructionsToInsert.Add(new CodeInstruction(OpCodes.Ldc_I4_1, (sbyte)4));
                instructionsToInsert.Add(new CodeInstruction(OpCodes.Stsfld, AccessTools.Field(typeof(Core), "NewDuck")));

                if (insertionIndex != -1)
                {
                    code.InsertRange(insertionIndex, instructionsToInsert);
                }

                return code;
            }
        }
    }
}