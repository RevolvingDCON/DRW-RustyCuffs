// UMod build

// DCON
// using Oxide.Ext.DCON;

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

//Oxide
using Oxide.Core.Plugins;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Plugins;
using Oxide.Game.Rust;
using Oxide.Game.Rust.Cui;
using Oxide.Game.Rust.Libraries;
using Oxide.Core;

//Rust
using Rust;
using Network;
using Network.Visibility;

//Unity
using UnityEngine;

// Yes I'm aware how hacky these methods are, It's almost impossible to achieve smooth anything given how limited mod support for rust is
// If there was a better more realistic way to achieve this I would have done it, if I haven't and there really is, I will update.
// I tried.

namespace Oxide.Plugins
{
    [Info("Rusty Cuffs", "Revolving DCON", "0.9.3")]
    [Description("Handcuffs allowing you to restrain and escort players")]

    public class RustyCuffs : CovalencePlugin
    {
        protected Oxide.Game.Rust.Libraries.Player Player = Interface.Oxide.GetLibrary<Oxide.Game.Rust.Libraries.Player>(null);
        
        // private static readonly DCONPlugin DCON = new DCONPlugin();
        // private static readonly Dictionary<string, string> AP = DCON.GetColors();

        #region variables

        private ulong cuffsSkinID = 2415236504;
        private string cuffsItemShortname = "metalspring";

        // private static ulong keysSkinID = 2415236504;
        private string keysItemShortname = "door.key";

        private StoredData storedData;
        private bool storageChanged;

        private Vector3 chairPositionOffset = new Vector3(0f,-1000f,0);

        // [PluginReference]
        // private Plugin DCON;

        private Dictionary<string, UIContainer> userUIContainers = new Dictionary<string, UIContainer>();
        private Dictionary<string, Timer> userTimers = new Dictionary<string, Timer>();
        private Dictionary<string,string> listenToUsers = new Dictionary<string,string>();
        private Dictionary<BasePlayer,BasePlayer> selectedUsers = new Dictionary<BasePlayer,BasePlayer>();
        private Dictionary<BasePlayer,BasePlayer> escortingUsers = new Dictionary<BasePlayer,BasePlayer>();
        private List<BasePlayer> usersInputDisabled = new List<BasePlayer>();
        private Dictionary<string,ChairHack> restrainChairs = new Dictionary<string,ChairHack>();
        private List<BaseMountable> chairEnts = new List<BaseMountable>();

        private int obstructionMask = LayerMask.GetMask("Construction", "Deployable", "Default", "Deployed", "Resource", "Terrain", "World", "Tree", "Impostor");
        private int baseMask = LayerMask.GetMask("Construction", "Deployable", "Deployed");

        private Dictionary<string,string> perms = new Dictionary<string,string>(){
            // primary perms
            {"admin","rustycuffs.admin"},
            {"use","rustycuffs.use"},
            {"unlimited","rustycuffs.unlimited"},
            {"usecuffkeys","rustycuffs.usecuffkeys"},
            {"lockpick","rustycuffs.lockpick"},

            // button perms
            {"escort","rustycuffs.escort"},
            {"viewinventory","rustycuffs.viewinventory"},
            {"execute","rustycuffs.execute"},
            {"createkey","rustycuffs.createkey"},
            {"unrestrain","rustycuffs.unrestrain"},
        };

        private new Dictionary<string, string> messages = new Dictionary<string, string> {
            // notifications player
            ["scriptreload"] = "Script reload, escorting stopped",
            ["givecuffs"] = "Gave cuffs to {0}",
            ["keycreated"] = "Key created for {0}",
            ["restrained"] = "{0} was restrained",
            ["unrestrained"] = "{0} was unrestrained",

            // notifications target
            ["restrained_tgt"] = "You have been restrained",
            ["unrestrained_tgt"] = "You have been unrestrained",
            ["givekey_tgt"] = "You got keys",
            ["givecuffs_tgt"] = "You got cuffs",
            ["bot_created"] = "Bot created at your location",
            ["restraining_tgt"] = "You are being restrained",

            // errors
            ["error_noperms"] = "You do not have permission to use this command",
            ["error_noperms_keys"] = "You do not have permission to use cuff keys",
            ["error_noperms_cuffs"] = "You do not have permission to use cuffs",
            ["error_not_restrained"] = "{0} is not restrained",
            ["error_select_self"] = "You can not select yourself",
            ["error_restrain_self"] = "You can not restrain yourself",
            ["error_tgt_selected"] = "{0} is already selected",
            ["error_tgt_restrained"] = "{0} is already restrained",
            ["error_restrain_npc"] = "Can not restrain NPCs",
            ["error_restrained_cuff_use"] = "You can not use cuffs while restrained",
            ["error_static_ground"] = "Must be standing on static ground",
            ["error_cuffs_missing"] = "Can not find cuffs in tool belt",
            ["error_key_missing"] = "Can not find correct key in tool belt",
            ["error_wrong_key"] = "This key is not for {0}",
            ["error_mount_escorting"] = "Can not mount while escorting",
            ["error_escort_self"] = "You can not escort yourself..",
            ["error_escort_view_obstructed"] = "Can not escort, view obstructed",
            ["error_escort_stop_view_obstructed"] = "Can not stop escorting, view obstructed",
            ["error_unrestrain_view_obstructed"] = "Can not unrestrain, view obstructed",

            ["error_invalid_selection"] = "Invalid number, use the number in front of the player's name. Use /{0} to check the list of players again",
            ["error_multiple_players_found"] = "Multiple players matching: {1}, please select one of these players by using /{0} list <number>:",
            ["error_no_list_available"] = "You do not have a players list available for /{0}",
            ["error_no_players_found"] = "Couldn't find any players matching: {0}" ,
            ["error_too_many_players_found"] = "Too many players were found, the list of matches is only showing the first 5. Try to be more specific" 
        };

        private static RustyCuffs _ins;
        private Configuration config;

        #endregion

        #region lang

        // umod compliance
        protected override void LoadDefaultMessages() => lang.RegisterMessages(messages, this);

        #endregion

        #region config

        public class Configuration {
            [JsonProperty(PropertyName = "Chat Prefix")]
            public string prefix = "[+16][#00ffff]Rusty Cuffs[/#][/+]: ";

            [JsonProperty(PropertyName = "Chat Icon")]
            public ulong icon = 76561199105408156;

            // [JsonProperty(PropertyName = "Destroy Key [Destroy key when player is unrestrained]")]
            // public bool destroyKey = true;

            [JsonProperty(PropertyName = "Return Cuffs [Give cuffs back when player is unrestrained]")]
            public bool returnCuffs = false;

            [JsonProperty(PropertyName = "Restrain Time [How long does it take to restrain a player]")]
            public float restrainTime = 1;

            [JsonProperty(PropertyName = "Restrain Distance [Maximum distance players can be restrained from]")]
            public float restrainDist = 2f;

            [JsonProperty(PropertyName = "Escort Distance [Distance players are while being escorted]")]
            public float escortDist = 0.9f;

            [JsonProperty(PropertyName = "Restrain NPCs [Can NPCs be restrained]")]
            public bool npcsEnabled = false;
        }

