using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info( "Kits", "Reneb", "3.3.0" )]
    class Kits : RustPlugin
    {
        private readonly int _playerLayer = LayerMask.GetMask( "Player (Server)" );
        private DateTime _lastWipe;

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// Plugin initialization
        //////////////////////////////////////////////////////////////////////////////////////////
        [PluginReference] private Plugin CopyPaste, ImageLibrary, EventManager;

        private void Loaded()
        {
            LoadData();
            try
            {
                kitsData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<string, KitData>>>( "Kits_Data" );
            }
            catch
            {
                kitsData = new Dictionary<ulong, Dictionary<string, KitData>>();
            }

            lang.RegisterMessages( _messages, this );
        }

        private void OnServerInitialized()
        {
            _lastWipe = SaveRestore.SaveCreatedTime.ToLocalTime();
            InitializePermissions();
            if ( !string.IsNullOrEmpty( BackgroundURL ) )
            {
                if ( ImageLibrary )
                {
                    AddImage( BackgroundURL, "Background", ( ulong ) ResourceId );
                }
            }
        }

        private void InitializePermissions()
        {
            permission.RegisterPermission( this.Title + ".admin", this );
            permission.RegisterPermission( this.Title + ".ConsoleGive", this );
            foreach ( var kit in storedData.Kits.Values )
            {
                if ( !string.IsNullOrEmpty( kit.permission ) && !permission.PermissionExists( kit.permission ) )
                {
                    permission.RegisterPermission( kit.permission, this );
                }

                if ( ImageLibrary )
                {
                    AddImage( kit.image ?? "http://i.imgur.com/xxQnE1R.png", kit.name.Replace( " ", "" ), ( ulong ) ResourceId );
                }
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// Configuration
        //////////////////////////////////////////////////////////////////////////////////////////

        private Dictionary<ulong, GUIKit> GUIKits;
        private List<string> CopyPasteParameters = new List<string>();
        private string BackgroundURL;
        private bool KitLogging;
        private bool ShowUnavailableKits;
        public Dictionary<int, string> AutoKits = new Dictionary<int, string>();
        public bool WipeKitsDataNewMap;
        private Dictionary<string, float> _wipeKitCooldown = new Dictionary<string, float>();

        private class GUIKit
        {
            public string description = string.Empty;
            public List<string> kits = new List<string>();
        }

        protected override void LoadDefaultConfig()
        { }

        private void Init()
        {
            var config = Config.ReadObject<Dictionary<string, object>>();
            if ( !config.ContainsKey( "NPC - GUI Kits" ) )
            {
                config["NPC - GUI Kits"] = GetExampleGUIKits();
                Config.WriteObject( config );
            }

            if ( !config.ContainsKey( "CopyPaste - Parameters" ) )
            {
                config["CopyPaste - Parameters"] = new List<string> { "deployables", "true", "inventories", "true" };
                Config.WriteObject( config );
            }

            if ( !config.ContainsKey( "Custom AutoKits" ) )
            {
                config["Custom AutoKits"] = new Dictionary<int, string> { { 0, "KitName" }, { 1, "KitName" }, { 2, "KitName" } };
                Config.WriteObject( config );
            }

            if ( !config.ContainsKey( "Kit - Logging" ) )
            {
                config["Kit - Logging"] = false;
                Config.WriteObject( config );
            }

            if ( !config.ContainsKey( "Show All Kits" ) )
            {
                config["Show All Kits"] = false;
                Config.WriteObject( config );
            }

            if ( !config.ContainsKey( "Background - URL" ) )
            {
                config["Background - URL"] = string.Empty;
                Config.WriteObject( config );
            }
            
            if ( !config.ContainsKey( "Wipe kit data on map wipe" ) )
            {
                config["Wipe kit data on map wipe"] = false;
                Config.WriteObject( config );
            }
            
            if ( !config.ContainsKey( "Kit Names & Cooldowns - Cooldowns (minutes)" ) )
            {
                config["Kit Names & Cooldowns - Cooldowns (minutes)"] = new Dictionary<string, float>
                {
                    {"kitName1", 20},
                    {"kitName2", 30}
                };
                Config.WriteObject( config );
            }

            var keys = config.Keys.ToArray();
            if ( keys.Length > 1 )
            {
                foreach ( var key in keys )
                {
                    if ( !key.Equals( "NPC - GUI Kits" ) && !key.Equals( "CopyPaste - Parameters" ) && !key.Equals( "Custom AutoKits" ) && !key.Equals( "Background - URL" ) && !key.Equals( "Kit - Logging" ) && !key.Equals( "Show All Kits" ) && !key.Equals( "Wipe kit data on map wipe" ) && !key.Equals( "Kit Names & Cooldowns - Cooldowns (minutes)" ) )
                    {
                        config.Remove( key );
                    }
                }

                Config.WriteObject( config );
            }

            CopyPasteParameters = JsonConvert.DeserializeObject<List<string>>( JsonConvert.SerializeObject( config["CopyPaste - Parameters"] ) );
            GUIKits = JsonConvert.DeserializeObject<Dictionary<ulong, GUIKit>>( JsonConvert.SerializeObject( config["NPC - GUI Kits"] ) );
            AutoKits = JsonConvert.DeserializeObject<Dictionary<int, string>>( JsonConvert.SerializeObject( config["Custom AutoKits"] ) );

            if ( AutoKits.Keys.Count > 0 )
            {
                AutoKits = AutoKits.OrderBy( k => k.Key ).ToDictionary( x => x.Key, x => x.Value );   
            }

            BackgroundURL = JsonConvert.DeserializeObject<string>( JsonConvert.SerializeObject( config["Background - URL"] ) );
            KitLogging = JsonConvert.DeserializeObject<bool>( JsonConvert.SerializeObject( config["Kit - Logging"] ) );
            ShowUnavailableKits = JsonConvert.DeserializeObject<bool>( JsonConvert.SerializeObject( config["Show All Kits"] ) );
            WipeKitsDataNewMap = JsonConvert.DeserializeObject<bool>( JsonConvert.SerializeObject( config["Wipe kit data on map wipe"] ) );
            _wipeKitCooldown = JsonConvert.DeserializeObject<Dictionary<string, float>>( JsonConvert.SerializeObject( config["Kit Names & Cooldowns - Cooldowns (minutes)"] ) );
            
        }
        
        private void OnNewSave(string filename)
        {
            if ( WipeKitsDataNewMap )
            {
                ResetData();
                SaveKitsData();
                PrintWarning( "Wiped kits data" );
            }
        }

        private static Dictionary<ulong, GUIKit> GetExampleGUIKits()
        {
            return new Dictionary<ulong, GUIKit>
            {
                {
                    0, new GUIKit
                    {
                        kits = { "kit1", "kit2" },
                        description = "Welcome on this server! Here is a list of free kits that you can get.<color=green>Enjoy your stay</color>"
                    }
                },
                {
                    1235439, new GUIKit
                    {
                        kits = { "kit1", "kit2" },
                        description = "Welcome on this server! Here is a list of free kits that you can get.<color=green>Enjoy your stay</color>"
                    }
                },
                {
                    8753201223, new GUIKit
                    {
                        kits = { "kit1", "kit3" },
                        description = "<color=red>VIPs Kits</color>"
                    }
                }
            };
        }

        private void OnPlayerRespawned( BasePlayer player )
        {
            var thereturn = Interface.Oxide.CallHook( "canRedeemKit", player );
            if ( thereturn == null )
            {
                if ( storedData.Kits.ContainsKey( "autokit" ) )
                {
                    player.inventory.Strip();
                    GiveKit( player, "autokit" );
                    return;
                }

                foreach ( var entry in AutoKits )
                {
                    var success = CanRedeemKit( player, entry.Value, true ) as string;
                    if ( success != null )
                    {
                        continue;
                    }

                    player.inventory.Strip();
                    success = GiveKit( player, entry.Value ) as string;
                    if ( success != null )
                    {
                        continue;
                    }

                    ProccessKitGiven( player, entry.Value );
                    return;
                }
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// Language
        //////////////////////////////////////////////////////////////////////////////////////////

        private string GetMsg( string key, string steamid = null )
        {
            return lang.GetMessage( key, this, steamid );
        }

        private Dictionary<string, string> _messages = new Dictionary<string, string>()
        {
            { "title", "Kits: " },
            { "Name", "Name" },
            { "Description", "Description" },
            { "Redeem", "Redeem" },
            { "AddKit", "Add Kit" },
            { "Close", "Close" },
            { "NoKitsFound", "All Available Kits are already in this Menu!" },
            { "AddKitToMenu", "Select a Kit to Add to this Menu" },
            { "KitCooldown", "Cooldown: {0}" },
            { "Unavailable", "Unavailable" },
            { "Last", "Last" },
            { "Next", "Next" },
            { "Back", "Back" },
            { "First", "First" },
            { "RemoveKit", "Remove Kit?" },
            { "KitUses", "Uses: {0}" },
            { "NoInventorySpace", "You do not have enough inventory space for this Kit!" },
            { "Unlimited", "Unlimited" },
            { "None", "None" },
            { "KitRedeemed", "Kit Redeemed" },
            { "Emptykitname", "Empty Kit Name" },
            { "KitExistError", "This kit doesn't exist" },
            { "CantRedeemNow", "You are not allowed to redeem a kit at the moment" },
            { "NoAuthToRedeem", "You don't have the Auth Level to use this kit" },
            { "NoPermKit", "You don't have the permissions to use this kit" },
            { "NoRemainingUses", "You already redeemed all of these kits" },
            { "CooldownMessage", "You need to wait {0} seconds to use this kit" },
            { "WipeCooldownMessage", "You need to wait {0} seconds to use this kit since it recently wiped" },
            { "NPCError", "You must find the NPC that gives this kit to redeem it." },
            { "PastingError", "Something went wrong while pasting, is CopyPaste installed?" },
            { "NoKitFound", "This kit doesn't exist" },
        };

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// Kit Creator
        //////////////////////////////////////////////////////////////////////////////////////////

        private static List<KitItem> GetPlayerItems( BasePlayer player )
        {
            var kititems = new List<KitItem>();
            foreach ( var item in player.inventory.containerWear.itemList )
            {
                if ( item != null )
                {
                    var iteminfo = ProcessItem( item, "wear" );
                    kititems.Add( iteminfo );
                }
            }

            foreach ( var item in player.inventory.containerMain.itemList )
            {
                if ( item != null )
                {
                    var iteminfo = ProcessItem( item, "main" );
                    kititems.Add( iteminfo );
                }
            }

            foreach ( var item in player.inventory.containerBelt.itemList )
            {
                if ( item != null )
                {
                    var iteminfo = ProcessItem( item, "belt" );
                    kititems.Add( iteminfo );
                }
            }

            return kititems;
        }

        private static KitItem ProcessItem( Item item, string container )
        {
            var iItem = new KitItem
            {
                amount = item.amount,
                mods = new List<int>(),
                container = container,
                skinid = item.skin,
                itemid = item.info.itemid,
                weapon = false,
                blueprintTarget = item.blueprintTarget
            };

            if ( item.info.category == ItemCategory.Weapon )
            {
                var weapon = item.GetHeldEntity() as BaseProjectile;
                if ( weapon != null )
                {
                    if ( weapon.primaryMagazine != null )
                    {
                        iItem.weapon = true;
                        if ( item.contents != null )
                        {
                            foreach ( var mod in item.contents.itemList )
                            {
                                if ( mod.info.itemid != 0 )
                                {
                                    iItem.mods.Add( mod.info.itemid );
                                }
                            }
                        }
                    }
                }
            }

            return iItem;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// Kit Redeemer
        //////////////////////////////////////////////////////////////////////////////////////////

        private void TryGiveKit( BasePlayer player, string kitname )
        {
            var success = CanRedeemKit( player, kitname ) as string;
            if ( success != null )
            {
                OnScreen( player, success );
                return;
            }

            success = GiveKit( player, kitname ) as string;
            if ( success != null )
            {
                OnScreen( player, success );
                return;
            }

            OnScreen( player, GetMsg( "KitRedeemed", player.UserIDString ) );
            Interface.CallHook( "OnKitRedeemed", player, kitname );
            ProccessKitGiven( player, kitname );
        }

        private void ProccessKitGiven( BasePlayer player, string kitname )
        {
            if ( string.IsNullOrEmpty( kitname ) )
            {
                return;
            }

            kitname = kitname.ToLower();
            Kit kit;
            if ( !storedData.Kits.TryGetValue( kitname, out kit ) )
            {
                return;
            }

            var kitData = GetKitData( player.userID, kitname );
            if ( kit.max > 0 )
            {
                kitData.max += 1;
            }

            if ( kit.cooldown > 0 )
            {
                kitData.cooldown = CurrentTime() + kit.cooldown;
            }

            if ( PlayerGUI.ContainsKey( player.userID ) && PlayerGUI[player.userID].open )
            {
                RefreshKitPanel( player, PlayerGUI[player.userID].guiid, PlayerGUI[player.userID].page );
            }
        }

        private object GiveKit( BasePlayer player, string kitname )
        {
            if ( string.IsNullOrEmpty( kitname ) )
            {
                return GetMsg( "Emptykitname", player.UserIDString );
            }

            kitname = kitname.ToLower();
            Kit kit;
            if ( !storedData.Kits.TryGetValue( kitname, out kit ) )
            {
                return GetMsg( "NoKitFound", player.UserIDString );
            }

            foreach ( var kitem in kit.items )
            {
                GiveItem( player.inventory,
                    kitem.weapon
                        ? BuildWeapon( kitem.itemid, kitem.skinid, kitem.mods )
                        : BuildItem( kitem.itemid, kitem.amount, kitem.skinid, kitem.blueprintTarget ),
                    kitem.container == "belt"
                        ? player.inventory.containerBelt
                        : kitem.container == "wear"
                            ? player.inventory.containerWear
                            : player.inventory.containerMain );
            }

            if ( !string.IsNullOrEmpty( kit.building ) )
            {
                var success = CopyPaste?.CallHook( "TryPasteFromSteamID", player.userID, kit.building, CopyPasteParameters.ToArray() );
                return success;
            }

            if ( KitLogging )
            {
                LogToFile( "received", $"{player.displayName}<{player.UserIDString}> - Received Kit: {kitname}", this );
            }

            return true;
        }

        private bool GiveItem( PlayerInventory inv, Item item, ItemContainer container = null )
        {
            if ( item == null )
            {
                return false;
            }

            var position = -1;
            return ( ( ( container != null ) && item.MoveToContainer( container, position, true ) ) || ( item.MoveToContainer( inv.containerMain, -1, true ) || item.MoveToContainer( inv.containerBelt, -1, true ) ) );
        }

        private Item BuildItem( int itemid, int amount, ulong skin, int blueprintTarget )
        {
            if ( amount < 1 )
            {
                amount = 1;
            }

            var item = ItemManager.CreateByItemID( itemid, amount, skin );
            if ( blueprintTarget != 0 )
            {
                item.blueprintTarget = blueprintTarget;
            }

            return item;
        }

        private Item BuildWeapon( int id, ulong skin, List<int> mods )
        {
            var item = ItemManager.CreateByItemID( id, 1, skin );
            var weapon = item.GetHeldEntity() as BaseProjectile;
            if ( weapon != null )
            {
                ( ( BaseProjectile ) item.GetHeldEntity() ).primaryMagazine.contents = ( ( BaseProjectile ) item.GetHeldEntity() ).primaryMagazine.capacity;
            }

            if ( mods != null )
            {
                foreach ( var mod in mods )
                {
                    BuildItem( mod, 1, 0, 0 ).MoveToContainer( item.contents );
                }
            }

            return item;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// Check Kits
        //////////////////////////////////////////////////////////////////////////////////////////
        
        private bool isKit( string kitname )
        {
            return IsKit(kitname);
        }
        
        private bool IsKit( string kitname )
        {
            return !string.IsNullOrEmpty( kitname ) && storedData.Kits.ContainsKey( kitname.ToLower() );
        }

        private static readonly DateTime epoch = new DateTime( 1970, 1, 1, 0, 0, 0 );

        private static double CurrentTime()
        {
            return DateTime.UtcNow.Subtract( epoch ).TotalSeconds;
        }

        private bool CanSeeKit( BasePlayer player, string kitname, bool fromNpc, out string reason )
        {
            reason = string.Empty;
            if ( string.IsNullOrEmpty( kitname ) )
            {
                return false;
            }

            kitname = kitname.ToLower();
            Kit kit;
            if ( !storedData.Kits.TryGetValue( kitname, out kit ) )
            {
                return false;
            }

            if ( kit.hide )
            {
                return false;
            }

            if ( kit.authlevel > 0 )
            {
                if ( player.net.connection.authLevel < kit.authlevel )
                {
                    return false;
                }
            }

            if ( !string.IsNullOrEmpty( kit.permission ) )
            {
                if ( player.net.connection.authLevel < 2 && !permission.UserHasPermission( player.UserIDString, kit.permission ) )
                {
                    return false;
                }
            }

            if ( kit.npconly && !fromNpc )
            {
                return false;
            }
            
            if ( kit.max > 0 )
            {
                var left = GetKitData( player.userID, kitname ).max;
                if ( left >= kit.max )
                {
                    reason = "- 0 left";
                    return false;
                }

                reason = $"- {( kit.max - left )} left";
            }

            if ( kit.cooldown > 0 )
            {
                var cd = GetKitData( player.userID, kitname ).cooldown;
                var ct = CurrentTime();
                if ( cd > ct && cd != 0.0 )
                {
                    reason += $"- {Math.Abs( Math.Ceiling( cd - ct ) )} seconds";
                    return false;
                }
            }

            return true;
        }

        private object CanRedeemKit( BasePlayer player, string kitname, bool skipAuth = false )
        {
            if ( string.IsNullOrEmpty( kitname ) )
            {
                return GetMsg( "Emptykitname", player.UserIDString );
            }

            kitname = kitname.ToLower();
            Kit kit;
            if ( !storedData.Kits.TryGetValue( kitname, out kit ) )
            {
                return GetMsg( "KitExistError", player.UserIDString );
            }

            var thereturn = Interface.Oxide.CallHook( "canRedeemKit", player );
            if ( thereturn != null )
            {
                if ( thereturn is string )
                {
                    return thereturn;
                }

                return GetMsg( "CantRedeemNow", player.UserIDString );
            }

            if ( kit.authlevel > 0 && !skipAuth )
            {
                if ( player.net.connection.authLevel < kit.authlevel )
                {
                    return GetMsg( "NoAuthToRedeem", player.UserIDString );
                }
            }

            if ( !string.IsNullOrEmpty( kit.permission ) )
            {
                if ( player.net.connection.authLevel < 2 && !permission.UserHasPermission( player.UserIDString, kit.permission ) )
                {
                    return GetMsg( "NoPermKit", player.UserIDString );
                }
            }

            var kitData = GetKitData( player.userID, kitname );
            if ( kit.max > 0 )
            {
                if ( kitData.max >= kit.max )
                {
                    return GetMsg( "NoRemainingUses", player.UserIDString );
                }
            }

            if ( kit.cooldown > 0 )
            {
                var ct = CurrentTime();
                if ( kitData.cooldown > ct && kitData.cooldown != 0.0 )
                {
                    return string.Format( GetMsg( "CooldownMessage", player.UserIDString ), Math.Abs( Math.Ceiling( kitData.cooldown - ct ) ) );
                }
            }
            
            float cooldown;
            if ( _wipeKitCooldown.TryGetValue( kit.name, out cooldown ) )
            {
                if ( _lastWipe.AddMinutes( cooldown ) > DateTime.Now )
                {
                    var cooldownSeconds = _lastWipe.AddMinutes( cooldown ) - DateTime.Now;
                    return string.Format( GetMsg( "WipeCooldownMessage", player.UserIDString ), Math.Abs( Math.Ceiling( cooldownSeconds.TotalSeconds )) );
                }
            }

            if ( kit.npconly )
            {
                var foundNPC = false;
                var neededNpc = new List<ulong>();
                foreach ( var pair in GUIKits )
                {
                    if ( pair.Value.kits.Contains( kitname ) )
                    {
                        neededNpc.Add( pair.Key );
                    }
                }

                foreach ( var col in Physics.OverlapSphere( player.transform.position, 3f, _playerLayer, QueryTriggerInteraction.Collide ) )
                {
                    var targetplayer = col.GetComponentInParent<BasePlayer>();
                    if ( targetplayer == null )
                    {
                        continue;
                    }

                    if ( neededNpc.Contains( targetplayer.userID ) )
                    {
                        foundNPC = true;
                        break;
                    }
                }

                if ( !foundNPC )
                {
                    return GetMsg( "NPCError", player.UserIDString );
                }
            }
            
            var beltcount = GetItemCountForContainer( kit.items, "belt" );
            var wearcount = GetItemCountForContainer( kit.items, "wear" );
            var maincount = GetItemCountForContainer( kit.items, "main" );
            
            var totalcount = beltcount + wearcount + maincount;
            if ( ( player.inventory.containerBelt.capacity - player.inventory.containerBelt.itemList.Count ) < beltcount || ( player.inventory.containerWear.capacity - player.inventory.containerWear.itemList.Count ) < wearcount || ( player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count ) < maincount )
            {
                if ( totalcount > ( player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count ) )
                {
                    return GetMsg( "NoInventorySpace", player.UserIDString );
                }
            }

            return true;
        }

        private int GetItemCountForContainer(List<KitItem> items, string containerName)
        {
            int amount = 0;
            
            for ( int i = 0; i < items.Count; i++ )
            {
                if ( items[i].container == containerName )
                {
                    amount++;
                }
            }

            return amount;
        }


        //////////////////////////////////////////////////////////////////////////////////////
        // Kit Class
        //////////////////////////////////////////////////////////////////////////////////////
        private class KitItem
        {
            public int itemid;
            public string container;
            public int amount;
            public ulong skinid;
            public bool weapon;
            public int blueprintTarget;
            public List<int> mods = new List<int>();
        }

        private class Kit
        {
            public string name;
            public string description;
            public int max;
            public double cooldown;
            public int authlevel;
            public bool hide;
            public bool npconly;
            public string permission;
            public string image;
            public string building;
            public List<KitItem> items = new List<KitItem>();
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // Data Manager
        //////////////////////////////////////////////////////////////////////////////////////

        private void SaveKitsData()
        {
            if ( kitsData == null )
            {
                return;
            }

            Interface.Oxide.DataFileSystem.WriteObject( "Kits_Data", kitsData );
        }


        private StoredData storedData;
        private Dictionary<ulong, Dictionary<string, KitData>> kitsData;

        private class StoredData
        {
            public Dictionary<string, Kit> Kits = new Dictionary<string, Kit>();
        }

        private class KitData
        {
            public int max;
            public double cooldown;
        }

        private void ResetData()
        {
            kitsData.Clear();
            SaveKitsData();
        }

        private void Unload()
        {
            SaveKitsData();
            foreach ( var player in BasePlayer.activePlayerList )
            {
                DestroyAllGUI( player );
            }
        }

        private void OnServerSave()
        {
            SaveKitsData();
        }

        private void SaveKits()
        {
            Interface.Oxide.DataFileSystem.WriteObject( "Kits", storedData );
        }

        private void LoadData()
        {
            var kits = Interface.Oxide.DataFileSystem.GetFile( "Kits" );
            try
            {
                kits.Settings.NullValueHandling = NullValueHandling.Ignore;
                storedData = kits.ReadObject<StoredData>();
                var update = new List<string>();
                foreach ( var kit in storedData.Kits )
                {
                    if ( !kit.Key.Equals( kit.Key.ToLower() ) )
                    {
                        update.Add( kit.Key );
                    }
                }

                foreach ( var key in update )
                {
                    storedData.Kits[key.ToLower()] = storedData.Kits[key];
                    storedData.Kits.Remove( key );
                }
            }
            catch
            {
                storedData = new StoredData();
            }

            kits.Settings.NullValueHandling = NullValueHandling.Include;
        }

        private KitData GetKitData( ulong userID, string kitname )
        {
            Dictionary<string, KitData> kitDatas;
            if ( !kitsData.TryGetValue( userID, out kitDatas ) )
            {
                kitsData[userID] = kitDatas = new Dictionary<string, KitData>();
            }

            KitData kitData;
            if ( !kitDatas.TryGetValue( kitname, out kitData ) )
            {
                kitDatas[kitname] = kitData = new KitData();
            }

            return kitData;
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // Kit Editor
        //////////////////////////////////////////////////////////////////////////////////////

        private readonly Dictionary<ulong, string> kitEditor = new Dictionary<ulong, string>();

        //////////////////////////////////////////////////////////////////////////////////////
        // ImageLibrary Hooks ---->> Absolut
        //////////////////////////////////////////////////////////////////////////////////////

        private string TryForImage( string shortname, ulong skin = 99 )
        {
            if ( shortname.Contains( "http" ) )
            {
                return shortname;
            }

            if ( skin == 99 )
            {
                skin = ( ulong ) ResourceId;
            }

            return GetImage( shortname, skin, true );
        }

        public string GetImage( string shortname, ulong skin = 0, bool returnUrl = false ) => ( string ) ImageLibrary.Call( "GetImage", shortname.ToLower(), skin, returnUrl );
        public bool HasImage( string shortname, ulong skin = 0 ) => ( bool ) ImageLibrary.Call( "HasImage", shortname.ToLower(), skin );
        public bool AddImage( string url, string shortname, ulong skin = 0 ) => ( bool ) ImageLibrary?.Call( "AddImage", url, shortname.ToLower(), skin );
        public List<ulong> GetImageList( string shortname ) => ( List<ulong> ) ImageLibrary.Call( "GetImageList", shortname.ToLower() );
        public bool isReady() => ( bool ) ImageLibrary?.Call( "IsReady" );

        //////////////////////////////////////////////////////////////////////////////////////
        // GUI CREATION ---->> Absolut
        //////////////////////////////////////////////////////////////////////////////////////

        private string PanelOnScreen = "PanelOnScreen";
        private string PanelKits = "PanelKits";
        private string PanelBackground = "PanelBackground";

        public class UI
        {
            public static CuiElementContainer CreateElementContainer( string panelName, string color, string aMin, string aMax, bool cursor = false )
            {
                var NewElement = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = { Color = color },
                            RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                            CursorEnabled = cursor
                        },
                        new CuiElement().Parent,
                        panelName
                    }
                };
                return NewElement;
            }

            public static CuiElementContainer CreateOverlayContainer( string panelName, string color, string aMin, string aMax, bool cursor = false )
            {
                var NewElement = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = { Color = color },
                            RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                            CursorEnabled = cursor
                        },
                        new CuiElement().Parent = "Overlay",
                        panelName
                    }
                };
                return NewElement;
            }

            public static void CreatePanel( ref CuiElementContainer container, string panel, string color, string aMin, string aMax, bool cursor = false )
            {
                container.Add( new CuiPanel
                    {
                        Image = { Color = color },
                        RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                        CursorEnabled = cursor
                    },
                    panel );
            }

            public static void CreateLabel( ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter )
            {
                container.Add( new CuiLabel
                    {
                        Text = { Color = color, FontSize = size, Align = align, FadeIn = 1.0f, Text = text },
                        RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                    },
                    panel );
            }

            public static void CreateButton( ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter )
            {
                container.Add( new CuiButton
                    {
                        Button = { Color = color, Command = command, FadeIn = 1.0f },
                        RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                        Text = { Text = text, FontSize = size, Align = align }
                    },
                    panel );
            }

            public static void LoadImage( ref CuiElementContainer container, string panel, string img, string aMin, string aMax )
            {
                if ( img.StartsWith( "http" ) || img.StartsWith( "www" ) )
                {
                    container.Add( new CuiElement
                    {
                        Parent = panel,
                        Components =
                        {
                            new CuiRawImageComponent { Url = img, Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                            new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax }
                        }
                    } );
                }
                else
                {
                    container.Add( new CuiElement
                    {
                        Parent = panel,
                        Components =
                        {
                            new CuiRawImageComponent { Png = img, Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                            new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax }
                        }
                    } );
                }
            }

            public static void CreateTextOutline( ref CuiElementContainer element, string panel, string colorText, string colorOutline, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter )
            {
                element.Add( new CuiElement
                {
                    Parent = panel,
                    Components =
                    {
                        new CuiTextComponent { Color = colorText, FontSize = size, Align = align, Text = text },
                        new CuiOutlineComponent { Distance = "1 1", Color = colorOutline },
                        new CuiRectTransformComponent { AnchorMax = aMax, AnchorMin = aMin }
                    }
                } );
            }
        }

        private Dictionary<string, string> _uiColors = new Dictionary<string, string>
        {
            { "black", "0 0 0 1.0" },
            { "dark", "0.1 0.1 0.1 0.98" },
            { "header", "1 1 1 0.3" },
            { "light", ".564 .564 .564 1.0" },
            { "grey1", "0.6 0.6 0.6 1.0" },
            { "brown", "0.3 0.16 0.0 1.0" },
            { "yellow", "0.9 0.9 0.0 1.0" },
            { "orange", "1.0 0.65 0.0 1.0" },
            { "limegreen", "0.42 1.0 0 1.0" },
            { "blue", "0.2 0.6 1.0 1.0" },
            { "red", "1.0 0.1 0.1 1.0" },
            { "white", "1 1 1 1" },
            { "green", "0.28 0.82 0.28 1.0" },
            { "grey", "0.85 0.85 0.85 1.0" },
            { "lightblue", "0.6 0.86 1.0 1.0" },
            { "buttonbg", "0.2 0.2 0.2 0.7" },
            { "buttongreen", "0.133 0.965 0.133 0.9" },
            { "buttonred", "0.964 0.133 0.133 0.9" },
            { "buttongrey", "0.8 0.8 0.8 0.9" },
        };

        //////////////////////////////////////////////////////////////////////////////////////
        // GUI
        //////////////////////////////////////////////////////////////////////////////////////
        private Dictionary<string, Timer> _timers = new Dictionary<string, Timer>();

        private void OnScreen( BasePlayer player, string msg )
        {
            if ( _timers.ContainsKey( player.userID.ToString() ) )
            {
                _timers[player.userID.ToString()].Destroy();
                _timers.Remove( player.userID.ToString() );
            }

            CuiHelper.DestroyUi( player, PanelOnScreen );
            var element = UI.CreateOverlayContainer( PanelOnScreen, "0.0 0.0 0.0 0.0", "0.15 0.65", "0.85 .85", false );
            UI.CreateTextOutline( ref element, PanelOnScreen, _uiColors["black"], _uiColors["white"], msg, 24, "0.0 0.0", "1.0 1.0" );
            CuiHelper.AddUi( player, element );
            _timers.Add( player.userID.ToString(), timer.Once( 3, () => CuiHelper.DestroyUi( player, PanelOnScreen ) ) );
        }

        private void BackgroundPanel( BasePlayer player )
        {
            CuiHelper.DestroyUi( player, PanelBackground );
            var element = UI.CreateOverlayContainer( PanelBackground, "0 0 0 0", ".1 .15", ".9 .95", true );
            UI.CreatePanel( ref element, PanelBackground, "0.1 0.1 0.1 0.8", "0 0", "1 1" );
            if ( !string.IsNullOrEmpty( BackgroundURL ) )
            {
                var image = BackgroundURL;
                if ( ImageLibrary )
                {
                    image = TryForImage( "Background" );
                }

                UI.LoadImage( ref element, PanelBackground, image, "0 0", "1 1" );
            }

            CuiHelper.AddUi( player, element );
        }


        private readonly Dictionary<ulong, PLayerGUI> PlayerGUI = new Dictionary<ulong, PLayerGUI>();

        private class PLayerGUI
        {
            public ulong guiid;
            public int page;
            public bool open;
        }

        [ConsoleCommand( "UI_ToggleKitMenu" )]
        private void cmdUI_ToggleKitMenu( ConsoleSystem.Arg arg )
        {
            var player = arg.Connection.player as BasePlayer;
            if ( player == null )
            {
                return;
            }

            if ( PlayerGUI.ContainsKey( player.userID ) )
            {
                if ( PlayerGUI[player.userID].open )
                {
                    PlayerGUI[player.userID].open = false;
                    DestroyAllGUI( player );
                    return;
                }
            }

            NewKitPanel( player );
        }

        private void NewKitPanel( BasePlayer player, ulong guiId = 0 )
        {
            DestroyAllGUI( player );
            GUIKit kitpanel;
            if ( !GUIKits.TryGetValue( guiId, out kitpanel ) )
            {
                return;
            }

            BackgroundPanel( player );
            RefreshKitPanel( player, guiId );
        }

        private void RefreshKitPanel( BasePlayer player, ulong guiId, int page = 0 )
        {
            CuiHelper.DestroyUi( player, PanelKits );
            var element = UI.CreateOverlayContainer( PanelKits, "0 0 0 0", ".1 .15", ".9 .95" );
            PLayerGUI playerGUI;
            if ( !PlayerGUI.TryGetValue( player.userID, out playerGUI ) )
            {
                PlayerGUI[player.userID] = playerGUI = new PLayerGUI();
            }

            playerGUI.guiid = guiId;
            playerGUI.page = page;
            PlayerGUI[player.userID].open = true;
            var npcCheck = false;
            if ( guiId != 0 )
            {
                npcCheck = true;
            }

            var kitpanel = GUIKits[guiId];
            var Kits = new List<string>();
            if ( ShowUnavailableKits )
            {
                Kits = kitpanel.kits.Where( k => storedData.Kits.ContainsKey( k.ToLower() ) ).ToList();
            }
            else
            {
                foreach ( var entry in kitpanel.kits )
                {
                    string reason;
                    var cansee = CanSeeKit( player, entry.ToLower(), npcCheck, out reason );
                    if ( !cansee && string.IsNullOrEmpty( reason ) )
                    {
                        continue;
                    }

                    Kits.Add( entry );
                }
            }

            var entriesallowed = 10;
            var remainingentries = Kits.Count - ( page * entriesallowed );
            UI.CreateTextOutline( ref element, PanelKits, _uiColors["white"], _uiColors["black"], kitpanel.description, 20, "0.1 0.9", "0.9 1" );
            var i = 0;
            var n = 0;
            var shownentries = page * entriesallowed;
            foreach ( var entry in Kits )
            {
                i++;
                if ( i < shownentries + 1 )
                {
                    continue;
                }
                else if ( i <= shownentries + entriesallowed )
                {
                    var pos = KitSquarePos( n, remainingentries );
                    CreateKitEntry( player, ref element, PanelKits, pos, entry );
                    n++;
                    if ( n == entriesallowed )
                    {
                        break;
                    }
                }
            }

            if ( player.net.connection.authLevel == 2 || permission.UserHasPermission( player.UserIDString, this.Title + ".admin" ) )
            {
                UI.CreateButton( ref element, PanelKits, _uiColors["buttongrey"], GetMsg( "AddKit", player.UserIDString ), 14, $".02 .02", ".07 .06", $"UI_AddKit {0}" );
            }

            if ( page >= 1 )
            {
                UI.CreateButton( ref element, PanelKits, _uiColors["buttongrey"], "<<", 20, $".79 .02", ".84 .06", $"kit.show {page - 1}" );
            }

            if ( remainingentries > entriesallowed )
            {
                UI.CreateButton( ref element, PanelKits, _uiColors["buttongrey"], ">>", 20, $".86 .02", ".91 .06", $"kit.show {page + 1}" );
            }

            UI.CreateButton( ref element, PanelKits, _uiColors["buttonred"], GetMsg( "Close", player.UserIDString ), 14, $".93 .02", ".98 .06", $"kit.close" );
            CuiHelper.AddUi( player, element );
        }

        private void CreateKitEntry( BasePlayer player, ref CuiElementContainer element, string panel, float[] pos, string entry )
        {
            var kit = storedData.Kits[entry.ToLower()];
            var kitData = GetKitData( player.userID, entry.ToLower() );
            var image = kit.image ?? "http://i.imgur.com/xxQnE1R.png";
            if ( ImageLibrary )
            {
                image = TryForImage( kit.name.Replace( " ", "" ) );
            }

            UI.CreatePanel( ref element, PanelKits, _uiColors["header"], $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}" );
            UI.LoadImage( ref element, PanelKits, image, $"{pos[2] - .06f} {pos[1] + 0.01f}", $"{pos[2] - .005f} {pos[1] + .1f}" );
            UI.CreateLabel( ref element, PanelKits, _uiColors["white"], kit.name, 12, $"{pos[0] + .005f} {pos[3] - .07f}", $"{pos[2] - .005f} {pos[3] - .01f}" );
            UI.CreateLabel( ref element, PanelKits, _uiColors["white"], kit.description ?? string.Empty, 12, $"{pos[0] + .005f} {pos[3] - .18f}", $"{pos[2] - .005f} {pos[3] - .07f}", TextAnchor.UpperLeft );
            UI.CreateLabel( ref element, PanelKits, _uiColors["white"], string.Format( GetMsg( "KitCooldown", player.UserIDString ), kit.cooldown <= 0 ? GetMsg( "None", player.UserIDString ) : CurrentTime() > kitData.cooldown ? MinuteFormat( kit.cooldown / 60 ).ToString() : "<color=red> -" + MinuteFormat( ( double ) Math.Abs( Math.Ceiling( CurrentTime() - kitData.cooldown ) ) / 60 ) + "</color>" ), 12, $"{pos[0] + .005f} {pos[3] - .24f}", $"{pos[2] - .005f} {pos[3] - .21f}", TextAnchor.MiddleLeft );
            UI.CreateLabel( ref element, PanelKits, _uiColors["white"], string.Format( GetMsg( "KitUses", player.UserIDString ), kit.max <= 0 ? GetMsg( "Unlimited" ) : kit.max - kitData.max < kit.max ? $"<color=red>{kit.max - kitData.max}</color>/{kit.max}" : $"{kit.max - kitData.max}/{kit.max}" ), 12, $"{pos[0] + .005f} {pos[3] - .27f}", $"{pos[2] - .005f} {pos[3] - .24f}", TextAnchor.MiddleLeft );
            if ( player.net.connection.authLevel == 2 || permission.UserHasPermission( player.UserIDString, this.Title + ".admin" ) )
            {
                UI.CreateButton( ref element, PanelKits, _uiColors["buttonred"], GetMsg( "RemoveKit", player.UserIDString ), 14, $"{pos[0] + .067f} {pos[1] + 0.01f}", $"{pos[0] + .122f} {pos[1] + .07f}", $"UI_RemoveKit {entry.ToLower()}" );
            }

            if ( !ShowUnavailableKits )
            {
                UI.CreateButton( ref element, PanelKits, _uiColors["dark"], GetMsg( "Redeem" ), 12, $"{pos[0] + .005f} {pos[1] + 0.01f}", $"{pos[0] + .06f} {pos[1] + .07f}", $"kit.gui {entry.ToLower()}" );
            }
            else
            {
                string reason;
                var cansee = CanSeeKit( player, entry.ToLower(), PlayerGUI[player.userID].guiid == 0 ? false : true, out reason );
                if ( !cansee && string.IsNullOrEmpty( reason ) )
                {
                    UI.CreatePanel( ref element, PanelKits, _uiColors["header"], $"{pos[0] + .005f} {pos[1] + 0.01f}", $"{pos[0] + .06f} {pos[1] + .07f}" );
                    UI.CreateLabel( ref element, PanelKits, _uiColors["white"], "<color=red>" + GetMsg( "Unavailable", player.UserIDString ) + "</color>", 12, $"{pos[0] + .005f} {pos[1] + 0.01f}", $"{pos[0] + .06f} {pos[1] + .07f}" );
                }
                else
                {
                    UI.CreateButton( ref element, PanelKits, _uiColors["dark"], GetMsg( "Redeem" ), 12, $"{pos[0] + .005f} {pos[1] + 0.01f}", $"{pos[0] + .06f} {pos[1] + .07f}", $"kit.gui {entry.ToLower()}" );
                }
            }
        }

        private float[] KitSquarePos( int number, double count )
        {
            var position = new Vector2( 0.015f, 0.5f );
            var dimensions = new Vector2( 0.19f, 0.4f );
            float offsetY = 0;
            float offsetX = 0;
            if ( count < 10 )
            {
                position.x = ( float ) ( 1 - ( ( ( dimensions.x + .005f ) * ( count > 1 ? ( Math.Round( ( count / 2 ), MidpointRounding.AwayFromZero ) ) : 1 ) ) ) ) / 2;
            }

            if ( number >= 0 && number < 2 )
            {
                offsetY = ( -0.01f - dimensions.y ) * number;
            }

            if ( number > 1 && number < 4 )
            {
                offsetX = ( .005f + dimensions.x ) * 1;
                offsetY = ( -0.01f - dimensions.y ) * ( number - 2 );
            }

            if ( number > 3 && number < 6 )
            {
                offsetX = ( .005f + dimensions.x ) * 2;
                offsetY = ( -0.01f - dimensions.y ) * ( number - 4 );
            }

            if ( number > 5 && number < 8 )
            {
                offsetX = ( .005f + dimensions.x ) * 3;
                offsetY = ( -0.01f - dimensions.y ) * ( number - 6 );
            }

            if ( number > 7 && number < 10 )
            {
                offsetX = ( .005f + dimensions.x ) * 4;
                offsetY = ( -0.01f - dimensions.y ) * ( number - 8 );
            }

            var offset = new Vector2( offsetX, offsetY );
            var posMin = position + offset;
            var posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }


        [ConsoleCommand( "UI_RemoveKit" )]
        private void cmdUI_RemoveKit( ConsoleSystem.Arg arg )
        {
            var player = arg.Connection.player as BasePlayer;
            if ( player == null || ( player.net.connection.authLevel < 2 && !permission.UserHasPermission( player.UserIDString, this.Title + ".admin" ) ) )
            {
                return;
            }

            CuiHelper.DestroyUi( player, PanelOnScreen );
            var kit = string.Join( " ", arg.Args );
            if ( GUIKits[PlayerGUI[player.userID].guiid].kits.Contains( kit ) )
            {
                GUIKits[PlayerGUI[player.userID].guiid].kits.Remove( kit );
                var config = Config.ReadObject<Dictionary<string, object>>();
                config["NPC - GUI Kits"] = GUIKits;
                Config.WriteObject( config );
                CuiHelper.DestroyUi( player, PanelOnScreen );
                RefreshKitPanel( player, PlayerGUI[player.userID].guiid, PlayerGUI[player.userID].page );
            }
        }

        [ConsoleCommand( "UI_AddKit" )]
        private void cmdUI_AddKit( ConsoleSystem.Arg arg )
        {
            var player = arg.Connection.player as BasePlayer;
            if ( player == null || ( player.net.connection.authLevel < 2 && !permission.UserHasPermission( player.UserIDString, this.Title + ".admin" ) ) )
            {
                return;
            }

            CuiHelper.DestroyUi( player, PanelOnScreen );
            int page;
            if ( !int.TryParse( arg.Args[0], out page ) )
            {
                if ( arg.Args[0] == "close" )
                {
                    return;
                }
                else
                {
                    GUIKits[PlayerGUI[player.userID].guiid].kits.Add( string.Join( " ", arg.Args ) );
                    var config = Config.ReadObject<Dictionary<string, object>>();
                    config["NPC - GUI Kits"] = GUIKits;
                    Config.WriteObject( config );
                    CuiHelper.DestroyUi( player, PanelOnScreen );
                    RefreshKitPanel( player, PlayerGUI[player.userID].guiid, PlayerGUI[player.userID].page );
                    return;
                }
            }

            double count = GetAllKits().Count() - GUIKits[PlayerGUI[player.userID].guiid].kits.Where( k => storedData.Kits.ContainsKey( k.ToLower() ) ).ToList().Count();
            if ( count == 0 )
            {
                SendReply( player, GetMsg( "NoKitsFound", player.UserIDString ) );
                return;
            }

            var element = UI.CreateOverlayContainer( PanelOnScreen, _uiColors["dark"], ".1 .15", ".9 .95" );
            UI.CreateTextOutline( ref element, PanelOnScreen, _uiColors["white"], _uiColors["black"], GetMsg( "AddKitToMenu", player.UserIDString ), 20, "0.1 0.9", "0.9 .99", TextAnchor.UpperCenter );
            var entriesallowed = 30;
            var remainingentries = count - ( page * ( entriesallowed ) );
            var totalpages = ( Math.Floor( count / ( entriesallowed ) ) );
            {
                if ( page < totalpages - 1 )
                {
                    UI.CreateButton( ref element, PanelOnScreen, _uiColors["buttongrey"], GetMsg( "Last", player.UserIDString ), 16, "0.8 0.02", "0.85 0.06", $"UI_AddKit {totalpages}" );
                }

                if ( remainingentries > entriesallowed )
                {
                    UI.CreateButton( ref element, PanelOnScreen, _uiColors["buttongrey"], GetMsg( "Next", player.UserIDString ), 16, "0.74 0.02", "0.79 0.06", $"UI_AddKit {page + 1}" );
                }

                if ( page > 0 )
                {
                    UI.CreateButton( ref element, PanelOnScreen, _uiColors["buttongrey"], GetMsg( "Back", player.UserIDString ), 16, "0.68 0.02", "0.73 0.06", $"UI_AddKit {page - 1}" );
                }

                if ( page > 1 )
                {
                    UI.CreateButton( ref element, PanelOnScreen, _uiColors["buttongrey"], GetMsg( "First", player.UserIDString ), 16, "0.62 0.02", "0.67 0.06", $"UI_AddKit {0}" );
                }
            }
            var i = 0;
            var n = 0;
            double shownentries = page * entriesallowed;
            foreach ( var kitname in GetAllKits().Where( k => !GUIKits[PlayerGUI[player.userID].guiid].kits.Contains( k ) ).OrderBy( k => k ) )
            {
                i++;
                if ( i < shownentries + 1 )
                {
                    continue;
                }
                else if ( i <= shownentries + entriesallowed )
                {
                    CreateKitButton( ref element, PanelOnScreen, _uiColors["header"], kitname, $"UI_AddKit {kitname}", n );
                    n++;
                }
            }

            UI.CreateButton( ref element, PanelOnScreen, _uiColors["buttonred"], GetMsg( "Close", player.UserIDString ), 14, $".93 .02", ".98 .06", $"UI_AddKit close" );
            CuiHelper.AddUi( player, element );
        }

        private void CreateKitButton( ref CuiElementContainer container, string panelName, string color, string name, string cmd, int num )
        {
            var pos = CalcKitButtonPos( num );
            UI.CreateButton( ref container, panelName, color, name, 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", cmd );
        }

        private float[] CalcKitButtonPos( int number )
        {
            var position = new Vector2( 0.05f, 0.82f );
            var dimensions = new Vector2( 0.125f, 0.125f );
            float offsetY = 0;
            float offsetX = 0;
            if ( number >= 0 && number < 6 )
            {
                offsetX = ( 0.03f + dimensions.x ) * number;
            }

            if ( number > 5 && number < 12 )
            {
                offsetX = ( 0.03f + dimensions.x ) * ( number - 6 );
                offsetY = ( -0.06f - dimensions.y ) * 1;
            }

            if ( number > 11 && number < 18 )
            {
                offsetX = ( 0.03f + dimensions.x ) * ( number - 12 );
                offsetY = ( -0.06f - dimensions.y ) * 2;
            }

            if ( number > 17 && number < 24 )
            {
                offsetX = ( 0.03f + dimensions.x ) * ( number - 18 );
                offsetY = ( -0.06f - dimensions.y ) * 3;
            }

            if ( number > 23 && number < 36 )
            {
                offsetX = ( 0.03f + dimensions.x ) * ( number - 24 );
                offsetY = ( -0.06f - dimensions.y ) * 4;
            }

            var offset = new Vector2( offsetX, offsetY );
            var posMin = position + offset;
            var posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        private string MinuteFormat( double minutes )
        {
            var dateDifference = TimeSpan.FromMinutes( minutes );
            var hours = dateDifference.Hours;
            hours += ( dateDifference.Days * 24 );
            return string.Format( "{0:00}:{1:00}:{2:00}", hours, dateDifference.Minutes, dateDifference.Seconds );
        }

        private void DestroyAllGUI( BasePlayer player )
        {
            CuiHelper.DestroyUi( player, "KitOverlay" );
            CuiHelper.DestroyUi( player, PanelOnScreen );
            CuiHelper.DestroyUi( player, PanelKits );
            CuiHelper.DestroyUi( player, PanelBackground );
        }

        private void OnUseNPC( BasePlayer npc, BasePlayer player )
        {
            if ( !GUIKits.ContainsKey( npc.userID ) )
            {
                return;
            }

            NewKitPanel( player, npc.userID );
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // External Hooks
        //////////////////////////////////////////////////////////////////////////////////////
        [HookMethod( "GetAllKits" )]
        public string[] GetAllKits() => storedData.Kits.Keys.ToArray();

        [HookMethod( "GetKitInfo" )]
        public object GetKitInfo( string kitname )
        {
            if ( storedData.Kits.ContainsKey( kitname.ToLower() ) )
            {
                var kit = storedData.Kits[kitname.ToLower()];
                var obj = new JObject
                {
                    ["name"] = kit.name,
                    ["permission"] = kit.permission,
                    ["npconly"] = kit.npconly,
                    ["max"] = kit.max,
                    ["image"] = kit.image,
                    ["hide"] = kit.hide,
                    ["description"] = kit.description,
                    ["cooldown"] = kit.cooldown,
                    ["building"] = kit.building,
                    ["authlevel"] = kit.authlevel
                };
                var items = new JArray();
                foreach ( var itemEntry in kit.items )
                {
                    var item = new JObject
                    {
                        ["amount"] = itemEntry.amount,
                        ["container"] = itemEntry.container,
                        ["itemid"] = itemEntry.itemid,
                        ["skinid"] = itemEntry.skinid,
                        ["weapon"] = itemEntry.weapon,
                        ["blueprint"] = itemEntry.blueprintTarget > 0
                    };
                    var mods = new JArray();
                    foreach ( var mod in itemEntry.mods )
                    {
                        mods.Add( mod );
                    }

                    item["mods"] = mods;
                    items.Add( item );
                }

                obj["items"] = items;
                return obj;
            }

            return null;
        }

        [HookMethod( "GetKitContents" )]
        public string[] GetKitContents( string kitname )
        {
            if ( storedData.Kits.ContainsKey( kitname.ToLower() ) )
            {
                var items = new List<string>();
                foreach ( var item in storedData.Kits[kitname.ToLower()].items )
                {
                    var itemstring = $"{item.itemid}_{item.amount}";
                    if ( item.mods.Count > 0 )
                    {
                        foreach ( var mod in item.mods )
                        {
                            itemstring = itemstring + $"_{mod}";
                        }
                    }

                    items.Add( itemstring );
                }

                if ( items.Count > 0 )
                {
                    return items.ToArray();
                }
            }

            return null;
        }

        [HookMethod( "KitCooldown" )]
        public double KitCooldown( string kitname ) => storedData.Kits[kitname].cooldown;

        [HookMethod( "PlayerKitCooldown" )]
        public double PlayerKitCooldown( ulong ID, string kitname ) => storedData.Kits[kitname].cooldown <= 0 ? 0 : CurrentTime() > GetKitData( ID, kitname ).cooldown ? storedData.Kits[kitname].cooldown : CurrentTime() - GetKitData( ID, kitname ).cooldown;

        [HookMethod( "KitDescription" )]
        public string KitDescription( string kitname ) => storedData.Kits[kitname].description;

        [HookMethod( "KitMax" )]
        public int KitMax( string kitname ) => storedData.Kits[kitname].max;

        [HookMethod( "PlayerKitMax" )]
        public double PlayerKitMax( ulong ID, string kitname ) => storedData.Kits[kitname].max <= 0 ? 0 : storedData.Kits[kitname].max - GetKitData( ID, kitname ).max < storedData.Kits[kitname].max ? storedData.Kits[kitname].max - GetKitData( ID, kitname ).max : 0;

        [HookMethod( "KitImage" )]
        public string KitImage( string kitname ) => storedData.Kits[kitname].image;

        //////////////////////////////////////////////////////////////////////////////////////
        // Console Command
        //////////////////////////////////////////////////////////////////////////////////////
        [ConsoleCommand( "kit.gui" )]
        private void CmdConsoleKitGui( ConsoleSystem.Arg arg )
        {
            if ( arg.Connection == null )
            {
                SendReply( arg, "You can't use this command from the server console" );
                return;
            }

            if ( !arg.HasArgs() )
            {
                SendReply( arg, "You are not allowed to use manually this command" );
                return;
            }

            var player = arg.Player();
            var kitname = arg.Args[0] /*.Substring(1, arg.Args[0].Length - 2)*/;
            TryGiveKit( player, kitname );
        }

        [ConsoleCommand( "kit.close" )]
        private void CmdConsoleKitClose( ConsoleSystem.Arg arg )
        {
            if ( arg.Connection == null )
            {
                SendReply( arg, "You can't use this command from the server console" );
                return;
            }

            PlayerGUI[arg.Player().userID].open = false;
            DestroyAllGUI( arg.Player() );
        }

        [ConsoleCommand( "kit.show" )]
        private void CmdConsoleKitShow( ConsoleSystem.Arg arg )
        {
            if ( arg.Connection == null )
            {
                SendReply( arg, "You can't use this command from the server console" );
                return;
            }

            if ( !arg.HasArgs() )
            {
                SendReply( arg, "You are not allowed to use manually this command" );
                return;
            }

            var player = arg.Player();
            PLayerGUI playerGUI;
            if ( !PlayerGUI.TryGetValue( player.userID, out playerGUI ) )
            {
                return;
            }

            RefreshKitPanel( player, playerGUI.guiid, arg.GetInt( 0 ) );
        }

        private List<BasePlayer> FindPlayer( string arg )
        {
            var listPlayers = new List<BasePlayer>();

            ulong steamid;
            ulong.TryParse( arg, out steamid );
            var lowerarg = arg.ToLower();

            foreach ( var player in BasePlayer.activePlayerList )
            {
                if ( steamid != 0L )
                {
                    if ( player.userID == steamid )
                    {
                        listPlayers.Clear();
                        listPlayers.Add( player );
                        return listPlayers;
                    }
                }

                var lowername = player.displayName.ToLower();
                if ( lowername.Contains( lowerarg ) )
                {
                    listPlayers.Add( player );
                }
            }

            return listPlayers;
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // Chat Command
        //////////////////////////////////////////////////////////////////////////////////////

        private bool hasAccess( BasePlayer player )
        {
            if ( player.net?.connection?.authLevel > 1 || permission.UserHasPermission( player.UserIDString, this.Title + ".admin" ) )
            {
                return true;
            }

            return false;
        }

        private void SendListKitEdition( BasePlayer player )
        {
            SendReply( player, "authlevel XXX\r\nbuilding \"filename\" => buy a building to paste from\r\ncooldown XXX\r\ndescription \"description text here\" => set a description for this kit\r\nhide TRUE/FALSE => dont show this kit in lists (EVER)\r\nimage \"image http url\" => set an image for this kit (gui only)\r\nitems => set new items for your kit (will copy your inventory)\r\nmax XXX\r\nnpconly TRUE/FALSE => only get this kit out of a NPC\r\npermission \"permission name\" => set the permission needed to get this kit" );
        }

        [ChatCommand( "kit" )]
        private void CmdChatKit( BasePlayer player, string command, string[] args )
        {
            if ( args.Length == 0 )
            {
                if ( GUIKits.ContainsKey( 0 ) )
                {
                    NewKitPanel( player, 0 );
                }
                else
                {
                    var reason = string.Empty;
                    foreach ( var pair in storedData.Kits )
                    {
                        var cansee = CanSeeKit( player, pair.Key, false, out reason );
                        if ( !cansee && string.IsNullOrEmpty( reason ) )
                        {
                            continue;
                        }

                        SendReply( player, $"{pair.Value.name} - {pair.Value.description} {reason}" );
                    }
                }

                return;
            }

            if ( args.Length == 1 )
            {
                switch ( args[0] )
                {
                    case "help":
                        SendReply( player, "====== Player Commands ======" );
                        SendReply( player, "/kit => to get the list of kits" );
                        SendReply( player, "/kit KITNAME => to redeem the kit" );
                        if ( !hasAccess( player ) )
                        {
                            return;
                        }

                        SendReply( player, "====== Admin Commands ======" );
                        SendReply( player, "/kit add KITNAME => add a kit" );
                        SendReply( player, "/kit remove KITNAME => remove a kit" );
                        SendReply( player, "/kit edit KITNAME => edit a kit" );
                        SendReply( player, "/kit list => get a raw list of kits (the real full list)" );
                        SendReply( player, "/kit give PLAYER/STEAMID KITNAME => give a kit to a player" );
                        SendReply( player, "/kit resetkits => deletes all kits" );
                        SendReply( player, "/kit resetdata => reset player data" );
                        break;
                    case "add":
                    case "remove":
                    case "edit":
                        if ( !hasAccess( player ) )
                        {
                            SendReply( player, "You don't have access to this command" );
                            return;
                        }

                        SendReply( player, $"/kit {args[0]} KITNAME" );
                        break;
                    case "give":
                        if ( !hasAccess( player ) )
                        {
                            SendReply( player, "You don't have access to this command" );
                            return;
                        }

                        SendReply( player, "/kit give PLAYER/STEAMID KITNAME" );
                        break;
                    case "list":
                        if ( !hasAccess( player ) )
                        {
                            SendReply( player, "You don't have access to this command" );
                            return;
                        }

                        foreach ( var kit in storedData.Kits.Values )
                        {
                            SendReply( player, $"{kit.name} - {kit.description}" );
                        }

                        break;
                    case "items":
                        break;
                    case "resetkits":
                        if ( !hasAccess( player ) )
                        {
                            SendReply( player, "You don't have access to this command" );
                            return;
                        }

                        storedData.Kits.Clear();
                        kitEditor.Clear();
                        ResetData();
                        SaveKits();
                        SendReply( player, "Resetted all kits and player data" );
                        break;
                    case "resetdata":
                        if ( !hasAccess( player ) )
                        {
                            SendReply( player, "You don't have access to this command" );
                            return;
                        }

                        ResetData();
                        SendReply( player, "Resetted all player data" );
                        break;
                    default:
                        TryGiveKit( player, args[0].ToLower() );
                        break;
                }

                if ( args[0] != "items" )
                {
                    return;
                }
            }

            if ( !hasAccess( player ) )
            {
                SendReply( player, "You don't have access to this command" );
                return;
            }

            string kitname;
            switch ( args[0] )
            {
                case "add":
                    kitname = args[1].ToLower();
                    if ( storedData.Kits.ContainsKey( kitname ) )
                    {
                        SendReply( player, "This kit already exists." );
                        return;
                    }

                    storedData.Kits[kitname] = new Kit { name = args[1] };
                    kitEditor[player.userID] = kitname;
                    SendReply( player, "You've created a new kit: " + args[1] );
                    SendListKitEdition( player );
                    break;
                case "give":
                    if ( args.Length < 3 )
                    {
                        SendReply( player, "/kit give PLAYER/STEAMID KITNAME" );
                        return;
                    }

                    kitname = args[2].ToLower();
                    if ( !storedData.Kits.ContainsKey( kitname ) )
                    {
                        SendReply( player, "This kit doesn't seem to exist." );
                        return;
                    }

                    var findPlayers = FindPlayer( args[1] );
                    if ( findPlayers.Count == 0 )
                    {
                        SendReply( player, "No players found." );
                        return;
                    }

                    if ( findPlayers.Count > 1 )
                    {
                        SendReply( player, "Multiple players found." );
                        return;
                    }

                    GiveKit( findPlayers[0], kitname );
                    SendReply( player, $"You gave {findPlayers[0].displayName} the kit: {storedData.Kits[kitname].name}" );
                    SendReply( findPlayers[0], string.Format( "You've received the kit {1} from {0}", player.displayName, storedData.Kits[kitname].name ) );
                    break;
                case "edit":
                    kitname = args[1].ToLower();
                    if ( !storedData.Kits.ContainsKey( kitname ) )
                    {
                        SendReply( player, "This kit doesn't seem to exist" );
                        return;
                    }

                    kitEditor[player.userID] = kitname;
                    SendReply( player, $"You are now editing the kit: {kitname}" );
                    SendListKitEdition( player );
                    break;
                case "remove":
                    kitname = args[1].ToLower();
                    if ( !storedData.Kits.Remove( kitname ) )
                    {
                        SendReply( player, "This kit doesn't seem to exist" );
                        return;
                    }

                    SendReply( player, $"{kitname} was removed" );
                    if ( kitEditor[player.userID] == kitname )
                    {
                        kitEditor.Remove( player.userID );
                    }

                    break;
                default:
                    if ( !kitEditor.TryGetValue( player.userID, out kitname ) )
                    {
                        SendReply( player, "You are not creating or editing a kit" );
                        return;
                    }

                    Kit kit;
                    if ( !storedData.Kits.TryGetValue( kitname, out kit ) )
                    {
                        SendReply( player, "There was an error while getting this kit, was it changed while you were editing it?" );
                        return;
                    }

                    for ( var i = 0; i < args.Length; i++ )
                    {
                        object editvalue;
                        var key = args[i].ToLower();
                        switch ( key )
                        {
                            case "items":
                                kit.items = GetPlayerItems( player );
                                SendReply( player, "The items were copied from your inventory" );
                                continue;
                            case "building":
                                var buildingvalue = args[++i];
                                if ( buildingvalue.ToLower() == "false" )
                                {
                                    editvalue = kit.building = string.Empty;
                                }
                                else
                                {
                                    editvalue = kit.building = buildingvalue;
                                }

                                break;
                            case "name":
                                continue;
                            case "description":
                                editvalue = kit.description = args[++i];
                                break;
                            case "max":
                                editvalue = kit.max = int.Parse( args[++i] );
                                break;
                            case "cooldown":
                                editvalue = kit.cooldown = double.Parse( args[++i] );
                                break;
                            case "authlevel":
                                editvalue = kit.authlevel = int.Parse( args[++i] );
                                break;
                            case "hide":
                                editvalue = kit.hide = bool.Parse( args[++i] );
                                break;
                            case "npconly":
                                editvalue = kit.npconly = bool.Parse( args[++i] );
                                break;
                            case "permission":
                                editvalue = kit.permission = args[++i];
                                if ( !kit.permission.StartsWith( "kits." ) )
                                {
                                    editvalue = kit.permission = $"kits.{kit.permission}";
                                }

                                InitializePermissions();
                                break;
                            case "image":
                                editvalue = kit.image = args[++i];
                                break;
                            default:
                                SendReply( player, $"{args[i]} is not a valid argument" );
                                continue;
                        }

                        SendReply( player, $"{key} set to {editvalue ?? "null"}" );
                    }

                    break;
            }
            SaveKits();
        }
    }
}