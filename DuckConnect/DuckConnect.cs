using Duck_Connect;
using HarmonyLib;
using MelonLoader;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using System.Text;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

[assembly: MelonInfo(typeof(DuckConnect), "DuckConnect", "0.1.0", "Pladis", "https://github.com/pladisdev/PPDS-Mods")]
[assembly: MelonGame("Turbolento Games", "Placid Plastic Duck Simulator")]

namespace Duck_Connect
{
    public class DuckObject
    {
        public string DuckID { get; set; }
        public string DuckName { get; set; }
    }

    public class DuckConnect : MelonMod
    {
        public static DuckConnect _instance;

        private static GeneralManager _generalManager;
        private static readonly string _savePath = "./UserData/CustomNames.json";
        private static string _savecontent;
        public static bool NewDuck = false;
        public static Dictionary<string, string> _duckNames = new();
        public static Dictionary<string, int> _ducks = new();

        private MqttClient mqttClient;
        private static readonly string brokerIpAddress = "localhost";
        private static readonly int brokerPort = 1883;
        private static readonly  string clientId = "duck_client";
        private static readonly string tabTopic = "ppds/tab";
        private static readonly string quackTopic = "ppds/quack";
        private static readonly string duckNameTopic = "ppds/duckname";
        private static readonly string duckIDTopic = "ppds/duckid";

        private bool nameChange = false;
        private string newName = null;

        public override void OnEarlyInitializeMelon()
        {
            _instance = this;
        }

        public override void OnInitializeMelon()
        {
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
                harmony.PatchAll(typeof(GeneralManager_AddDuck));
                _instance.LoggerInstance.Msg("General Manager Start Patched!");

                harmony.PatchAll(typeof(GeneralManager_Start));
                _instance.LoggerInstance.Msg("General Manager Add Patched!");

                harmony.PatchAll(typeof(SpawnUpdate_Patch));
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
                _instance.LoggerInstance.Msg("MQTT Connected!");
            }
            catch (Exception e)
            {
                _instance.LoggerInstance.Msg(e);
            }
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            //Hook GeneralManager
            if ("Intro" != sceneName)
            {
                _ducks.Clear();
                _generalManager = Object.FindObjectOfType<GeneralManager>();
                if (_generalManager == null)
                {
                    _instance.LoggerInstance.Error("General Manager Didn't Hook!!");
                    return;
                }

                // Creates a second duck on game start so the tabbing logic works properly
                Traverse.Create(_generalManager).Field("spawnCounter").SetValue(1000);
            }
        }

        public override void OnLateUpdate()
        {
            var intro = SceneManager.GetActiveScene().name == "Intro";

            if (intro) { return; } // Check if Intro Scene is not loaded

            // Duck renaming can only occur OnLateUpdate, throws an error otherwise
            if (nameChange) 
            { 
                DuckRename(null, newName);
                nameChange = false;
            }
        }

        private void OnMqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            try
            {
                // Check which MQTT topic the message was received on
                if (e.Topic == tabTopic)
                {
                    OnTabMessageReceived(); // Ignore message, just look if a message was sent
                }
                else if (e.Topic == quackTopic)
                {
                    OnQuackMessageReceieved(); // Ignore message, just look if a message was sent
                }
                else if (e.Topic == duckNameTopic)
                {
                    string message = Encoding.UTF8.GetString(e.Message);
                    if (string.IsNullOrEmpty(message)) { return; }
                    
                    newName = message;
                    nameChange = true;
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

            var intro = SceneManager.GetActiveScene().name == "Intro";
            if (intro) { return; } // Check if Intro Scene is not loaded       

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
                
            catch(Exception e)
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

        //Harmony Patches
        [HarmonyPatch(typeof(GeneralManager), "AddDuck")]
        public class GeneralManager_AddDuck
        {
            //TODO: Fix Add 2 | Might be internal code \ Might be a bug in the game
            //BUG: Duck Not Renaming while Spawning a New Duck
            static void Postfix(ref GeneralManager __instance ,DuckManager duckManager, string duckID, bool unlock = true, bool addToList = true)
            {
                int count = 0;
                if (DuckConnect._ducks.ContainsKey(duckID))
                {
                    count = DuckConnect._ducks[duckID];
                    DuckConnect._ducks[duckID]++;
                }
                else
                {
                    DuckConnect._ducks[duckID] = 1;
                }
                string modifiedDuckID = duckID;
                if (count > 0)
                {
                    modifiedDuckID += "_" + count;
                }
                duckManager.duckID = modifiedDuckID;
                
                var duckNames = DuckConnect.GetName(modifiedDuckID);
                duckManager.NameChanged(modifiedDuckID, duckNames);

            }
        }
        [HarmonyPatch(typeof(GeneralManager), "Start")]
        public class GeneralManager_Start
        {
            static void Postfix(ref DuckManager ___base1Duck)
            {
                var duckNames = DuckConnect.GetName("Duck1Base");
                ___base1Duck.NameChanged("Duck1Base", duckNames);
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
                    if (code[i].opcode == OpCodes.Stfld && code[i+1].opcode == OpCodes.Ldarg_0)
                    {
                        insertionIndex = i;
                        code[i].labels.Add(returnDuck);
                        break;
                    }
                }

                var instructionsToInsert = new List<CodeInstruction>();

                instructionsToInsert.Add(new CodeInstruction(OpCodes.Ldc_I4_1, (sbyte)4));
                instructionsToInsert.Add(new CodeInstruction(OpCodes.Stsfld, AccessTools.Field(typeof(DuckConnect), "NewDuck")));

                if (insertionIndex != -1)
                {
                    code.InsertRange(insertionIndex, instructionsToInsert);
                }

                return code;
            }
        }
    }
}