        protected override void LoadConfig() {
            base.LoadConfig();
            try {
                config = Config.ReadObject<Configuration>();
                if (config == null) {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig(){
            string configPath = $"{Interface.Oxide.ConfigDirectory}{Path.DirectorySeparatorChar}{Name}.json";
            LogWarning($"Could not load a valid configuration file, creating a new configuration file at {configPath}");
            config = new Configuration();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region data

        public class StoredData {
            public Dictionary<uint, string> keys = new Dictionary<uint, string>();
            public Dictionary<string, string> restrained = new Dictionary<string, string>();
        }

        private void SaveData(){
            if(storageChanged){
                storageChanged = false;

                Puts("Saving Rusty Cuffs");
                Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
            }
        }
        private void OnServerSave() => SaveData();

        #endregion

        #region setup

        private void Init() {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            _ins = this;

            AddCovalenceCommand(new []{
                "restrain",
                "unrestrain",

                "cuffsmenu",
                "cuffskey",
                "cuffkeys",
                "cuffkey",
                "cuffs",

                "cuffsbot"
            },"RestrainCmd");

            AddCovalenceCommand(new []{"rustycuffs.ui_btn_callback"},"UIBtnCallback");

            foreach(var perm in perms.Values){
                permission.RegisterPermission(perm, this);
            }
        }

        private void OnServerInitialized() {
            ConsoleNetwork.BroadcastToAllClients($"cinematic_stop");

            foreach(IPlayer pr in players.Connected){
                if(storedData.restrained.Keys.Contains(pr.Id)){
                    DisableUserInput(pr.Object as BasePlayer);
                }
            }

            foreach (var player in BasePlayer.activePlayerList){
                ConsoleNetwork.BroadcastToAllClients($"cinematic_stop {player.UserIDString}");
            }

            timer.Every(20f,() => {
                // Puts("Syncing handcuffs aimation state al all clients");
                foreach(var id in storedData.restrained.Keys){
                    PlayNetworkAnimation(id,$"cinematic_play idle_stand_handcuff {id}");
                }
            });
        }

        private void Unload() {
            RestrainInspector[] inspectors = UnityEngine.Object.FindObjectsOfType<RestrainInspector>();
            foreach (RestrainInspector inspector in inspectors)inspector.Remove();

            ForceLocation[] forceLocations = UnityEngine.Object.FindObjectsOfType<ForceLocation>();
            foreach (ForceLocation forceLocation in forceLocations)forceLocation.Remove();

            AntiHack[] antihacks = UnityEngine.Object.FindObjectsOfType<AntiHack>();
            foreach (AntiHack antihack in antihacks)antihack.Remove();

            foreach (var player in BasePlayer.activePlayerList){      
                if(userUIContainers.ContainsKey(player.UserIDString)){
                    userUIContainers[player.UserIDString].Progress.Destroy();
                    userUIContainers[player.UserIDString].Menu.Destroy();
                }

                if(escortingUsers.ContainsKey(player)){
                    SendMessage(player,new[]{"scriptreload"});
                    StopEscorting(player,Vector3.zero,true);
                }
            }

            foreach(IPlayer pr in players.Connected){
                if(storedData.restrained.Keys.Contains(pr.Id)){
                    EnableUserInput(pr.Object as BasePlayer);
                }
            }

            SaveData();

            _ins = null;
        }

        #endregion

        #region chat command

        private void RestrainCmd(IPlayer player, string command, string[] args){
            if(!player.HasPermission(perms["admin"])){
                SendMessage(player,new[]{"error_noperms"});
                return;
            }

            BasePlayer bplayer = player.Object as BasePlayer;
            UIContainer UICont;

            string name = string.Join(" ", args);
            BasePlayer target = null;

            if(name == "")target = bplayer;

            if(target == null){
                if(!FindPlayer(name,player,command,ref args,ref target))return;
            }

            if(!userUIContainers.ContainsKey(bplayer.UserIDString)){
                UICont = new UIContainer(bplayer,target);

                userUIContainers[bplayer.UserIDString] = UICont;
            }
            else
            {
                UICont = userUIContainers[bplayer.UserIDString];
            }

            switch(command.ToLower()){
                case "cuffs":
                case "givecuffs":
                    GiveItem(target,CreateCuffs(1));

                    SendMessage(player,new[]{"givecuffs",target.displayName});
                    PlayEffect(target,"assets/prefabs/weapons/arms/effects/pickup_item.prefab");

                    SendMessage(target,new[]{"givecuffs_tgt"});
                break;
                case "cuffskey":
                case "cuffkeys":
                case "cuffkey":
                    Item key = CreateCuffsKey(target.displayName);
                    storedData.keys[key.uid] = target.UserIDString;
                    GiveItem(bplayer,key);

                    SendMessage(player,new[]{"keycreated",target.displayName});
                    // SendMessage(target,new[]{"givekey_tgt"});

                    PlayEffect(player,"assets/prefabs/weapons/arms/effects/pickup_item.prefab");
                break;
                case "cuffsmenu":
                    if(!IsRestrained(target)){
                        SendMessage(player,new[]{"error_not_restrained",target.displayName});

                        return;
                    }

                    if(bplayer == target){
                        SendMessage(player,new[]{"error_select_self"});
                        break;
                    }

                    UICont.UpdateTarget(target);

                    if(!PlayerSelect(bplayer,target,false)){
                        SendMessage(player,new[]{"error_tgt_selected",target.displayName});
                    }
                break;
                case "restrain":
                    if(IsRestrained(target)){
                        SendMessage(player,new[]{"error_tgt_restrained",target.displayName});
                        return;
                    }

                    if(Restrain(target,bplayer)){
                        SendMessage(player,new[]{"restrained",target.displayName});
                        SendMessage(target,new[]{"restrained_tgt"});

                        PlayEffect(target,"assets/prefabs/building/wall.frame.fence/effects/chain-link-fence-deploy.prefab",true);
                    }
                break;
                case "unrestrain":
                    if(!IsRestrained(target)){
                        SendMessage(player,new[]{"error_not_restrained",target.displayName});
                        return;
                    }

                    if(Unrestrain(target)){
                        SendMessage(player,new[]{"unrestrained",target.displayName});
                        SendMessage(target,new[]{"unrestrained_tgt"});

                        PlayEffect(target,"assets/prefabs/deployable/signs/effects/large-banner-deploy.prefab",true);
                    };

                break;
                case "cuffsbot":
                    CreateBot(bplayer.transform.position);
                    SendMessage(player,new[]{"bot_created"});
                break;
            }
        }

        #endregion

        #region hooks

        private void OnPlayerInput(BasePlayer player, InputState state){
            if(state.WasJustPressed(BUTTON.RELOAD)){
                if(!listenToUsers.ContainsKey(player.UserIDString))return;

                if(!permission.UserHasPermission(player.UserIDString, perms["use"])){
                    SendMessage(player,new[]{"error_noperms_cuffs"});
                    return;
                }

                // player.SetParent(null,true,true);

                string item = listenToUsers[player.UserIDString];

                BasePlayer target;
                UIContainer UICont = null;

                target = RayToPlayer(player);
                if(escortingUsers.ContainsKey(player)){
                    target = escortingUsers[player];
                }

                if(target == null)return;

                if(IsNPC(target) && !config.npcsEnabled){
                    SendMessage(player,new[]{"error_restrain_npc"});
                    return;
                }

                // Puts($"ID: {player.UserIDString}");

                if(!userUIContainers.ContainsKey(player.UserIDString)){
                    UICont = new UIContainer(player,target);

                    userUIContainers[player.UserIDString] = UICont;
                    // Puts($"ID: {player.UserIDString}");

                    // Puts($"New Cont - Owner:{UICont.player.displayName} - NPC:{UICont.target.displayName}");
                }
                else
                {
                    UICont = userUIContainers[player.UserIDString];
                }

                UICont.UpdateTarget(target);
                // BroadcastPlayer(player,$"UI Cont - Owner:{UICont.player.displayName} - NPC:{UICont.target.displayName}");

                float dist = Vector3.Distance(player.transform.position,target.transform.position);
                if(dist > config.restrainDist && !escortingUsers.ContainsKey(player))return;

                switch(item){
                    case "cuffs":
                        if(storedData.restrained.ContainsKey(player.UserIDString)){
                            SendMessage(player,new[]{"error_restrained_cuff_use"});
                            return;
                        }

                        // Puts($"Use Key Pressed: Cuffs");

                        if(userTimers.ContainsKey(player.UserIDString))userTimers[player.UserIDString].Destroy();

                        if(IsRestrained(target)){
                            if(Interface.CallHook("CanCuffsPlayerUseCuffs", target, player) != null) return;

                            if(permission.UserHasPermission(player.UserIDString, perms["unlimited"])){
                                if(!permission.UserHasPermission(player.UserIDString, perms["usecuffkeys"])){
                                    SendMessage(player,new[]{"error_noperms_keys"});
                                    return;
                                }

                                if(!PlayerSelect(player,target) && !PlayerDeselect(player)){
                                    // BroadcastPlayer(player,$"{target.displayName} is already selected");
                                }

                                Interface.CallHook("OnCuffsPlayerUseCuffs", target, player);

                                return;
                            }
                            else
                            {
                                SendMessage(player,new[]{"error_tgt_restrained",target.displayName});
                                return;
                            }
                        }
                        else
                        {
                            if(escortingUsers.ContainsKey(player)){
                                if(!PlayerSelect(player,target)){
                                    SendMessage(player,new[]{"error_tgt_selected",target.displayName});
                                }
                                return;
                            }
                        }

                        if(player.GetParentEntity() != null) {
                            SendMessage(player,new[]{"error_static_ground"});
                            return;
                        }

                        if(player == target) {
                            SendMessage(player,new[]{"error_restrain_self"});
                            return;
                        }

                        if(Interface.CallHook("CanCuffsPlayerStartRestrain", target, player) != null)return;

                        int complete = 0;
                        float duration = config.restrainTime;
                        float steps = 10;
                        // Timer toktoktok = null;

                        // being restrained
                        UICont.Progress.Draw();

                        SendMessage(target,new[]{"restraining_tgt"});

                        userTimers[player.UserIDString] = timer.Every((duration/steps),() => {
                            complete++;

                            if(complete > steps){
                                StopProgress(player,UICont);
                                if(storedData.restrained.ContainsKey(target.UserIDString)){
                                    SendMessage(player,new[]{"error_tgt_restrained",target.displayName});
                                    return;
                                };

                                Item cuffs = null;

                                foreach(Item cItem in player.inventory.containerBelt.itemList){
                                    if(cItem.info.shortname == cuffsItemShortname && cItem.skin == cuffsSkinID){
                                        cuffs = cItem;
                                        break;
                                    }
                                }

                                if(cuffs == null){
                                    SendMessage(player,new[]{"error_cuffs_missing"});
                                    return;
                                }

                                if(!permission.UserHasPermission(player.UserIDString, perms["unlimited"])){
                                    if(cuffs.amount > 1){
                                        cuffs.MarkDirty();
                                        cuffs.amount--;
                                    }
                                    else
                                    {
                                        cuffs.Remove();
                                        cuffs.DoRemove();
                                    }

                                    Item key = CreateCuffsKey(target.displayName);
                                    storedData.keys[key.uid] = target.UserIDString;
                                    GiveItem(player,key);
                                }

                                if(Restrain(target,player)){
                                    SendMessage(player,new[]{"restrained",target.displayName});
                                    SendMessage(target,new[]{"restrained_tgt"});

                                    PlayEffect(target,"assets/prefabs/building/wall.frame.fence/effects/chain-link-fence-deploy.prefab",true);
                                }

                                storageChanged = true;

                                Interface.CallHook("OnCuffsPlayerUseCuffs", target, player);

                                // userTimers[player.UserIDString].Destroy();
                                return;
                            }

                            target = RayToPlayer(player);
                            if(target != null)dist = Vector3.Distance(player.transform.position,target.transform.position);
                            if(target == null || dist > config.restrainDist){
                                StopProgress(player,UICont);
                                return;
                            }

                            UICont.Progress.Update((complete/steps));
                        });
                    break;
                    case "key":
                        if(!permission.UserHasPermission(player.UserIDString, perms["usecuffkeys"])){
                            SendMessage(player,new[]{"error_noperms_keys"});
                            return;
                        }
                        
                        if(Interface.CallHook("CanCuffsPlayerUseKey", target, player) != null)return;

                        if(storedData.restrained.ContainsKey(player.UserIDString)){
                            SendMessage(player,new[]{"error_restrained_cuff_use"});
                            return;
                        }

                        // Puts($"Use Key Pressed: Key");

                        if(!IsRestrained(target)){
                            SendMessage(player,new[]{"error_not_restrained",target.displayName});
                            return;
                        }

                        Item activeItem = player.GetActiveItem();

                        if(!storedData.keys.ContainsKey(activeItem.uid) || storedData.keys[activeItem.uid] != target.UserIDString){
                            SendMessage(player,new[]{"error_wrong_key",target.displayName});
                            return;
                        }

                        if(!PlayerSelect(player,target)){
                            SendMessage(player,new[]{"error_tgt_selected",target.displayName});
                            return;
                        }

                        Interface.CallHook("OnCuffsPlayerUseKey", target, player);
                    break;
                }
            }

            if(state.WasJustReleased(BUTTON.RELOAD)){
                if(!listenToUsers.ContainsKey(player.UserIDString) || !userTimers.ContainsKey(player.UserIDString) || !userUIContainers.ContainsKey(player.UserIDString))return;
                string item = listenToUsers[player.UserIDString];
                
                switch(item){
                    case "cuffs":
                        // Puts($"Use Key Released: Cuffs");

                        UIContainer UICont = userUIContainers[player.UserIDString];
                        StopProgress(player,UICont);
                    break;
                    case "key":
                        // Puts($"Use Key Released: Key");

                        
                    break;
                }
            }
        }

        void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem){
            listenToUsers.Remove(player.UserIDString);
            UIContainer UICont = null;

            if(userUIContainers.ContainsKey(player.UserIDString)){
                UICont = userUIContainers[player.UserIDString];
                StopProgress(player,UICont);
            }

            if(oldItem != null && oldItem.skin == cuffsSkinID){
                
            }

            // check newly selected item
            if(newItem != null && newItem.skin == cuffsSkinID){
                listenToUsers.Add(player.UserIDString,"cuffs");
                return;
            }

            if(newItem != null && newItem.info.itemid == -1112793865 && newItem.name != ""){
                listenToUsers.Add(player.UserIDString,"key");
                // Puts($"key {newItem.name}");
                return;
            }
        }

        private void OnPlayerRevive(BasePlayer reviver, BasePlayer player){
            if(storedData.restrained.ContainsKey(player.UserIDString))PlayNetworkAnimation(player.UserIDString,$"cinematic_play idle_stand_handcuff {player.UserIDString}");
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info){
            listenToUsers.Remove(player.UserIDString);

            if(storedData.restrained.ContainsKey(player.UserIDString)){
                if(config.returnCuffs && !permission.UserHasPermission(storedData.restrained[player.UserIDString], perms["unlimited"])){
                    DropItem(CreateCuffs(),player.transform.position);
                }

                Unrestrain(player);
            }

            if(escortingUsers.ContainsKey(player)){
                StopEscorting(player,Vector3.zero);
            }
        }

        void OnPlayerSleepEnded(BasePlayer player){
            if(!storedData.restrained.ContainsKey(player.UserIDString))return;

            SendMessage(player,new[]{"restrained_tgt"});
            timer.In(0.5f,() => {
                Restrain(player);
            });
        }

        void OnPlayerConnected(BasePlayer player){
            if(!storedData.restrained.ContainsKey(player.UserIDString))return;
            listenToUsers.Remove(player.UserIDString);
            EnableUserInput(player);
        }

        void OnPlayerDisconnected(BasePlayer player){
            if(userUIContainers.ContainsKey(player.UserIDString)){
                userUIContainers[player.UserIDString].Progress.Destroy();
                userUIContainers[player.UserIDString].Menu.Destroy();

                userUIContainers.Remove(player.UserIDString);
            }

            if(escortingUsers.ContainsKey(player)){
                StopEscorting(player,Vector3.zero);
            }

            if(escortingUsers.ContainsValue(player)){
                BasePlayer p = escortingUsers.FirstOrDefault(x => x.Value == player).Key;
                StopEscorting(p,Vector3.zero,true);
                // EnableUserInput(player);
            }
        }

        private object CanLootPlayer(BasePlayer target, BasePlayer looter){
            if(storedData.restrained.ContainsKey(looter.UserIDString))return false;
            if (looter.GetComponent<RestrainInspector>() == null){
                return null;
            }

            return true;
        }

        private object CanLootEntity(BasePlayer player, StorageContainer container){
            if(storedData.restrained.ContainsKey(player.UserIDString))return false;
            return null;
        }
        private object CanLootEntity(BasePlayer player, LootableCorpse container){
            if(storedData.restrained.ContainsKey(player.UserIDString))return false;
            return null;
        }
        private object CanLootEntity(BasePlayer player, DroppedItemContainer container){
            if(storedData.restrained.ContainsKey(player.UserIDString))return false;
            return null;
        }

        private object CanPickupEntity(BasePlayer player, BaseCombatEntity ent){
            if(storedData.restrained.ContainsKey(player.UserIDString))return false;
            return null;
        }

        private object CanMountEntity(BasePlayer player, BaseMountable mount){
            if(storedData.restrained.ContainsKey(player.UserIDString) && !chairEnts.Contains(mount))return false;
            if(escortingUsers.ContainsKey(player)){
                SendMessage(player,new[]{"error_mount_escorting"});
                return false;
            }
            return null;
        }

        // object CanDismountEntity(BasePlayer player, BaseMountable entity){
        //     if(storedData.restrained.ContainsKey(player.UserIDString))return false;
        //     return null;
        // }

        private object CanUnlock(BasePlayer player, BaseLock baseLock){
            if(storedData.restrained.ContainsKey(player.UserIDString))return false;
            return null;
        }

        private void OnServerCommand(ConsoleSystem.Arg arg){
            if (arg.cmd.FullName == "inventory.endloot")
            {
                BasePlayer player = arg.Player();
                player.GetComponent<RestrainInspector>()?.Remove();
            }
        }

        private object CanSpectateTarget(BasePlayer player, string name){
            if(usersInputDisabled.Contains(player))return false;
            return null;
        }

        // plugin hooks - credit NoEscape
        object CanTeleport (BasePlayer player){
            return IsRestrained(player)?"Can not use this command while restrained":null;
        }

        object canTeleport (BasePlayer player){
            return IsRestrained(player)?"Can not use this command while restrained":null;
        }

        object CanGridTeleport (BasePlayer player){
            return IsRestrained(player)?"Can not use this command while restrained":null;
        }

        #endregion

        #region UI

        private void UIBtnCallback(IPlayer player, string command, string[] args){
            BasePlayer bplayer = player.Object as BasePlayer;
            command = args[0];
            UIContainer UICont = userUIContainers[player.Id.ToString()];
            Item key = null;

            // BroadcastPlayer(bplayer,$"{command} - Owner:{UICont.player.displayName} - NPC:{UICont.target.displayName}");

            switch(command.ToLower()){
                case "close":
                    PlayerDeselect(bplayer);
                break;
                case "viewinventory":
                    PlayerDeselect(bplayer);
                    if(StartInspecting(UICont.target,bplayer)){
                        PlayEffect(player,"assets/prefabs/misc/summer_dlc/beach_chair/effects/beach-parasol-deploy.prefab",true);
                    }
                break;
                case "execute":
                    PlayerDeselect(bplayer);
                    if(Interface.CallHook("CanCuffsPlayerExecute", UICont.target, bplayer) != null) break;

                    PlayEffect(UICont.target,"assets/bundled/prefabs/fx/player/gutshot_scream.prefab",true);
                    PlayEffect(UICont.target,"assets/bundled/prefabs/fx/takedamage_generic.prefab");
                    PlayEffect(UICont.target,"assets/bundled/prefabs/fx/headshot.prefab",true);

                    UICont.target.Hurt(100, Rust.DamageType.Stab);

                    Interface.CallHook("OnCuffsPlayerExecute", UICont.target, bplayer);
                break;
                case "escort":
                    PlayerDeselect(bplayer);

                    if(bplayer.GetParentEntity() != null) {
                        SendMessage(player,new[]{"error_static_ground"});
                        PlayEffect(player,"assets/prefabs/locks/keypad/effects/lock.code.denied.prefab");
                        return;
                    }

                    if(UICont.target == bplayer){
                        SendMessage(player,new[]{"error_escort_self"});
                        break;
                    }

                    if(!escortingUsers.ContainsKey(bplayer)){
                        if(IsViewObstructed(bplayer,obstructionMask)){
                            SendMessage(player,new[]{"error_escort_view_obstructed"});
                            PlayEffect(player,"assets/prefabs/locks/keypad/effects/lock.code.denied.prefab");
                            break;
                        }

                        StartEscorting(UICont.target,bplayer);
                        PlayEffect(UICont.target,"assets/prefabs/deployable/small stash/effects/small-stash-deploy.prefab",true);
                    }
                    else
                    {
                        if(IsViewObstructed(bplayer,obstructionMask)){
                            SendMessage(player,new[]{"error_escort_stop_view_obstructed"});
                            PlayEffect(player,"assets/prefabs/locks/keypad/effects/lock.code.denied.prefab");
                            break;
                        }

                        StopEscorting(bplayer,Vector3.zero);
                        PlayEffect(UICont.target,"assets/prefabs/deployable/sleeping bag/effects/sleeping-bag-deploy.prefab",true);
                    }
                break;
                case "createkey":
                    PlayerDeselect(bplayer);
                    if(Interface.CallHook("CanCuffsPlayerCreateKey", UICont.target, bplayer) != null) break;

                    key = CreateCuffsKey(UICont.target.displayName);
                    storedData.keys[key.uid] = UICont.target.UserIDString;
                    GiveItem(bplayer,key);

                    SendMessage(player,new[]{"keycreated",UICont.target.displayName});

                    PlayEffect(player,"assets/prefabs/weapons/arms/effects/pickup_item.prefab");

                    Interface.CallHook("OnCuffsPlayerCreateKey", UICont.target, bplayer);
                    break;
                break;
                case "unrestrain":
                    PlayerDeselect(bplayer);
                    if(bplayer.GetParentEntity() != null) {
                        SendMessage(player,new[]{"error_static_ground"});

                        PlayEffect(player,"assets/prefabs/locks/keypad/effects/lock.code.denied.prefab");
                        return;
                    }

                    if(IsViewObstructed(bplayer,obstructionMask)){
                        SendMessage(player,new[]{"error_unrestrain_view_obstructed"});
                        PlayEffect(player,"assets/prefabs/locks/keypad/effects/lock.code.denied.prefab");
                        break;
                    }

                    if(!permission.UserHasPermission(bplayer.UserIDString, perms["unlimited"])){
                        key = null;

                        foreach(Item item in bplayer.inventory.containerBelt.itemList){
                            if(storedData.keys.ContainsKey(item.uid) && storedData.keys[item.uid] == UICont.target.UserIDString){
                                key = item;
                                break;
                            }
                        }

                        if(key == null){
                            SendMessage(player,new[]{"error_key_missing"});
                            PlayEffect(player,"assets/prefabs/locks/keypad/effects/lock.code.denied.prefab");
                            break;
                        }

                        key.Remove();
                        key.DoRemove();

                        if(config.returnCuffs){
                            GiveItem(bplayer,CreateCuffs());
                        }
                    }

                    StopEscorting(bplayer,Vector3.zero);
                    
                    if(Unrestrain(UICont.target)){
                        SendMessage(player,new[]{"unrestrained",UICont.target.displayName});
                        SendMessage(UICont.target,new[]{"unrestrained_tgt"});

                        PlayEffect(UICont.target,"assets/prefabs/deployable/signs/effects/large-banner-deploy.prefab",true);
                    }

                    UICont.UpdateTarget(null);
                break;
            }

            // PlayEffect(player,"assets/prefabs/tools/detonator/effects/attack.prefab");
            // PlayEffect(player,"assets/prefabs/deployable/dropbox/effects/submit_items.prefab");

            // Puts($"Here! {command}");
        }

        public class UIContainer {
            // Yes I know I should have built this class differently, I will refactor next update.
            public static string panel_overlay_cuffs_id = "panel_overlay_cuffs_id";
            public static string panel_overlay_menu_id = "panel_overlay_menu_id";

            public BasePlayer player;
            public BasePlayer target;

            public readonly ProgressBar Progress = new ProgressBar();
            public readonly DynamicMenu Menu = new DynamicMenu();

            public UIContainer(BasePlayer p, BasePlayer t){
                player = p;
                target = t;

                Update();
            }

            public void Update() {
                Progress.Update(player,target);
                Menu.Update(player,target);
            }

            public void UpdateTarget(BasePlayer t) {
                target = t;
                Update();
            }

            public class ProgressBar {
                private CuiElementContainer parentContainer;
                private string Name = "progress";
                private string Parent;

                private BasePlayer player;
                private BasePlayer target;

                public void Update(BasePlayer p, BasePlayer t) {
                    player = p;
                    target = t;
                }

                public void Draw(){
                    Destroy();
                    parentContainer = new CuiElementContainer();

                    var background = parentContainer.Add(new CuiPanel {
                        Image = {
                            Color = $"1 0 0 0",
                        },
                        RectTransform = {
                            AnchorMin = $"0.5 0.5",
                            AnchorMax = $"0.5 0.5",

                            OffsetMin = $"{-90} {-16 + 4}",
                            OffsetMax = $"{90} {16 + 4}"
                        },
                    },"Hud",panel_overlay_cuffs_id);

                    Parent = background;

                    CuiElement title = new CuiElement {
                        Name = "title",
                        Parent = background,
                        Components = {
                            new CuiTextComponent  {
                                Text = $"Restraining",
                                FontSize = 16,
                                Color = "1.0 1.0 1.0 1.0",
                                Align = TextAnchor.UpperCenter
                            },
                            new CuiRectTransformComponent {
                                AnchorMin = $"0 1",
                                AnchorMax = $"1 1",

                                OffsetMin = "0 -30",
                                OffsetMax = "0 0",
                            }
                        }
                    };
                    parentContainer.Add(title);

                    CuiHelper.AddUi(player, parentContainer);
                    UpdateFill(0);
                }

                public void UpdateFill(double amount = 0){
                    string el_name = $"{Name}_bar";
                    CuiHelper.DestroyUi(player,el_name);

                    CuiElementContainer container = new CuiElementContainer();

                    CuiElement progress_bg = new CuiElement {
                        Name = el_name,
                        Parent = Parent,
                        Components = {
                            new CuiImageComponent {
                                Color = "1 1 1 0.5" // "0 0 0 0.6" - nice black,
                            },
                            new CuiRectTransformComponent {
                                AnchorMin = $"0 0",
                                AnchorMax = $"1 0",

                                OffsetMin = "0 0",
                                OffsetMax = "0 10",
                            }
                        }
                    };
                    container.Add(progress_bg);

                    CuiElement progress_fill = new CuiElement {
                        Name = el_name+"_fill",
                        Parent = progress_bg.Name,
                        Components = {
                            new CuiImageComponent {
                                Color = "1 1 1 1" // "0 0 0 0.6" - nice black,
                            },
                            new CuiRectTransformComponent {
                                AnchorMin = $"0 0",
                                AnchorMax = $"{amount} 1",

                                OffsetMin = "0 0",
                                OffsetMax = "0 0",
                            }
                        }
                    };
                    container.Add(progress_fill);

                    parentContainer.AddRange(container);

                    CuiHelper.AddUi(player, container);
                }

                public void Update(float amount = 0){
                    UpdateFill(amount);
                }

                public void Destroy(){
                    parentContainer = new CuiElementContainer();
                    CuiHelper.DestroyUi(player,panel_overlay_cuffs_id);
                }
            }

            public class DynamicMenu {
                private CuiElementContainer parentContainer;
                private string Name = "menu";

                private BasePlayer player;
                private BasePlayer target;

                public void Update(BasePlayer p, BasePlayer t) {
                    player = p;
                    target = t;
                }

                public void Draw(){
                    List<string[]> buttons = new List<string[]>(){
                        new[]{"Execute","execute","0.1 0.1 0.1 0.8",_ins.perms["execute"]},
                        new[]{"View Inventory","viewinventory","0.1 0.1 0.1 0.8",!_ins.escortingUsers.ContainsKey(player)?_ins.perms["viewinventory"]:"FAKE_PERM_ASDF"},
                        new[]{"Get Key","createkey","0.1 0.1 0.1 0.8",!_ins.escortingUsers.ContainsKey(player)?_ins.perms["createkey"]:"FAKE_PERM_ASDF"},
                        new[]{(target != null && _ins.escortingUsers.ContainsKey(player))?"Stop Escorting":"Escort","escort","0.1 0.1 0.1 0.8",_ins.perms["escort"]},
                        new[]{"Unrestrain","unrestrain","0.1 0.1 0.1 0.8",_ins.perms["unrestrain"]},
                        new[]{"Close","close","1 0 0 0.8",_ins.perms["use"]}, // dont change this or people will get stuck
                    };

                    for (int indx = buttons.Count - 1; indx >= 0; indx--){
                        if(!_ins.permission.UserHasPermission(player.UserIDString, buttons[indx][3]))buttons.RemoveAt(indx);
                    }

                    Destroy();
                    parentContainer = new CuiElementContainer();

                    var panel = parentContainer.Add(new CuiPanel {
                        Image = {
                            Color = $"1 0 0 0",
                        },
                        RectTransform = {
                            AnchorMin = $"0 0",
                            AnchorMax = $"1 1",

                            
                        },
                        CursorEnabled = true
                    },"Overlay",panel_overlay_menu_id);

                    CuiElement background = new CuiElement {
                        Name = $"{Name}_bg",
                        Parent = panel,
                        Components = {
                            new CuiButtonComponent {
                                Color = "1 0 0 0",
                                Command = $"rustycuffs.ui_btn_callback close",
                            },
                            new CuiRectTransformComponent {
                                AnchorMin = $"0 0",
                                AnchorMax = $"1 1",

                                OffsetMin = $"0 0",
                                OffsetMax = $"0 0",
                            }
                        },
                    };
                    parentContainer.Add(background);

                    CuiElement elements = new CuiElement {
                        Name = $"{Name}_btns_container",
                        Parent = background.Name,
                        Components = {
                            new CuiImageComponent {
                                Color = "0 0 0 0.6" // "0 0 0 0.6" - nice black,
                            },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.5 0.5",
                                AnchorMax = $"0.5 0.5",

                                OffsetMin = $"{-100} -{(((6+32) * (double.Parse(buttons.Count.ToString())/2)) + 6) + 10}",
                                OffsetMax = $"{100} {(((6+32) * (double.Parse(buttons.Count.ToString())/2)) + 6) - 10}",
                            }
                        }
                    };
                    parentContainer.Add(elements);

                    CuiElement title = new CuiElement {
                        Name = $"{Name}_title",
                        Parent = elements.Name,
                        Components = {
                            new CuiTextComponent  {
                                Text = target.displayName,
                                FontSize = 22,
                                Color = "1.0 1.0 1.0 1.0",
                                Align = TextAnchor.UpperCenter
                            },
                            new CuiOutlineComponent {
                                Color = "0 0 0 1",
                                Distance = "1 -1"
                            },
                            new CuiRectTransformComponent {
                                AnchorMin = $"0 1",
                                AnchorMax = $"1 1",

                                OffsetMin = "0 -60",
                                OffsetMax = "0 30",
                            }
                        }
                    };
                    parentContainer.Add(title);

                    int i = 0;
                    foreach(var cmds in buttons){
                        OptionBtn(cmds[0],cmds[1],cmds[2],i);
                        i++;
                    }

                    CuiHelper.AddUi(player, parentContainer);
                    _ins.PlayEffect(player,"assets/prefabs/tools/detonator/effects/attack.prefab");
                }

                private void OptionBtn (string label, string command, string color, float posy) {
                    double height = 32;
                    double gap = 6;
                    double offset = ((height+gap) * posy);
                    double padding = 8;

                    CuiElement btn_el = new CuiElement {
                        Name = $"{Name}_{posy}",
                        Parent = $"{Name}_btns_container",
                        Components = {
                            new CuiButtonComponent {
                                Color = color,
                                Command = $"rustycuffs.ui_btn_callback {command}",
                            },
                            new CuiRectTransformComponent {
                                AnchorMin = $"0 1",
                                AnchorMax = $"1 1",

                                OffsetMin = $"{padding} -{height + (offset + padding)}",
                                OffsetMax = $"-{padding} {0 - (offset + padding)}",
                            }
                        },
                    };
                    parentContainer.Add(btn_el);

                    CuiElement label_el = new CuiElement {
                        Name = $"{Name}_{posy}_label",
                        Parent = btn_el.Name,
                        Components = {
                            new CuiTextComponent  {
                                Text = label,
                                FontSize = 16,
                                Color = "1.0 1.0 1.0 1.0",
                                Align = TextAnchor.MiddleCenter
                            },
                            // new CuiOutlineComponent {
                            //     Color = "0 0 0 1",
                            //     Distance = "1 -1"
                            // },
                            new CuiRectTransformComponent {
                                AnchorMin = $"0 0",
                                AnchorMax = $"1 1",
                            }
                        }
                    };
                    parentContainer.Add(label_el);
                }

                public void Destroy(){
                    parentContainer = new CuiElementContainer();
                    CuiHelper.DestroyUi(player,panel_overlay_menu_id);
                }
            }
        }

        #endregion

        #region API

        private bool API_IsRestrained(BasePlayer player) => IsRestrained(player);
        private Item API_CreateCuffs(int amount) => CreateCuffs(amount);
        private Item API_CreateCuffsKey(BasePlayer player){
            Item key = CreateCuffsKey(player.displayName);
            storedData.keys[key.uid] = player.UserIDString;

            storageChanged = true;

            return key;
        }

        private bool API_Restrain(BasePlayer target, BasePlayer player) => Restrain(target,player);
        private bool API_Unrestrain(BasePlayer target) => Unrestrain(target);

        #endregion

        #region methods

        private bool IsNPC(BasePlayer player){
            if (player == null) return false;
            if (player is NPCPlayer) return true;
            if (!(player.userID >= 76560000000000000L || player.userID <= 0L))return true;
            return false;
        }

        private bool IsViewObstructed(BasePlayer player, int mask){
            Vector3 padding = player.eyes.MovementForward() * 0.2f;
            Vector3 startPos = (padding+player.eyes.transform.position) + Vector3.up * 1.5f;
            Vector3 endPos = (startPos + player.eyes.MovementForward() * (config.escortDist * 0.9f));

            bool los = GamePhysics.CheckCapsule(startPos, endPos, 0.4f, mask);

            // player.SendConsoleCommand("ddraw.arrow", 0.3f, los?Color.red:Color.cyan, startPos , endPos, 0.5f);

            return los;
        }

        private bool IsRestrained(BasePlayer target){
            if(storedData.restrained.ContainsKey(target.UserIDString))return true;
            return false;
        }

        private bool Restrain(BasePlayer target, BasePlayer player = null){
            if(!storedData.restrained.ContainsKey(target.UserIDString)){
                if(Interface.CallHook("CanCuffsPlayerRestrain", target, player) != null)return false;

                storageChanged = true;
            }

            listenToUsers.Remove(target.UserIDString);
            target.EnsureDismounted();

            target.GetActiveItem()?.Drop(target.eyes.position,Vector3.zero);
            // target.SendNetworkUpdate();

            if(userUIContainers.ContainsKey(target.UserIDString)){
                userUIContainers[target.UserIDString].Progress.Destroy();
                userUIContainers[target.UserIDString].Menu.Destroy();
            }

            if(escortingUsers.ContainsKey(target)){
                StopEscorting(target,Vector3.zero);
            }

            DisableUserInput(target);

            if(player != null)storedData.restrained.Add(target.UserIDString,player.UserIDString);

            if(!target.IsSleeping())PlayNetworkAnimation(target,$"cinematic_play idle_stand_handcuff {target.UserIDString}");

            Interface.CallHook("OnCuffsPlayerRestrained", target, player);

            return true;
        }

        private bool Unrestrain(BasePlayer target){
            if(storedData.restrained.ContainsKey(target.UserIDString)){
                storageChanged = true;
            }

            BasePlayer player = escortingUsers.FirstOrDefault(x => x.Value == target).Key;

            if(Interface.CallHook("CanCuffsPlayerUnrestrain", target) != null)return false;
            
            if(player != null){
                StopEscorting(player,Vector3.zero);
            }

            EnableUserInput(target);

            if(!target.IsSleeping())PlayNetworkAnimation(target,$"cinematic_stop {target.UserIDString}");
            storedData.restrained.Remove(target.UserIDString);

            Interface.CallHook("OnCuffsPlayerUnrestrained", target);

            return true;
        }

        private bool PlayerSelect(BasePlayer player, BasePlayer target, bool limitDist = true){
            if(selectedUsers.ContainsKey(player) || selectedUsers.ContainsValue(target)){
                return false;
            }

            object obj = Interface.CallHook("CanCuffsPlayerSelect", target, player);
            if (obj is bool){
                return (bool)obj;
            }

            UIContainer UICont = userUIContainers[player.UserIDString];

            if(!escortingUsers.ContainsKey(player) && limitDist)userTimers[$"{player.UserIDString}_player_selected"] = timer.Every(0.5f,() => {
                float dist = Vector3.Distance(player.transform.position,target.transform.position);
                if(dist > config.restrainDist){
                    PlayerDeselect(player);
                    return;
                }
            });

            selectedUsers[player] = target;
            UICont.Menu.Draw();
            PlayEffect(player,"assets/prefabs/tools/detonator/effects/attack.prefab");

            Interface.CallHook("OnCuffsPlayerSelected", target, player);

            return true;
        }

        private bool PlayerDeselect(BasePlayer player){
            if(selectedUsers.ContainsValue(player)){
                return false;
            }

            UIContainer UICont = userUIContainers[player.UserIDString];

            if(userTimers.ContainsKey($"{player.UserIDString}_player_selected"))userTimers[$"{player.UserIDString}_player_selected"]?.Destroy();

            selectedUsers.Remove(player);
            // UIBtnCallback(player.IPlayer,"",new[]{"close"});

            UICont.Menu.Destroy();
            PlayEffect(player,"assets/prefabs/tools/detonator/effects/attack.prefab");

            return true;
        }

        private bool StartInspecting(BasePlayer target, BasePlayer player){
            object obj = Interface.CallHook("CanCuffsPlayerViewInventory", target, player);
            if (obj is bool){
                return (bool)obj;
            }

            RestrainInspector inspector = player.gameObject.GetComponent<RestrainInspector>();
            inspector?.Remove();

            inspector = player.gameObject.AddComponent<RestrainInspector>();
            inspector.Instantiate(player, target);

            Interface.CallHook("OnCuffsPlayerViewInventory", target, player);

            return true;
        }

        private bool StartEscorting(BasePlayer target, BasePlayer player){
            if(escortingUsers.ContainsKey(player))return false;
            object obj = Interface.CallHook("CanCuffsPlayerEscort",target, player);
            if (obj is bool){
                return (bool)obj;
            }

            EnableUserInput(target);

            target.inventory.containerBelt.capacity = 0;
            target.SendNetworkUpdate();

            player.EnsureDismounted();

            ChairHack chairHack = new ChairHack();
            BaseVehicle chair = chairHack.chair;

            chair.SetParent(player,"collision",false,false);

            chair.transform.position = player.transform.position;
            chair.transform.localPosition += (chairPositionOffset + new Vector3(0,0,config.escortDist));

            chair.SendNetworkUpdateImmediate();

            restrainChairs[target.UserIDString] = chairHack;

            chairHack.mount.MountPlayer(target);

            escortingUsers[player] = target;

            AntiHack antihack = target.gameObject.AddComponent<AntiHack>();
            antihack.Instantiate(player,target);

            Interface.CallHook("OnCuffsPlayerEscort", target, player);

            return true;
        }

        private bool StopEscorting(BasePlayer player, Vector3 pos, bool soft = false){
            if(!escortingUsers.ContainsKey(player))return false;
            BasePlayer target = escortingUsers[player];

            object obj = Interface.CallHook("CanCuffsPlayerEscortStop",target, player);
            if (obj is bool){
                return (bool)obj;
            }

            if(pos == Vector3.zero)pos = player.transform.position + player.eyes.MovementForward() * config.escortDist;
            ChairHack chairHack = restrainChairs[target.UserIDString];

            target.inventory.containerBelt.capacity = 6;
            target.SendNetworkUpdate();

            DestroyChair(chairHack);

            if(soft){
                UnityEngine.Object.Destroy(target.GetComponent<AntiHack>());
                restrainChairs.Remove(target.UserIDString);

                escortingUsers.Remove(player);
                return true;
            }

            ForceLocation forceLocation = target.gameObject.AddComponent<ForceLocation>();
            forceLocation.pos = pos;

            UnityEngine.Object.Destroy(target.GetComponent<AntiHack>());
            restrainChairs.Remove(target.UserIDString);

            timer.In(.3f, () => {
                forceLocation.Remove();

                if(storedData.restrained.ContainsKey(target.UserIDString)){
                    DisableUserInput(target);
                }

                target.OverrideViewAngles(player.viewAngles);
            });

            escortingUsers.Remove(player);

            Interface.CallHook("OnCuffsPlayerEscortStop", target, player);

            return true;
        }

        private void DestroyChair(ChairHack chairHack){
            BaseVehicle chair = chairHack.chair;
            BasePlayer player = chair.GetParentEntity() as BasePlayer;

            Vector3 pos = player.transform.position + player.eyes.MovementForward() * 2;

            chair.transform.position = pos;
            chair.SendNetworkUpdate(BasePlayer.NetworkQueue.UpdateDistance);

            chairHack.Destroy();
        }

        private Item CreateCuffs(int amount = 1){
            Item item = ItemManager.Create(ItemManager.FindItemDefinition(cuffsItemShortname),amount,cuffsSkinID);
            item.name = "Rusty Cuffs";

            return item;
        }

        private Item CreateCuffsKey(string name, int amount = 1){
            Item item = ItemManager.Create(ItemManager.FindItemDefinition(keysItemShortname),amount,0);
            item.name = $"Handcuff Key - {name}";

            return item;
        }

        private void DropItem(Item item, Vector3 pos){
            item.Drop(pos,Vector3.zero);
        }

        private void GiveItem(BasePlayer player, Item item){
            if(!player.inventory.GiveItem(item, player.inventory.containerBelt) && !player.inventory.GiveItem(item, player.inventory.containerMain)){
                DropItem(item,player.eyes.position);
            }
        }

        // testing asset unused
        private BaseEntity CreateBot(Vector3 pos){
            var player = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab",pos).ToPlayer();
            
            player.Spawn();

            RelationshipManager.Instance.playerToTeam.Remove(player.userID);
            player.ClearTeam();     
            RelationshipManager.PlayerTeam team = RelationshipManager.Instance.CreateTeam();
            RelationshipManager.PlayerTeam playerTeam = team;
            playerTeam.teamLeader = player.userID;

            if (!playerTeam.AddPlayer(player))
            {
                player.currentTeam = playerTeam.teamID;
                playerTeam.members.Add(player.userID);
                player.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            }


            return player;
        }

        private void BroadcastPlayer(BasePlayer player, string msg, bool prefix = true) => Player.Message(player,$"{(prefix?config.prefix:"")}{msg}",config.icon);
        // private void BroadcastPlayer(IPlayer player, string msg, bool prefix = true) => BroadcastPlayer((BasePlayer)player.Object,msg,prefix);

        // umod compliance
        private void SendMessage(BasePlayer player, string[] args, bool prefix = true) => BroadcastPlayer(player,string.Format(lang.GetMessage(args[0],this),args.Skip(1).ToArray()),prefix);
        // private void SendMessage(BasePlayer player, string[] args, bool prefix = true) => BroadcastPlayer(player,string.Format(lang.GetMessage(messages[args[0]],this),args.Skip(1).ToArray()),prefix);
        
        private void SendMessage(IPlayer player, string[] args, bool prefix = true) => SendMessage((BasePlayer)player.Object,args,prefix);


        private void StopProgress(BasePlayer player, UIContainer cont){
            // Puts($"Progress Cleared");

            if(userTimers.ContainsKey(player.UserIDString)){
                userTimers[player.UserIDString].Destroy();
            }
            userTimers.Remove(player.UserIDString);

            cont.Progress.Destroy();
        }

        private void DisableUserInput(BasePlayer player){
            if(usersInputDisabled.Contains(player))return;
            player.SetParent(null,true,true);

            usersInputDisabled.Add(player);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, true);
        }

        private void EnableUserInput(BasePlayer player){
            if(!usersInputDisabled.Contains(player))return;
            player.SetParent(null,true,true);

            usersInputDisabled.Remove(player);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, false);
        }

        private void PlayNetworkAnimation(BasePlayer bpr, string command){
            // Puts($"Found: {bpr.UserIDString} - {command}");

            if(bpr.IsWounded() || bpr.IsSleeping())return;

            Network.Visibility.Group nGroup = Net.sv.visibility.GetGroup(bpr.transform.position);

            foreach(IPlayer p2 in players.Connected){
                BasePlayer bp2 = p2.Object as BasePlayer;
                if(!Net.sv.visibility.IsInside(nGroup,bp2.transform.position))continue;
                
                // Puts($"{(p2.Object as BasePlayer).displayName} can see {bpr.displayName} over net");

                Network.Net.sv.write.Start();
                Network.Net.sv.write.PacketID(Message.Type.ConsoleCommand);
                Network.Net.sv.write.String(ConsoleSystem.BuildCommand(command));
                Network.Net.sv.write.Send(new SendInfo(bp2.net.connection));
            }
        }

        private void PlayNetworkAnimation(string id, string command){
            BasePlayer bpr = null;
            foreach(BasePlayer p1 in BasePlayer.allPlayerList){
                if(p1.UserIDString == id){
                    bpr = p1;
                    break;
                }
            }
            if(bpr == null)return;

            PlayNetworkAnimation(bpr,command);
        }

        public void PlayEffect(BasePlayer player, string prefab, bool global = false){
            var effect = new Effect();
            effect.Init(Effect.Type.Generic, player.transform.position, Vector3.zero);
            effect.pooledString = prefab;

            if(global){
                EffectNetwork.Send(effect);
            }
            else
            {
                EffectNetwork.Send(effect, player.net.connection);
            }
        }
        public void PlayEffect(IPlayer player, string prefab, bool global = false) => PlayEffect((player.Object as BasePlayer),prefab,global);

        private float mdist = 9999f;
        private int generalColl = LayerMask.GetMask("Player (Server)","Construction", "Deployable", "Default", "Prevent Building", "Deployed", "Resource", "Terrain", "World","Tree");

        private bool TryGetPlayerView(BasePlayer player, out Quaternion viewAngle) {
            viewAngle = Quaternion.identity;

            if (player.serverInput.current == null)
                return false;

            viewAngle = Quaternion.Euler(player.serverInput.current.aimAngles);

            return true;
        }

        private bool TryGetClosestRayPoint(Vector3 sourcePos, Quaternion sourceDir, out object closestEnt, out Vector3 closestHitpoint){
            float closestdist = 999999f;

            Vector3 sourceEye = sourcePos + new Vector3(0f, 1.5f, 0f);
            Ray ray = new Ray(sourceEye, sourceDir * Vector3.forward);
            
            closestHitpoint = sourcePos;
            closestEnt = false;

            foreach (var hit in Physics.RaycastAll(ray, mdist, generalColl)){
                if (hit.collider.GetComponentInParent<TriggerBase>() == null){
                    if (hit.distance < closestdist){
                        closestdist = hit.distance;
                        closestEnt = hit.GetCollider();
                        closestHitpoint = hit.point;
                    }
                }
            }

            if (closestEnt is bool)return false;
            return true;
        }

        // player finder
        public List<BasePlayer> FindPlayers(string nameOrIdOrIp){
            if (string.IsNullOrEmpty(nameOrIdOrIp)) return new List<BasePlayer>();
            return BasePlayer.allPlayerList.Where(p => p && (p.UserIDString == nameOrIdOrIp || p.displayName.Contains(nameOrIdOrIp, CompareOptions.OrdinalIgnoreCase) || (p.IsConnected && p.net.connection.ipaddress.Contains(nameOrIdOrIp)))).ToList();
        }

        public List<BasePlayer> FindPlayersOnline(string nameOrIdOrIp){
            if (string.IsNullOrEmpty(nameOrIdOrIp)) return new List<BasePlayer>();
            return BasePlayer.activePlayerList.Where(p => p && (p.UserIDString == nameOrIdOrIp || p.displayName.Contains(nameOrIdOrIp, CompareOptions.OrdinalIgnoreCase) || (p.IsConnected && p.net.connection.ipaddress.Contains(nameOrIdOrIp)))).ToList();
        }

        private Dictionary<string, List<BasePlayer>> findPlayerMatches = new Dictionary<string, List<BasePlayer>>();
        private Dictionary<string, string[]> findPlayerArgs = new Dictionary<string, string[]>();

        public bool FindPlayer(string name, object player, string command, ref string[] args, ref BasePlayer target){
            BasePlayer bplayer = (player is IPlayer)?(((IPlayer)player).Object as BasePlayer):player as BasePlayer;
            IPlayer iplayer = (player is IPlayer)?(player as IPlayer):(bplayer.IPlayer != null?bplayer.IPlayer:covalence.Players.FindPlayer(bplayer.UserIDString));
            string key = bplayer.UserIDString;

            if(args[0] == "list"){
                if (args.Length == 1){
                    if (!findPlayerMatches.ContainsKey(key) || findPlayerMatches[key] == null){
                        SendMessage(bplayer,new[]{"error_no_list_available",command});
                        return false;
                    }

                    FindPlayerShowMatches(bplayer);
                    return false;
                }

                int num;
                if (int.TryParse(args[1], out num)){
                    if (!findPlayerMatches.ContainsKey(key) || findPlayerMatches[key] == null){
                        SendMessage(bplayer,new[]{"error_no_list_available",command});
                        return false;
                    }

                    if (num > findPlayerMatches[key].Count){
                        SendMessage(bplayer,new[]{"error_invalid_selection",command});

                        FindPlayerShowMatches(bplayer);
                        return false;
                    }

                    args = findPlayerArgs[key];
                    target = findPlayerMatches[key][num - 1];

                    findPlayerArgs.Remove(key);
                    findPlayerMatches.Remove(key);
                    return true;
                }

                // BroadcastPlayer(bplayer,"InvalidArguments");
                return false;
            }
            else
            {   
                if(name == null || name == "")return false;
                List<BasePlayer> players = (List<BasePlayer>)FindPlayers(name);

                switch (players.Count){
                    case 0:
                        SendMessage(bplayer,new[]{"error_no_players_found",name});
                    break;

                    case 1:
                        target = players[0];
                        return true;
                    break;

                    default:
                        SendMessage(bplayer,new[]{"error_multiple_players_found",command,name});

                        if (!findPlayerMatches.ContainsKey(key)){
                            findPlayerMatches.Add(key, players);
                            findPlayerArgs.Add(key, args);
                        }
                        else
                        {
                            findPlayerMatches[key] = players;
                            findPlayerArgs[key] = args;
                        }

                        FindPlayerShowMatches(bplayer);
                    break;
                }

                return false;
            }
        }

        private void FindPlayerShowMatches(BasePlayer player){
            string key = player.UserIDString;

            for (int i = 0; i < findPlayerMatches[key].Count; i++){
                BroadcastPlayer(player,$"{i + 1}. {findPlayerMatches[key][i].displayName}",false);

                if (i == 4 && i < findPlayerMatches[key].Count){
                    SendMessage(player,new[]{"error_too_many_players_found"});
                    break;
                }
            }
        }

        private BasePlayer RayToPlayer(BasePlayer bplayer){
            object closestEnt = null;
            Vector3 closestHitpoint = new Vector3();
            Quaternion currentRot = new Quaternion();
            Quaternion currentRotate = Quaternion.Euler(0f, 45f, 0f);
            Collider currentCollider = null;
            BaseNetworkable currentBaseNet = null;
            Vector3 myPos = bplayer.transform.position;
            BasePlayer target = null;

            if(!TryGetPlayerView(bplayer, out currentRot) || !TryGetClosestRayPoint(myPos, currentRot, out closestEnt, out closestHitpoint)){
                return null;
            }

            currentCollider = closestEnt as Collider;
            if(!(currentCollider is UnityEngine.CapsuleCollider)){
                // Puts($"That isn't a person..");
                return null;
            }

            target = currentCollider.GetComponentInParent<BaseNetworkable>() as BasePlayer;
            if(target == null){
                // Puts($"Bad hit");
                return null;
            }

            return target;
        }

        #endregion

        #region components

        class ChairHack {
            // assets/bundled/prefabs/static/chair.invisible.static.prefab
            // assets/prefabs/deployable/chair/chair.deployed.prefab
            // assets/prefabs/deployable/sofa/sofa.deployed.prefab

            public BaseVehicle chair = null;
            public BaseMountable mount = null;

            public ChairHack(){
                chair = GameManager.server.CreateEntity("assets/prefabs/deployable/sofa/sofa.deployed.prefab") as BaseVehicle;

                UnityEngine.Object.Destroy(chair.GetComponent<DestroyOnGroundMissing>());
                UnityEngine.Object.Destroy(chair.GetComponent<GroundWatch>());

                chair.isMobile = true;
                chair.enableSaving = false;
                // chair.OwnerID = player.userID;

                // vehicle = chair.GetComponent<BaseVehicle>();

                chair.mountPoints.Add(new BaseVehicle.MountPointInfo {
                    pos = -_ins.chairPositionOffset,
                    rot = chair.mountPoints[0].rot,
                    prefab = chair.mountPoints[0].prefab,
                    mountable = chair.mountPoints[0].mountable,
                });

                BaseVehicle.MountPointInfo point = chair.mountPoints[2];

                chair.Spawn();

                mount = point.mountable.GetComponent<BaseMountable>();
                _ins.chairEnts.Add(mount);
                mount.isMobile = true;
            }

            public void Destroy(){
                chair?.Kill();
                _ins.chairEnts.Remove(mount);
            }
        }

        private class ForceLocation : MonoBehaviour {
            internal Vector3 pos = Vector3.zero;
            public BasePlayer target;

            private void Awake() {
                target = GetComponent<BasePlayer>();
            }

            public void Remove() {
                Destroy(this);
            }

            private void FixedUpdate() {
                if (target == null || pos == Vector3.zero)return;
                if (target != null && !target.IsDestroyed){
                    // target.transform.position = pos;
                    target.MovePosition(pos);
                    target.SendNetworkUpdate(BasePlayer.NetworkQueue.UpdateDistance);

                }
            }
        }

        private class AntiHack : MonoBehaviour {
            public BasePlayer player = null;
            public BasePlayer target = null;

            public void Instantiate(BasePlayer player, BasePlayer target){
                this.player = player;
                this.target = target;
            }

            public void Remove() {
                Destroy(this);
            }

            private void FixedUpdate() {
                if (target == null)return;
                if(_ins.IsViewObstructed(player,_ins.baseMask)){
                    _ins.StopEscorting(player,player.transform.position);
                }
            }
        }

        private class RestrainInspector : MonoBehaviour {
            private BasePlayer player;
            private BasePlayer target;
            private int ticks;

            public void Instantiate(BasePlayer player, BasePlayer target){
                this.player = player;
                this.target = target;

                BeginLooting();

                InvokeRepeating("UpdateLoot", 0f, 0.1f);
            }

            private void UpdateLoot(){
                if (!target)
                {
                    return;
                }

                if (!target.inventory)
                {
                    return;
                }

                ticks++;

                if (!player.inventory.loot.IsLooting())
                {
                    BeginLooting();
                }

                player.inventory.loot.SendImmediate();

                player.SendNetworkUpdateImmediate();
            }

            private void StopInspecting(bool forced = false){
                if (ticks < 5 && !forced)
                {
                    return;
                }

                CancelInvoke("UpdateLoot");

                EndLooting();
            }

            private void BeginLooting(){
                player.inventory.loot.Clear();

                if (!target)
                {
                    return;
                }

                if (!target.inventory)
                {
                    return;
                }

                player.inventory.loot.AddContainer(target.inventory.containerMain);
                player.inventory.loot.AddContainer(target.inventory.containerWear);
                player.inventory.loot.AddContainer(target.inventory.containerBelt);
                player.inventory.loot.PositionChecks = false;
                player.inventory.loot.entitySource = target;
                player.inventory.loot.itemSource = null;
                player.inventory.loot.MarkDirty();
                player.inventory.loot.SendImmediate();
                player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", "player_corpse");
                player.SendNetworkUpdateImmediate();
            }

            private void EndLooting(){
                player.inventory.loot.MarkDirty();

                if (player.inventory.loot.entitySource)
                {
                    player.inventory.loot.entitySource.SendMessage("PlayerStoppedLooting", player, SendMessageOptions.DontRequireReceiver);
                }

                foreach (ItemContainer container in player.inventory.loot.containers)
                {
                    if (container != null)
                    {
                        container.onDirty -= player.inventory.loot.MarkDirty;
                    }
                }

                player.inventory.loot.containers.Clear();
                player.inventory.loot.entitySource = null;
                player.inventory.loot.itemSource = null;
            }

            public void Remove(bool forced = false){
                if (ticks < 5 && !forced){
                    return;
                }

                StopInspecting(forced);

                Destroy(this);
            }
        }

        #endregion

        #region DEBUG

        public void jPrint(object type) {
            string jsonString;
            jsonString = JsonConvert.SerializeObject(type);

            Puts(jsonString);
        }

        #endregion
    }
}